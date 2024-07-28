using System.IO.Ports;
using System.Text.RegularExpressions;

using QuiCLI.Command;

using RFDump.Extensions;
using RFDump.Service;

using Spectre.Console;

namespace RFDump.Command;
public class DumpCommand(SerialService serialService)
{
    private readonly SerialService _serialService = serialService;
    private bool _readyForInput = false;
    private Dictionary<string, string> _environment = new();
    private readonly Regex _dataRegex = new Regex(@"(?<address>[a-f0-9]+): (?<bytes>[a-z0-9\s]+)    (?<ascii>.*)");

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
    public async Task Dump(string port, uint chunkSize = 0x1000)
    {
        var result = _serialService.Connect(port);

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
            while (!_readyForInput)
            {
                await Task.Delay(100);
            }
            bootloader.Increment(100);
            serial.DataReceived -= Initialize;

            var gatherInfo = ctx.AddTask("Gathering information...", true);
            var help = await serial.WriteCommand("help\n");
            var commands = ParseUBootHelp(help);
            gatherInfo.Increment(20);
            if (commands.ContainsKey("printenv"))
            {
                var environ = await serial.WriteCommand("printenv\n");
                _environment = ParseKeyValues(environ);
            }
            gatherInfo.Increment(20);


            if (commands.ContainsKey("bdinfo"))
            {
                var bdinfo = await serial.WriteCommand("bdinfo\n");
                var boardInformation = ParseKeyValues(bdinfo);
            }
            gatherInfo.Increment(20);

            var address = DetectAddress();
            if (address == 0)
            {
                AnsiConsole.MarkupLine("[red]Failed to detect boot address[/]");
                return;
            }
            gatherInfo.Increment(40);

            var dumpProgress = ctx.AddTask($"Dumping memory from [cyan]0x{address:X}[/]...", true, maxValue: (address + 0x1000000));
            var currentAddress = address;
            await using var file = File.Create("c:\\temp\\dump.bin");
            while (currentAddress < address + 0x1000000)
            {
                var dump = await serial.DumpMemoryBlock(currentAddress, chunkSize);
                var (success, lastKnownGoodAddress, binaryData) = ValidateDumpData(dump, currentAddress);
                if (!success)
                {
                    currentAddress = lastKnownGoodAddress;
                    dumpProgress.Description = $"Dumping memory from [cyan]0x{currentAddress:X}[/]...";
                    dumpProgress.Value = currentAddress;
                    continue;
                }
                currentAddress += chunkSize;
                dumpProgress.Description = $"Dumping memory from [cyan]0x{currentAddress:X}[/]...";
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

    private static Dictionary<string, string> ParseKeyValues(string data)
    {
        var keyValues = new Dictionary<string, string>();
        foreach (var line in data.Split("\n"))
        {
            var cleanLine = line.Replace("\r", "");
            if (cleanLine.Contains("="))
            {
                var parts = cleanLine.Split("=");
                keyValues[parts[0]] = parts[1];
            }
        }
        return keyValues;
    }

    private uint DetectAddress()
    {
        if (_environment.TryGetValue("bootaddr", out var bootaddr))
        {
            if (bootaddr.Contains('+'))
            {
                uint addr = 0x0;
                foreach (var value in bootaddr.Split("+"))
                {
                    addr += Convert.ToUInt32(value.Trim(), 16);
                }
                return addr;
            }
            return Convert.ToUInt32(bootaddr, 16);
        }
        else if (_environment.TryGetValue("bootcmd", out var bootcmd) && bootcmd.StartsWith("bootm"))
        {
            var match = Regex.Match(bootcmd, @"0x([0-9a-fA-F]+)");
            if (match.Success)
            {
                return Convert.ToUInt32(match.Groups[1].Value, 16);
            }
        }
        return 0;
    }

    private void Initialize(object sender, SerialDataReceivedEventArgs e)
    {
        var sp = (SerialPort)sender;
        var data = sp.ReadExisting();

        foreach (var line in data.Split("\n"))
        {
            var cleanLine = line.Replace("\r", "");
            if (cleanLine.Contains("U-Boot"))
            {
                var version = GetUBootVersion(cleanLine);
                AnsiConsole.MarkupLine($"Detected [cyan]U-Boot[/] [yellow]{version}[/] bootloader...");
            }
            else if (cleanLine.StartsWith("Hit any key to stop autoboot"))
            {
                sp.Write("\n");
            }
            else
            {
                _readyForInput = cleanLine.StartsWith("=>");

            }
        }
    }

    private static string EscapeMarkup(string data)
    {
        return data.Replace("[", "[[").Replace("]", "]]");
    }

    private string GetUBootVersion(string data)
    {
        var match = Regex.Match(data, @"U-Boot ([0-9.]+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }



    private void RenderEnvironment()
    {
        var table = new Table
        {
            Border = TableBorder.Minimal
        };
        table.AddColumn("Variable");
        table.AddColumn("Value");

        foreach (var (key, value) in _environment)
        {
            table.AddRow(key, value);
        }

        AnsiConsole.Write(table);
    }

    private static Dictionary<string, string> ParseUBootHelp(string rawHelp)
    {
        var commands = new Dictionary<string, string>();
        foreach (var line in rawHelp.Split("\n"))
        {
            var cleanLine = line.Replace("\r", "");
            if (cleanLine.Contains("-"))
            {
                var parts = cleanLine.Split("-");
                commands[parts[0].Trim()] = parts[1].Trim();
            }
        }
        return commands;
    }
}