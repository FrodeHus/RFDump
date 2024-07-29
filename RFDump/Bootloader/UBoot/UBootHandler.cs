using System.Text.RegularExpressions;

using RFDump.Service;

using Spectre.Console;

namespace RFDump.Bootloader.UBoot
{
    internal partial class UBootHandler(SerialService serialService) : IBootHandler
    {
        private readonly SerialService _serialService = serialService;
        private readonly Regex _dataRegex = DataLineMatcher();
        private Dictionary<string, string> _environment = new();

        public uint BootAddress { get; private set; }

        public string BootloaderInfo { get; private set; } = "U-Boot";

        public bool IsReady { get; private set; }

        public async Task Initialize()
        {
            var help = await _serialService.Execute("help\n");
            var commands = ParseUBootHelp(help);

            if (commands.ContainsKey("printenv"))
            {
                var environ = await _serialService.Execute("printenv\n");
                _environment = ParseKeyValues(environ);
            }
            if (commands.ContainsKey("bdinfo"))
            {
                var bdinfo = await _serialService.Execute("bdinfo\n");
                var boardInformation = ParseKeyValues(bdinfo);
            }

            if (commands.ContainsKey("version"))
            {
                var version = await _serialService.Execute("version\n");
                BootloaderInfo = $"U-Boot {GetUBootVersion(version)}";
            }

            BootAddress = DetectAddress();
            if (BootAddress == 0)
            {
                AnsiConsole.MarkupLine("[red]Failed to detect boot address[/]");
                throw new Exception("Failed to detect boot address");
            }

        }

        public (bool success, uint lastKnownGoodAddress, byte[] binaryData) ValidateDumpData(string data, uint startAddress)
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


        [GeneratedRegex(@"(?<address>[a-f0-9]+): (?<bytes>[a-z0-9\s]+)    (?<ascii>.*)")]
        private static partial Regex DataLineMatcher();

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

        public async Task HandleBoot(string prelimData)
        {
            await CheckForAutobootInterrupt(prelimData);
        }

        private async Task CheckForAutobootInterrupt(string? data = null)
        {
            if (data == null)
            {
                data = _serialService.ReadAvailableData();
            }

            if (data.Contains("Hit any key to stop autoboot"))
            {
                var result = await _serialService.SendKey(ConsoleKey.Enter);
                if (result.Contains("=>"))
                {
                    IsReady = true;
                    return;
                }
            }

            await Task.Delay(100);
            await CheckForAutobootInterrupt();
        }
    }
}
