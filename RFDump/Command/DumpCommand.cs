using QuiCLI.Command;
using RFDump.Service;
using Spectre.Console;
namespace RFDump.Command;
public class DumpCommand(SerialService serialService)
{
    private readonly SerialService _serialService = serialService;

    [Command("dump")]
    public string Dump(string port)
    {
        AnsiConsole.Write(new Markup( $"Connecting to [bold yellow]{port}[/]"));
        return "Hello World!";
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