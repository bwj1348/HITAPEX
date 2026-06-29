using System.Diagnostics;

namespace HITAPEX.Services;

/// <summary>
/// 将 TelemetrySDK 的 NormalizedData 转换为 USB 协议遥测数据包（0x6101/0x6102/0x6103）。
/// 协议定义参考：docs/乘游直驱方向盘与PC软件usb通信协议 v0.1.md 第8节"遥测数据"。
/// </summary>
public static class TelemetryPacketBuilder
{
    private const int FrameSize = 64;

    /// <summary>遥测数据包类型</summary>
    private static class PacketType
    {
        public const ushort VehicleInfo   = 0x6101;  // 车辆信息包1
        public const ushort BrakeSusp     = 0x6102;  // 刹车/悬挂信息包2
        public const ushort RaceSpeed     = 0x6103;  // 比赛/车速信息包3
    }

    /// <summary>速度单位常量</summary>
    public static class SpeedUnit
    {
        public const byte Ms   = 0;  // m/s
        public const byte Kph  = 1;  // km/h
        public const byte Mph  = 2;  // mph
    }

    /// <summary>挡位常量</summary>
    public static class GearValue
    {
        public const byte Reverse = 0xFF;  // 倒挡
        public const byte Neutral = 0;     // 空挡
        public const byte MaxForward = 100; // 前进挡上限
    }

    // ════════════════════════════════════════════════════════════════
    //  包1: 车辆信息 (0x6101)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建车辆信息数据包（包1，0x6101）。
    /// 包含：速度、转速、档位、燃油/电池百分比、转速灯、轮胎温度/胎压。
    /// </summary>
    /// <param name="data">归一化遥测数据</param>
    /// <param name="timestampMs">模拟时间戳（毫秒）</param>
    /// <param name="rpmLightPercent">转速灯显示百分比 0-100（调用方根据当前RPM和maxRPM计算）</param>
    public static byte[] BuildVehicleInfoPacket(
        TelemetryAPI.NormalizedData data,
        uint timestampMs,
        byte rpmLightPercent)
    {
        var frame = new byte[FrameSize];

        // ID
        frame[0] = 0x61;

        // 包类型 (0x6101)，小端序
        frame[1] = (byte)(PacketType.VehicleInfo & 0xFF);
        frame[2] = (byte)((PacketType.VehicleInfo >> 8) & 0xFF);

        // 模拟时间戳 (uint32 LE)
        frame[3] = (byte)(timestampMs & 0xFF);
        frame[4] = (byte)((timestampMs >> 8) & 0xFF);
        frame[5] = (byte)((timestampMs >> 16) & 0xFF);
        frame[6] = (byte)((timestampMs >> 24) & 0xFF);

        // 速度单位: NormalizedData.speed 固定为 km/h，所以用 1 (kph)
        frame[7] = SpeedUnit.Kph;

        // 最大转速 (uint16 LE)，限制范围 0-0xFFFF
        var maxRpm = (ushort)Math.Clamp(data.maxRpm, 0f, 65535f);
        frame[8] = (byte)(maxRpm & 0xFF);
        frame[9] = (byte)((maxRpm >> 8) & 0xFF);

        // 当前转速 (uint16 LE)
        var currentRpm = (ushort)Math.Clamp(data.rpm, 0f, 65535f);
        frame[10] = (byte)(currentRpm & 0xFF);
        frame[11] = (byte)((currentRpm >> 8) & 0xFF);

        // 车速 (float32 LE)，单位 kph
        BitConverter.TryWriteBytes(frame.AsSpan(12), data.speed);

        // 档位: 0=N, 1-100=前进挡, 0xFF=倒挡
        frame[16] = GearFromNormalized(data.gear);

        // 燃油百分比 (0-100)
        var fuelPct = HasFlag(data, TelemetryAPI.ValidFlags.FuelPct) && data.fuelRemainingPct >= 0
            ? (byte)Math.Clamp((int)(data.fuelRemainingPct * 100f + 0.5f), 0, 100)
            : (byte)0;
        frame[17] = fuelPct;

        // 电池电量百分比 (0-100)，从 ERS 电量获取
        var batteryPct = HasFlag(data, TelemetryAPI.ValidFlags.ErsCharge) && data.ersCharge >= 0
            ? (byte)Math.Clamp((int)(data.ersCharge * 100f + 0.5f), 0, 100)
            : (byte)0;
        frame[18] = batteryPct;

        // 转速灯显示百分比 (0-100)
        frame[19] = rpmLightPercent;

        // 轮胎温度和胎压（目前 NormalizedData 不包含轮胎数据，填 0）
        // 左前轮胎温度 (float32) — offset 20-23
        // 右前轮胎温度 (float32) — offset 24-27
        // 左后轮胎温度 (float32) — offset 28-31
        // 右后轮胎温度 (float32) — offset 32-35
        // 这些字段 TelemetrySDK 暂不提供，保留为 0

        // 左前轮胎胎压 (float32) — offset 36-39
        // 右前轮胎胎压 (float32) — offset 40-43
        // 左后轮胎胎压 (float32) — offset 44-47
        // 右后轮胎胎压 (float32) — offset 48-51
        // 这些字段 TelemetrySDK 暂不提供，保留为 0

        // offset 52-63: 保留

        return frame;
    }

