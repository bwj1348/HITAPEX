namespace HITAPEX.Models.Usb;

public class DeviceInfoResponse
{
    public DeviceType DeviceType { get; set; }
    public int UsbSpeed { get; set; }
    public int NormalFirmwareVersion { get; set; }
    public int BootFirmwareVersion { get; set; }

    public string VersionString => $"v{NormalFirmwareVersion >> 8}.{NormalFirmwareVersion & 0xFF}";

    public override string ToString()
        => $"DeviceInfo: Type={DeviceType}, USB={UsbSpeed}, FW={VersionString}, BootFW=v{BootFirmwareVersion >> 8}.{BootFirmwareVersion & 0xFF}";
}
