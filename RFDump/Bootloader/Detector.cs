using System.IO.Ports;

using RFDump.Bootloader.UBoot;

namespace RFDump.Bootloader
{
    internal static class Detector
    {
        public static IBootHandler? DetectBootloader(string data, SerialPort serialPort)
        {
            if (data.Contains("U-Boot"))
            {
                return new UBootHandler(serialPort);
            }
            return null;
        }
    }
}
