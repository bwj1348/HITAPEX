using System.Text;

namespace HITAPEX.Models.Usb;

/// <summary>设备上报的预设名称响应（协议 0x21D0）</summary>
/// <remarks>
/// 由于预设名称可能超出单个数据包长度，需要接收方收集多包数据后
/// 按 PacketIndex 排序拼接，再用 UTF-8 解码得到完整名称。
/// </remarks>
public class PresetNameResponse
{
    /// <summary>设备类型</summary>
    public DeviceType DeviceType { get; set; }

    /// <summary>名称数据总长度（字节数）</summary>
    public int TotalLength { get; set; }

    /// <summary>当前数据包的序号（从 0 开始）</summary>
    public int PacketIndex { get; set; }

    /// <summary>当前数据包中的名称片段（原始字节）</summary>
    public byte[] NameData { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// 将多个数据包的名称片段按序号拼接后以 UTF-8 解码，得到完整预设名称。
    /// </summary>
    /// <param name="packets">同一预设名称的所有数据包</param>
    /// <returns>解码后的完整名称字符串，若数据无效则返回空串</returns>
    public static string DecodeNameFromPackets(List<PresetNameResponse> packets)
    {
        packets.Sort((a, b) => a.PacketIndex.CompareTo(b.PacketIndex));
        var totalLen = packets.Count > 0 ? packets[0].TotalLength : 0;
        if (totalLen <= 0) return string.Empty;

        var buffer = new byte[totalLen];
        foreach (var p in packets)
        {
            var offset = p.PacketIndex * 56;
            var len = Math.Min(p.NameData.Length, totalLen - offset);
            if (len > 0)
                Array.Copy(p.NameData, 0, buffer, offset, len);
        }
        return Encoding.UTF8.GetString(buffer);
    }
}
