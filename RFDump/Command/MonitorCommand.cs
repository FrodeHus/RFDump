using System.IO.Ports;

using RFDump.Service;

using Spectre.Console;

namespace RFDump.Command;

internal class MonitorCommand(SerialService serialService)
{
    private readonly SerialService _serialService = serialService;
    public void Monitor(string port, int baudRate = 115200)
    {
        var config = new SerialConfiguration(port, baudRate, 8, Parity.None, StopBits.One);
        var result = _serialService.Connect(config);
        if (result.IsFailure)
        {
            AnsiConsole.Write(new Markup($"[bold red]{result.Error}[/]"));
        }

        var serialPort = result.Value;
        var data = string.Empty;
        AnsiConsole.MarkupLine("[bold]Monitoring serial port...[/]");
        while (true)
        {
            var buffer = serialPort.ReadExisting();
            data += buffer;
            AnsiConsole.Write(buffer);
        }

    }

}
