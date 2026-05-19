namespace HITAPEX.Models.Usb;

/// <summary>
/// 描述一个 USB 设备的身份信息：VID/PID（正常模式和更新模式）、设备类型、型号名称。
/// </summary>
public class DeviceDescriptor
{
    /// <summary>设备型号名称（如 "A1踏板"）</summary>
    public string ModelName { get; init; } = "";

    /// <summary>设备类型</summary>
    public DeviceType DeviceType { get; init; }

    /// <summary>正常模式下的 VID/PID</summary>
    public VidPidPair NormalMode { get; init; }

    /// <summary>更新模式下的 VID/PID</summary>
    public VidPidPair UpdateMode { get; init; }

    /// <summary>根据 VID/PID 判断是否为该设备的正常模式</summary>
    public bool IsNormalMode(int vid, int pid) =>
        vid == NormalMode.Vid && pid == NormalMode.Pid;

    /// <summary>根据 VID/PID 判断是否为该设备的更新模式</summary>
    public bool IsUpdateMode(int vid, int pid) =>
        vid == UpdateMode.Vid && pid == UpdateMode.Pid;

    /// <summary>根据 VID/PID 判断是否匹配该设备（任一模式）</summary>
    public bool Matches(int vid, int pid) =>
        IsNormalMode(vid, pid) || IsUpdateMode(vid, pid);
}
