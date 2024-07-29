using System.IO.Ports;

using RFDump.Bootloader.UBoot;
using RFDump.Service;

namespace RFDump.Bootloader
{
    internal static class Detector
    {
        public static IBootHandler? DetectBootloader(string data, SerialService serialService)
        {
            if (data.Contains("U-Boot"))
            {
                return new UBootHandler(serialService);
            }
            return null;
        }
    }
}
