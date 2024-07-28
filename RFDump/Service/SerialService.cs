using System.IO.Ports;

using QuiCLI.Common;

namespace RFDump.Service
{
    public class SerialService
    {
        private SerialPort? _port;
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

        /// <summary>
        /// Retrieve all available serial ports
        /// </summary>
        /// <returns></returns>
        public Result<List<string>> GetPorts() => SerialPort.GetPortNames().ToList();
    }
}
