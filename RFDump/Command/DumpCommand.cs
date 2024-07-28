using QuiCLI.Command;

using RFDump.Service;

using Spectre.Console;

namespace RFDump.Command;
public class DumpCommand(SerialService serialService)
{
    private readonly SerialService _serialService = serialService;

    [Command("dump")]
    public async Task Dump(string port)
    {
        var result = _serialService.Connect(port);
        if (result.IsFailure)
        {
            AnsiConsole.Write(new Markup($"[bold red]{result.Error}[/]"));
        }
        await AnsiConsole.Status().StartAsync("Dumping data", async ctx =>
        {
        });
    }

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
}