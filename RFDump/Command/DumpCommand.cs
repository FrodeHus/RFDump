using QuiCLI.Command;

using RFDump.Service;

using Spectre.Console;

namespace RFDump.Command;
public class DumpCommand(SerialService serialService)
{
    private readonly SerialService _serialService = serialService;

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
        AnsiConsole.MarkupLine($"[bold]Dumping firmware from device connected to port [yellow]{port}[/] at [yellow]{baudRate}[/] baud rate to [yellow]{filename}[/] with a chunk size of [yellow]{chunkSize}[/] bytes[/]");
        var result = _serialService.Connect(port, baudRate);

        if (result.IsFailure)
        {
            AnsiConsole.Write(new Markup($"[bold red]{result.Error}[/]"));
        }


        await AnsiConsole.Progress().AutoClear(false).Columns(
            [
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            ]).StartAsync(async ctx =>
        {
            var bootloader = ctx.AddTask("Access boot loader...", true);
            bootloader.IsIndeterminate = true;
            var bootloaderHandler = await _serialService.DetectBootLoader();
            bootloader.Increment(100);

            var gatherInfo = ctx.AddTask("Gathering information...", true);
            await bootloaderHandler.Initialize();
            gatherInfo.Increment(100);

            var address = bootloaderHandler.BootAddress;
            var endAddress = address + 0x1000000;
            var dumpProgress = ctx.AddTask($"Dumping memory [cyan]0x{address:X}[/][yellow]/[/][cyan]0x{endAddress:X}[/]...", true, maxValue: 0x1000000);
            var currentAddress = address;
            await using var file = File.Create(filename);
            while (currentAddress < endAddress)
            {
                if (currentAddress + chunkSize > endAddress)
                {
                    chunkSize = endAddress - currentAddress;
                }

                var dump = await _serialService.DumpMemoryBlock(currentAddress, chunkSize);
                var (success, lastKnownGoodAddress, binaryData) = bootloaderHandler.ValidateDumpData(dump, currentAddress);
                if (!success)
                {
                    var step = chunkSize - ((currentAddress + chunkSize) - lastKnownGoodAddress);
                    currentAddress = lastKnownGoodAddress;
                    dumpProgress.Description = $"Recovery in progress [cyan]0x{currentAddress:X}[/][yellow]/[/][cyan]0x{endAddress:X}[/]...";
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

    private static string EscapeMarkup(string data)
    {
        return data.Replace("[", "[[").Replace("]", "]]");
    }
}