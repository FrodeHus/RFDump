namespace RFDump.Bootloader
{
    public interface IBootHandler
    {
        Task Initialize();
        string BootloaderInfo { get; }
        uint BootAddress { get; }
        bool IsReady { get; }
        void HandleBoot(string prelimData);
        (bool success, uint lastKnownGoodAddress, byte[] binaryData) ValidateDumpData(string data, uint startAddress);
    }
}