namespace RFDump.Bootloader
{
    internal interface IBootHandler
    {
        uint BootAddress { get; }
        bool IsReady { get; }
        void Dispose();
        Task HandleBoot(string cleanLine);
    }
}