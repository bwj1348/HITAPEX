using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

/// <summary>
/// HID 设备服务接口，负责 HID 设备的发现、连接、数据读取和解析。
/// 与串口连接（IUsbSerialManager）并行运行，互不干扰。
/// </summary>
public interface IHidService : IDisposable
{
    /// <summary>已连接的 HID 设备信息列表</summary>
    IReadOnlyList<UsbDeviceInfo> ConnectedHidDevices { get; }

    /// <summary>HID 设备连接时触发</summary>
    event Action<UsbDeviceInfo>? HDeviceConnected;

    /// <summary>HID 设备断开时触发</summary>
    event Action<UsbDeviceInfo>? HDeviceDisconnected;

    /// <summary>解码后的踏板 HID 数据到达时触发</summary>
    event Action<UsbDeviceInfo, HidPedalData>? PedalDataReceived;

    /// <summary>解码后的基座 HID 数据到达时触发</summary>
    event Action<UsbDeviceInfo, HidBaseData>? BaseDataReceived;

    /// <summary>解码后的面盘 HID 数据到达时触发（面盘直连 USB 时）</summary>
    event Action<UsbDeviceInfo, HidWheelData>? WheelDataReceived;

    bool IsRunning { get; }

    /// <summary>启动 HID 设备发现与数据读取</summary>
    void Start();

    /// <summary>停止所有 HID 设备读取</summary>
    void Stop();
}
