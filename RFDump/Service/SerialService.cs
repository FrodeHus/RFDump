using System.IO.Ports;
using System.Net.Http.Headers;
using System.Security.Cryptography;

using QuiCLI.Common;

using RFDump.Bootloader;
using RFDump.Extensions;

namespace RFDump.Service
{
    public class SerialService
    {
        private SerialPort? _port;
        public event EventHandler<string>? OnDataReceived;
        public Result<SerialPort> Connect(string port, int baudRate = 115200)
        {
            _port = new SerialPort(port, baudRate, Parity.None, 8, StopBits.One);
            try
            {
                _port.Open();

            }
            catch (Exception e)
            {
                return Result<SerialPort>.Failure(new Error(ErrorCode.InvalidArgument, "Failed opening port", e));
            }
            return Result<SerialPort>.Success(_port);
        }

        public void Disconnect()
        {
            _port?.Close();
        }

        public Result<bool> IsConnected()
        {
            return _port?.IsOpen ?? false;
        }

        public async Task<string> Execute(string command)
        {
            return _port == null ? throw new InvalidOperationException("Serial port not connected") : await _port.WriteCommand(command);
        }

        public string ReadAvailableData()
        {
            return _port == null ? throw new InvalidOperationException("Serial port not connected") : _port.ReadExisting();
        }

        public void SendKey(ConsoleKey key)
        {
            SendKey((char)key);
        }

        public void SendKey(char key)
        {
            _port?.SendKey(key);
        }

        public async Task<IBootHandler> DetectBootLoader()
        {
            var start = DateTime.Now;
            while (true)
            {
                if (DateTime.Now - start > TimeSpan.FromSeconds(30))
                {
                    throw new Exception("Failed to detect bootloader");
                }

                var data = ReadAvailableData();
                var handler = Detector.DetectBootloader(data, this);
                if (handler != null)
                {
                    return handler;
                }
                await Task.Delay(100);
            }
        }

        public Task<string> DumpMemoryBlock(uint address, uint length)
        {
            return _port == null ? throw new InvalidOperationException("Serial port not connected") : _port.DumpMemoryBlock(address, length);
        }


        /// <summary>
        /// Retrieve all available serial ports
        /// </summary>
        /// <returns></returns>
        public Result<List<string>> GetPorts() => SerialPort.GetPortNames().ToList();
    }
}