    // ════════════════════════════════════════════════════════════════
    //  包2: 刹车/悬挂信息 (0x6102)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建刹车/悬挂信息数据包（包2，0x6102）。
    /// 包含：刹车温度、悬挂位置、赛车姿态、ABS 状态。
    /// </summary>
    /// <param name="data">归一化遥测数据</param>
    /// <param name="timestampMs">模拟时间戳（毫秒）</param>
    /// <param name="absActive">ABS 激活状态（从 NormalizedData 获取）</param>
    public static byte[] BuildBrakeSuspensionPacket(
        TelemetryAPI.NormalizedData data,
        uint timestampMs)
    {
        var frame = new byte[FrameSize];

        // ID
        frame[0] = 0x61;

        // 包类型 (0x6102)，小端序
        frame[1] = (byte)(PacketType.BrakeSusp & 0xFF);
        frame[2] = (byte)((PacketType.BrakeSusp >> 8) & 0xFF);

        // 模拟时间戳 (uint32 LE)
        frame[3] = (byte)(timestampMs & 0xFF);
        frame[4] = (byte)((timestampMs >> 8) & 0xFF);
        frame[5] = (byte)((timestampMs >> 16) & 0xFF);
        frame[6] = (byte)((timestampMs >> 24) & 0xFF);

        // 刹车温度 (offset 7-22): TelemetrySDK 暂不提供，保留为 0
        // 悬挂位置 (offset 23-38): TelemetrySDK 暂不提供，保留为 0
        // 赛车姿态 (offset 39-50): TelemetrySDK 暂不提供，保留为 0

        // ABS 状态 (offset 51): 0=未触发, 1=触发
        var absTriggered = HasFlag(data, TelemetryAPI.ValidFlags.AbsActive) && data.isAbsActive;
        frame[51] = absTriggered ? (byte)1 : (byte)0;

        // offset 52-63: 保留

        return frame;
    }

    // ════════════════════════════════════════════════════════════════
    //  包3: 比赛/车速信息 (0x6103)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建比赛/车速信息数据包（包3，0x6103）。
    /// 包含：圈数/排名、圈速、速度向量、加速度向量。
    /// </summary>
    /// <param name="data">归一化遥测数据</param>
    /// <param name="timestampMs">模拟时间戳（毫秒）</param>
    public static byte[] BuildRaceSpeedPacket(
        TelemetryAPI.NormalizedData data,
        uint timestampMs)
    {
        var frame = new byte[FrameSize];

        // ID
        frame[0] = 0x61;

        // 包类型 (0x6103)，小端序
        frame[1] = (byte)(PacketType.RaceSpeed & 0xFF);
        frame[2] = (byte)((PacketType.RaceSpeed >> 8) & 0xFF);

        // 模拟时间戳 (uint32 LE)
        frame[3] = (byte)(timestampMs & 0xFF);
        frame[4] = (byte)((timestampMs >> 8) & 0xFF);
        frame[5] = (byte)((timestampMs >> 16) & 0xFF);
        frame[6] = (byte)((timestampMs >> 24) & 0xFF);

        // 圈数/排名 (offset 7-14): TelemetrySDK 暂不提供，保留为 0
        // Normalized Driving Line (offset 14): 保留为 0
        // 圈速 (offset 15-26): 保留为 0

        // 赛车速度向量 (offset 27-38): 保留为 0（TelemetrySDK 暂不提供 3D 速度分量）
        // 加速度向量 (offset 39-50): 保留为 0

        // offset 51-63: 保留

        return frame;
    }

    // ════════════════════════════════════════════════════════════════
    //  批量构建
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 从 NormalizedData 构建完整的遥测数据三包。
    /// </summary>
    /// <param name="data">归一化遥测数据</param>
    /// <param name="timestampMs">模拟时间戳（毫秒），传入 0 则使用 Environment.TickCount</param>
    /// <param name="rpmLightPercent">转速灯显示百分比 0-100，传入 null 则自动计算</param>
    /// <returns>三个遥测数据包（索引 0=0x6101, 1=0x6102, 2=0x6103）</returns>
    public static byte[][] BuildAllPackets(
        TelemetryAPI.NormalizedData data,
        uint? timestampMs = null,
        byte? rpmLightPercent = null)
    {
        var timestamp = timestampMs ?? (uint)Environment.TickCount;
        var rpmLight = rpmLightPercent ?? CalculateRpmLightPercent(data);

        return new[]
        {
            BuildVehicleInfoPacket(data, timestamp, rpmLight),
            BuildBrakeSuspensionPacket(data, timestamp),
            BuildRaceSpeedPacket(data, timestamp),
        };
    }

    // ════════════════════════════════════════════════════════════════
    //  辅助方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>根据当前 RPM 和最大 RPM 计算转速灯百分比 (0-100)</summary>
    public static byte CalculateRpmLightPercent(TelemetryAPI.NormalizedData data)
    {
        if (!HasFlag(data, TelemetryAPI.ValidFlags.Rpm) || data.rpm <= 0)
            return 0;

        if (HasFlag(data, TelemetryAPI.ValidFlags.MaxRpm) && data.maxRpm > 0)
        {
            var pct = (data.rpm / data.maxRpm) * 100f;
            return (byte)Math.Clamp((int)(pct + 0.5f), 0, 100);
        }

        // 无 maxRpm 时，以 8000 RPM 为满载参考
        var fallbackPct = (data.rpm / 8000f) * 100f;
        return (byte)Math.Clamp((int)(fallbackPct + 0.5f), 0, 100);
    }

    /// <summary>将 NormalizedData.gear 转换为协议挡位字节</summary>
    private static byte GearFromNormalized(int gear)
    {
        return gear switch
        {
            -1 => GearValue.Reverse,                 // 倒挡 → 0xFF
            0  => GearValue.Neutral,                 // 空挡 → 0x00
            >= 1 and <= 100 => (byte)gear,           // 前进挡 1-100 → 直接映射
            > 100 => GearValue.MaxForward,           // 超过 100 → 截断为 100
            _ => GearValue.Neutral                   // 未知 → 空挡
        };
    }

    private static bool HasFlag(TelemetryAPI.NormalizedData data, ulong flag) =>
        (data.validFlags & flag) != 0;
}
