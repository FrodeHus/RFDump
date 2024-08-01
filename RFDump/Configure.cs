using System.IO.Ports;
using System.Reflection;
using System.Resources;

using RFDump.Service;

using Spectre.Console;

namespace RFDump;

public record SerialConfiguration(string Port, int BaudRate, int DataBits, Parity Parity, StopBits StopBits);
public record DumpConfiguration(string Filename, SerialConfiguration SerialConfiguration);
internal static class Configure
{
    public static DumpConfiguration AskForDumpConfiguration(SerialService serialService)
    {
        var font = GetFigletFont("miniFont");
        AnsiConsole.Write(new FigletText(font, "RFDump"));
        AnsiConsole.MarkupLine("[cyan]Firmware dump configuration[/]");
        var filename = AnsiConsole.Prompt(new TextPrompt<string>("Enter filename for dump")
            .DefaultValue("firmware.bin"));
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

            return new DumpConfiguration(filename, new SerialConfiguration(port, baudRate, dataBits, parity, stopBits));
        }
        return new DumpConfiguration(filename, new SerialConfiguration(port, baudRate, 8, Parity.None, StopBits.One));
    }

    private static FigletFont GetFigletFont(string name)
    {
        var font = RFDump.FigletFont;

        return FigletFont.Parse(font);
    }

}
