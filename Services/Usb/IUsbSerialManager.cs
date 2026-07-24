using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

/// <summary>
/// USB 串口管理器接口 —— 管理目标设备的串口发现、连接、断开、数据收发。
/// 与并行的 HID 服务（IHidService）互不干扰，各自管理独立的设备通道。
/// </summary>
/// <remarks>
/// 实现类（UsbSerialManager）通过 UsbDeviceDiscovery 进行设备热插拔监控，
/// 通过 DeviceSerialChannel 管理每个设备的串口读写。
/// </remarks>
public interface IUsbSerialManager : IDisposable
{
    /// <summary>设备首次连接成功时触发</summary>
    event Action<UsbDeviceInfo>? DeviceConnected;

    /// <summary>设备断开连接时触发</summary>
    event Action<UsbDeviceInfo>? DeviceDisconnected;

    /// <summary>从设备收到原始数据时触发（未经协议解析的字节数组）</summary>
    event Action<UsbDeviceInfo, byte[]>? RawDataReceived;

    /// <summary>设备发生错误时触发</summary>
    event Action<UsbDeviceInfo, string>? DeviceError;

    /// <summary>当前已连接的设备信息列表（只快照 State == Connected 的设备）</summary>
    IReadOnlyList<UsbDeviceInfo> ConnectedDevices { get; }

    /// <summary>管理器是否在运行状态</summary>
    bool IsRunning { get; }

    /// <summary>注册一个目标设备（VID/PID），管理器将仅发现和连接匹配的设备</summary>
    void RegisterTargetDevice(VidPidPair pair);

    /// <summary>批量注册目标设备</summary>
    void RegisterTargetDevices(IEnumerable<VidPidPair> pairs);

    /// <summary>注销一个目标设备，管理器将不再发现该 VID/PID 的设备</summary>
    void UnregisterTargetDevice(VidPidPair pair);

    /// <summary>获取当前注册的所有目标设备 VID/PID 列表</summary>
    IReadOnlyCollection<VidPidPair> GetRegisteredDevices();

    /// <summary>启动管理器（开始设备发现和热插拔监控）</summary>
    void Start();

    /// <summary>停止管理器（断开所有设备连接，停止监控）</summary>
    void Stop();

    /// <summary>连接到指定的设备（通常由内部发现后自动调用，也可手动触发）</summary>
    bool ConnectDevice(UsbDeviceInfo deviceInfo);

    /// <summary>断开指定设备</summary>
    void DisconnectDevice(UsbDeviceInfo deviceInfo);

    /// <summary>断开所有已连接的设备</summary>
    void DisconnectAll();

    /// <summary>
    /// 向指定设备发送原始字节数据。
    /// </summary>
    /// <param name="deviceKey">设备唯一标识（格式：VID:PID:SerialNumber:PortName）</param>
    /// <param name="data">要发送的字节数组</param>
    /// <returns>是否发送成功</returns>
    bool SendToDevice(string deviceKey, byte[] data);
}
