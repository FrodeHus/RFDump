using System.IO.Ports;

using RFDump.Service;

using Spectre.Console;

namespace RFDump;

public record SerialConfiguration(string Port, int BaudRate, int DataBits, Parity Parity, StopBits StopBits);
internal static class Configure
{
    public static SerialConfiguration AskForSerialSettings(SerialService serialService)
    {
        var ports = serialService.GetPorts();
        if (ports.IsFailure || ports.Value.Count == 0)
        {
            AnsiConsole.Write(new Markup("[bold red]No ports detected - is the device plugged in?[/]"));
            Environment.Exit(1);
        }

        var port = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select port ")
            .AddChoices(ports.Value));

        var baudRate = AnsiConsole.Prompt(new TextPrompt<int>("Enter baud rate")
            .DefaultValue(115200));

        var showAdvancedConfig = AnsiConsole.Confirm("Show advanced configuration?");
        if (showAdvancedConfig)
        {
            var dataBits = AnsiConsole.Prompt(new TextPrompt<int>("Enter data bits")
                .DefaultValue(8));

            var parity = AnsiConsole.Prompt(new SelectionPrompt<Parity>().Title("Select parity")
                .AddChoices(Parity.None, Parity.Odd, Parity.Even));

            var stopBits = AnsiConsole.Prompt(new SelectionPrompt<StopBits>().Title("Select stop bits")
                .AddChoices(StopBits.One, StopBits.Two));

            return new SerialConfiguration(port, baudRate, dataBits, parity, stopBits);
        }
        return new SerialConfiguration(port, baudRate, 8, Parity.None, StopBits.One);
    }
}
