namespace RFDump.Bootloader
{
    internal interface IBootHandler
    {
        uint BootAddress { get; }
        bool IsReady { get; }
        void Dispose();
        void HandleBoot(string prelimData);
    }
}