using System.IO.Ports;
using System.Text.RegularExpressions;

using QuiCLI.Command;

using RFDump.Bootloader;
using RFDump.Bootloader.UBoot;
using RFDump.Extensions;
using RFDump.Service;

using Spectre.Console;

namespace RFDump.Command;
public partial class DumpCommand(SerialService serialService)
{
    private readonly SerialService _serialService = serialService;
    private IBootHandler? _bootHandler = null;
    private readonly Regex _dataRegex = DataLineMatcher();

    [Command("ports")]
    public string Ports()
    {
        var ports = _serialService.GetPorts();
        if (ports.IsFailure)
        {
            AnsiConsole.Write(new Markup($"[bold red]{ports.Error}[/]"));
            return string.Empty;
        }
        AnsiConsole.Write(new Markup($"Available ports: [bold yellow]{string.Join(", ", ports.Value)}[/]"));
        return string.Empty;
    }

    [Command("dump")]
    public async Task Dump(string port, int baudRate = 115200, string filename = "firmware.bin", uint chunkSize = 0x1000)
    {
        var result = _serialService.Connect(port, baudRate);

        if (result.IsFailure)
        {
            AnsiConsole.Write(new Markup($"[bold red]{result.Error}[/]"));
        }

        var serial = result.Value;
        serial.DataReceived += Initialize;

        await AnsiConsole.Progress().AutoClear(false).StartAsync(async ctx =>
        {
            var bootloader = ctx.AddTask("Access boot loader...", true);
            bootloader.IsIndeterminate = true;
            while (!_bootHandler?.IsReady ?? true)
            {
                await Task.Delay(100);
            }
            bootloader.Increment(100);
            serial.DataReceived -= Initialize;

            var bootloaderHandler = new UBootHandler(serial);
            var gatherInfo = ctx.AddTask("Gathering information...", true);
            await bootloaderHandler.Initialize();
            gatherInfo.Increment(100);

            var address = bootloaderHandler.BootAddress;
            var endAddress = address + 0x1000000;
            var dumpProgress = ctx.AddTask($"Dumping memory [cyan]0x{address:X}[/][yellow]/[/][cyan]0x{endAddress:X}[/]...", true, maxValue: endAddress);
            var currentAddress = address;
            await using var file = File.Create(filename);
            while (currentAddress < endAddress)
            {
                var dump = await serial.DumpMemoryBlock(currentAddress, chunkSize);
                var (success, lastKnownGoodAddress, binaryData) = ValidateDumpData(dump, currentAddress);
                if (!success)
                {
                    var step = chunkSize - ((currentAddress + chunkSize) - lastKnownGoodAddress);
                    currentAddress = lastKnownGoodAddress;
                    dumpProgress.Description = $"Dumping memory [cyan]0x{currentAddress:X}[/][yellow]/[/][cyan]0x{endAddress:X}[/]...";
                    dumpProgress.Increment(step);
                    continue;
                }
                currentAddress += chunkSize;
                dumpProgress.Description = $"Dumping memory [cyan]0x{currentAddress:X}[/][yellow]/[/][cyan]0x{endAddress:X}[/] ({Math.Round((double)file.Position / 1024, 0)}Kb)...";
                dumpProgress.Increment(chunkSize);
                await file.WriteAsync(binaryData);
                await file.FlushAsync();
            }
        });
    }

    private (bool success, uint lastKnownGoodAddress, byte[] binaryData) ValidateDumpData(string data, uint startAddress)
    {
        var lines = data.Split("\n");
        var expectedAddress = startAddress;
        var binaryData = new List<byte>();
        if (lines.Length < 2)
        {
            return (false, startAddress, []);
        }
        foreach (var line in lines)
        {
            var cleanLine = line.Replace("\r", "");
            if (string.IsNullOrEmpty(cleanLine))
            {
                continue;
            }

            if (!_dataRegex.IsMatch(cleanLine))
            {
                return (false, expectedAddress, []);
            }
            var match = _dataRegex.Match(cleanLine);
            var address = Convert.ToUInt32(match.Groups["address"].Value, 16);
            if (address != expectedAddress)
            {
                // If the address is not what we expect (should be sequential), we need to find the last known good address
                return (false, expectedAddress, []);
            }

            var bytes = match.Groups["bytes"].Value;
            var ascii = match.Groups["ascii"].Value;
            var (validData, validBytes) = ValidateBytes(bytes, ascii);
            if (!validData)
            {
                return (false, expectedAddress, []);
            }
            binaryData.AddRange(validBytes);
            expectedAddress += 0x10;
        }
        return (true, expectedAddress, binaryData.ToArray());
    }

    private static (bool, byte[]) ValidateBytes(string bytes, string ascii)
    {
        var values = bytes.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        var validBytes = new byte[16];
        if (values.Length != 16)
        {
            return (false, []);
        }
        var index = 0;
        foreach (var value in values)
        {
            if (value.Length != 2)
            {
                return (false, []);
            }
            try
            {
                validBytes[index] = Convert.ToByte(value, 16);
                index++;
            }
            catch
            {
                return (false, []);
            }
        }
        return (true, validBytes);
    }

    private void Initialize(object sender, SerialDataReceivedEventArgs e)
    {
        var sp = (SerialPort)sender;
        var data = sp.ReadExisting();

        foreach (var line in data.Split("\n"))
        {
            var cleanLine = line.Replace("\r", "");
            _bootHandler = Detector.DetectBootloader(cleanLine, sp);

            if (_bootHandler != null)
            {
                sp.DataReceived -= Initialize;
                _bootHandler.HandleBoot(data);
                break;
            }
        }
    }

    private static string EscapeMarkup(string data)
    {
        return data.Replace("[", "[[").Replace("]", "]]");
    }

    [GeneratedRegex(@"(?<address>[a-f0-9]+): (?<bytes>[a-z0-9\s]+)    (?<ascii>.*)")]
    private static partial Regex DataLineMatcher();
}