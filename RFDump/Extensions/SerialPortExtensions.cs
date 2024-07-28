using System.IO.Ports;

namespace RFDump.Extensions
{
    public static class SerialPortExtensions
    {

        public static Task<string> WriteCommand(this SerialPort serialPort, string command)
        {
            return Task.Run(() =>
            {
                serialPort.Write(command);

                var data = string.Empty;
                while (true)
                {
                    var buffer = serialPort.ReadExisting();
                    data += buffer;

                    if (data.Contains("=>"))
                    {
                        data = data[command.Length..^3];
                        break;
                    }
                }
                return data;
            });
        }

        public static Task<string> DumpMemoryBlock(this SerialPort serialPort, uint address, uint length)
        {
            return Task.Run(() =>
            {
                string cmd = $"md.b {address:X} {length:X}\n";
                serialPort.Write(cmd);

                var data = string.Empty;
                while (true)
                {
                    var buffer = serialPort.ReadExisting();
                    data += buffer;

                    if (data.Contains("=>"))
                    {
                        data = data[cmd.Length..^3];
                        break;
                    }
                }
                return data;
            });
        }
    }
}
