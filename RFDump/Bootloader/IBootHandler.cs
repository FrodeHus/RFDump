namespace RFDump.Bootloader
{
    internal interface IBootHandler
    {
        string BootloaderInfo { get; }
        uint BootAddress { get; }
        bool IsReady { get; }
        void Dispose();
        void HandleBoot(string prelimData);
    }
}