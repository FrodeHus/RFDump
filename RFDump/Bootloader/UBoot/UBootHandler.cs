using System.IO.Ports;
using System.Text.RegularExpressions;

using RFDump.Extensions;

using Spectre.Console;

namespace RFDump.Bootloader.UBoot
{
    internal class UBootHandler(SerialPort serialPort) : IDisposable, IBootHandler
    {
        private readonly SerialPort _serialPort = serialPort;
        private bool _disposedValue;
        private Dictionary<string, string> _environment = new();

        internal uint BootAddress { get; private set; }

        uint IBootHandler.BootAddress => throw new NotImplementedException();

        public bool IsReady { get; private set; }

        internal async Task Initialize()
        {
            var help = await _serialPort.WriteCommand("help\n");
            var commands = ParseUBootHelp(help);

            if (commands.ContainsKey("printenv"))
            {
                var environ = await _serialPort.WriteCommand("printenv\n");
                _environment = ParseKeyValues(environ);
            }
            if (commands.ContainsKey("bdinfo"))
            {
                var bdinfo = await _serialPort.WriteCommand("bdinfo\n");
                var boardInformation = ParseKeyValues(bdinfo);
            }

            BootAddress = DetectAddress();
            if (BootAddress == 0)
            {
                AnsiConsole.MarkupLine("[red]Failed to detect boot address[/]");
                throw new Exception("Failed to detect boot address");
            }

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
        private string GetUBootVersion(string data)
        {
            var match = Regex.Match(data, @"U-Boot ([0-9.]+)");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _serialPort.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async Task HandleBoot(string cleanLine)
        {
            if (cleanLine.StartsWith("Hit any key to stop autoboot"))
            {
                var result = await _serialPort.WriteCommand("\n");
                if (string.IsNullOrEmpty(result))
                {
                    IsReady = true;
                }
            }
        }
    }
}
