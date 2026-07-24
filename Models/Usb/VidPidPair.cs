namespace HITAPEX.Models.Usb;

/// <summary>
/// USB 设备的 VID（供应商 ID）和 PID（产品 ID）值对，用于设备识别与匹配。
/// </summary>
public readonly record struct VidPidPair(int Vid, int Pid)
{
    /// <summary>返回格式化的 VID/PID 字符串，如 "VID_FF3F&PID_0002"</summary>
    public override string ToString() => $"VID_{Vid:X4}&PID_{Pid:X4}";
}
