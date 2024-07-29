using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuiCLI.Command;
using RFDump.Service;
using Spectre.Console;

namespace RFDump.Command
{
    public class PortCommand(SerialService serialService)
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
    }
}
