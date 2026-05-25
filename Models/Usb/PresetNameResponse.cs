using System.Text;

namespace HITAPEX.Models.Usb;

/// <summary>设备上报的预设名称响应（协议 0x21D0）</summary>
public class PresetNameResponse
{
    public DeviceType DeviceType { get; set; }
    public int TotalLength { get; set; }
    public int PacketIndex { get; set; }
    public byte[] NameData { get; set; } = Array.Empty<byte>();

    /// <summary>NameData 可为单包片段，完整名称需多包拼接后用 UTF-8 解码</summary>
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
