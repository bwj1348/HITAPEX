namespace HITAPEX.Services;

/// <summary>
/// 将 TelemetrySDK v1.0.0 NormalizedData 转换为 USB 协议遥测数据包（0x6101~0x6103）。
/// 协议定义参考：docs/乘游直驱方向盘与PC软件usb通信协议 v0.1.md 第8节"遥测数据"。
/// 轮胎/刹车数组统一：[0]=FL, [1]=FR, [2]=RL, [3]=RR。
/// </summary>
public static class TelemetryPacketBuilder
{
    private const int FrameSize = 64;

    /// <summary>遥测数据包类型</summary>
    private static class PacketType
    {
        public const ushort VehicleInfo1 = 0x6101;  // 车辆信息包1（基础驾驶参数）
        public const ushort VehicleInfo2 = 0x6102;  // 车辆信息包2（胎面内/中/外温度 + 前轮刹车温度）
        public const ushort VehicleInfo3 = 0x6103;  // 车辆信息包3（胎核温度 + 胎压 + 胎磨损 + 后轮刹车温度 + 发动机模式）
    }

    /// <summary>协议挡位常量（0=N, 1-100=前进挡, 0xFF=R1, 0xFE=R2, 0xFD=R3, 0xFC=R4）</summary>
    public static class GearValue
    {
        public const byte Reverse1 = 0xFF;
        public const byte Reverse2 = 0xFE;
        public const byte Reverse3 = 0xFD;
        public const byte Reverse4 = 0xFC;
        public const byte Neutral = 0;
    }

    // ════════════════════════════════════════════════════════════════
    //  通用帮助方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>写入包头：ID(0x61) + 包类型(uint16 LE) + 时间戳(uint32 LE)</summary>
    private static void WriteHeader(byte[] frame, ushort packetType, uint timestampMs)
    {
        frame[0] = 0x61;
        frame[1] = (byte)(packetType & 0xFF);
        frame[2] = (byte)((packetType >> 8) & 0xFF);
        frame[3] = (byte)(timestampMs & 0xFF);
        frame[4] = (byte)((timestampMs >> 8) & 0xFF);
        frame[5] = (byte)((timestampMs >> 16) & 0xFF);
        frame[6] = (byte)((timestampMs >> 24) & 0xFF);
    }

    private static void WriteUInt16(byte[] frame, int offset, ushort value)
    {
        frame[offset] = (byte)(value & 0xFF);
        frame[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static bool HasFlag(TelemetryAPI.NormalizedData data, ulong flag) =>
        (data.validFlags & flag) != 0;

    // ════════════════════════════════════════════════════════════════
    //  包1: 车辆信息 (0x6101) — 基础驾驶参数
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建车辆信息数据包1（0x6101）。
    /// 包含：车速、最大/当前转速、档位、油门/刹车/离合、排名、圈速、维修区限速器/TC/ABS/TC cut/DRS、
    /// TC/ABS/TC cut 档位、车轮抱死/打滑位、旗语（低11位）、ERS 模式/电量/回收、涡轮压力。
    /// </summary>
    public static byte[] BuildVehicleInfo1Packet(
        TelemetryAPI.NormalizedData data, uint timestampMs)
    {
        var frame = new byte[FrameSize];
        WriteHeader(frame, PacketType.VehicleInfo1, timestampMs);

        // 车速 (float32 LE, km/h) — offset 7-10
        if (HasFlag(data, TelemetryAPI.ValidFlags.Speed))
            BitConverter.TryWriteBytes(frame.AsSpan(7), data.speed);

        // 最大转速 (uint16 LE) — offset 11-12
        // 不检查 ValidFlags.MaxRpm：LFS/RBR/BeamNG 的 SDK 可能不置位该位，
        // 但自适应 maxRpm 追踪（TelemetryService.ApplyAdaptiveMaxRpm）会填充 data.maxRpm。
        // 以数值 > 0 为写入条件：SDK 提供的有效值 / 自适应追踪值均可写入，避免写入残留垃圾。
        if (data.maxRpm > 0)
            WriteUInt16(frame, 11, (ushort)Math.Clamp(data.maxRpm, 0f, 65535f));

        // 当前转速 (uint16 LE) — offset 13-14
        if (HasFlag(data, TelemetryAPI.ValidFlags.Rpm))
            WriteUInt16(frame, 13, (ushort)Math.Clamp(data.rpm, 0f, 65535f));

        // 档位 — offset 15
        // 协议：0=N, 1-100=前进挡, 0xFF=R1, 0xFE=R2, 0xFD=R3, 0xFC=R4（欧卡多倒挡）
        if (HasFlag(data, TelemetryAPI.ValidFlags.Gear))
            frame[15] = GearFromNormalized(data.gear);

        // 油门行程 (float32 LE) — offset 16-19
        if (HasFlag(data, TelemetryAPI.ValidFlags.Throttle))
            BitConverter.TryWriteBytes(frame.AsSpan(16), data.throttle);

        // 刹车行程 (float32 LE) — offset 20-23
        if (HasFlag(data, TelemetryAPI.ValidFlags.Brake))
            BitConverter.TryWriteBytes(frame.AsSpan(20), data.brake);

        // 离合行程 (float32 LE) — offset 24-27
        if (HasFlag(data, TelemetryAPI.ValidFlags.Clutch))
            BitConverter.TryWriteBytes(frame.AsSpan(24), data.clutch);

        // 排名 — offset 28
        if (HasFlag(data, TelemetryAPI.ValidFlags.Position))
            frame[28] = (byte)Math.Clamp(data.position, 0, 255);

        // 当前圈速 (float32 LE, s) — offset 29-32
        if (HasFlag(data, TelemetryAPI.ValidFlags.CurrentLap))
            BitConverter.TryWriteBytes(frame.AsSpan(29), data.currentLapTime);

        // 上一圈速 (float32 LE, s) — offset 33-36
        if (HasFlag(data, TelemetryAPI.ValidFlags.LastLap))
            BitConverter.TryWriteBytes(frame.AsSpan(33), data.lastLapTime);

        // 最快圈速 (float32 LE, s) — offset 37-40
        if (HasFlag(data, TelemetryAPI.ValidFlags.BestLap))
            BitConverter.TryWriteBytes(frame.AsSpan(37), data.bestLapTime);

        // 状态标志 (bool → byte) — offset 41-46
        if (HasFlag(data, TelemetryAPI.ValidFlags.PitLimiter))
            frame[41] = data.isPitLimiterActive ? (byte)1 : (byte)0;
        if (HasFlag(data, TelemetryAPI.ValidFlags.TcActive))
            frame[42] = data.isTcActive ? (byte)1 : (byte)0;
        if (HasFlag(data, TelemetryAPI.ValidFlags.AbsActive))
            frame[43] = data.isAbsActive ? (byte)1 : (byte)0;
        // TC cut 激活状态：NormalizedData 仅提供 tcCutLevel（0=关），>0 即视为激活
        if (HasFlag(data, TelemetryAPI.ValidFlags.TcCut))
            frame[44] = data.tcCutLevel > 0 ? (byte)1 : (byte)0;
        if (HasFlag(data, TelemetryAPI.ValidFlags.DrsAvailable))
            frame[45] = data.isDrsAvailable ? (byte)1 : (byte)0;
        if (HasFlag(data, TelemetryAPI.ValidFlags.DrsActive))
            frame[46] = data.isDrsActive ? (byte)1 : (byte)0;

        // TC / ABS / TC cut 档位 — offset 47-49
        if (HasFlag(data, TelemetryAPI.ValidFlags.TcLevel))
            frame[47] = (byte)Math.Clamp(data.tcLevel, 0, 255);
        if (HasFlag(data, TelemetryAPI.ValidFlags.AbsLevel))
            frame[48] = (byte)Math.Clamp(data.absLevel, 0, 255);
        if (HasFlag(data, TelemetryAPI.ValidFlags.TcCut))
            frame[49] = (byte)Math.Clamp(data.tcCutLevel, 0, 255);

        // 车轮抱死 — offset 50（bit0=FL, bit1=FR, bit2=RL, bit3=RR，1=抱死）
        if (HasFlag(data, TelemetryAPI.ValidFlags.WheelLocked) && data.isWheelLocked != null)
        {
            byte bits = 0;
            for (int i = 0; i < 4 && i < data.isWheelLocked.Length; i++)
                if (data.isWheelLocked[i]) bits |= (byte)(1 << i);
            frame[50] = bits;
        }

        // 车轮打滑 — offset 51（bit0-3 逐胎标记，1=打滑）
        if (HasFlag(data, TelemetryAPI.ValidFlags.WheelSlipping) && data.isWheelSlipping != null)
        {
            byte bits = 0;
            for (int i = 0; i < 4 && i < data.isWheelSlipping.Length; i++)
                if (data.isWheelSlipping[i]) bits |= (byte)(1 << i);
            frame[51] = bits;
        }

        // 旗语 — offset 52-53（uint16 LE，低 11 位逐位标记，1=激活）
        // 位序（协议固定）：黄 → 绿 → 蓝 → 白 → 黑 → 方格 → 处罚 → 肉丸(橙) → 红 → 安全车 → 虚拟安全车
        if (HasFlag(data, TelemetryAPI.ValidFlags.RaceFlag))
            WriteUInt16(frame, 52, PackRaceFlags(data));

        // ERS 模式 — offset 54
        if (HasFlag(data, TelemetryAPI.ValidFlags.ErsDeploy))
            frame[54] = (byte)Math.Clamp(data.ersDeployMode, 0, 255);

        // ERS 电量 (float32 LE, 0.0-1.0) — offset 55-58
        if (HasFlag(data, TelemetryAPI.ValidFlags.ErsCharge))
            BitConverter.TryWriteBytes(frame.AsSpan(55), data.ersCharge);

        // ERS 回收级别（百分比，0-100） — offset 59
        if (HasFlag(data, TelemetryAPI.ValidFlags.ErsRecovery))
            frame[59] = (byte)Math.Clamp(data.ersRecoveryLevel, 0, 100);

        // 涡轮压力 (float32 LE, bar) — offset 60-63
        if (HasFlag(data, TelemetryAPI.ValidFlags.TurboPressure))
            BitConverter.TryWriteBytes(frame.AsSpan(60), data.turboPressure);

        return frame;
    }

    // ════════════════════════════════════════════════════════════════
    //  包2: 车辆信息 (0x6102) — 胎面温度三层 + 前轮刹车温度
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建车辆信息数据包2（0x6102）。
    /// 包含：四轮胎面内侧/中间/外侧温度（12 个 float）、左前/右前刹车温度。
    /// 协议顺序：FL, FR, RL, RR。
    /// </summary>
    public static byte[] BuildVehicleInfo2Packet(
        TelemetryAPI.NormalizedData data, uint timestampMs)
    {
        var frame = new byte[FrameSize];
        WriteHeader(frame, PacketType.VehicleInfo2, timestampMs);

        // 四轮胎面内侧温度 (float32 × 4) — offset 7-22
        WriteTyreArray(frame, 7, data.tyreTempInner, TelemetryAPI.ValidFlags.TyreTempInner, data);

        // 四轮胎面中间温度 (float32 × 4) — offset 23-38
        WriteTyreArray(frame, 23, data.tyreTempMiddle, TelemetryAPI.ValidFlags.TyreTempMiddle, data);

        // 四轮胎面外侧温度 (float32 × 4) — offset 39-54
        WriteTyreArray(frame, 39, data.tyreTempOuter, TelemetryAPI.ValidFlags.TyreTempOuter, data);

        // 前轮刹车温度 FL/FR (float32 × 2) — offset 55-62
        if (HasFlag(data, TelemetryAPI.ValidFlags.BrakeTemp) && data.brakeTemp != null)
        {
            for (int i = 0; i < 2 && i < data.brakeTemp.Length; i++)
                BitConverter.TryWriteBytes(frame.AsSpan(55 + i * 4), data.brakeTemp[i]);
        }

        // offset 63: 保留（补齐 64 字节）

        return frame;
    }

    // ════════════════════════════════════════════════════════════════
    //  包3: 车辆信息 (0x6103) — 胎核温度 + 胎压 + 胎磨损 + 后轮刹车温度 + 发动机模式
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建车辆信息数据包3（0x6103）。
    /// 包含：四轮胎核心温度、四轮胎压力、四轮胎磨损、左后/右后刹车温度、发动机模式（Engine Map）。
    /// 协议顺序：FL, FR, RL, RR。
    /// </summary>
    public static byte[] BuildVehicleInfo3Packet(
        TelemetryAPI.NormalizedData data, uint timestampMs)
    {
        var frame = new byte[FrameSize];
        WriteHeader(frame, PacketType.VehicleInfo3, timestampMs);

        // 四轮胎核心温度 (float32 × 4) — offset 7-22
        WriteTyreArray(frame, 7, data.tyreCoreTemp, TelemetryAPI.ValidFlags.TyreCoreTemp, data);

        // 四轮胎压力 (float32 × 4, kPa) — offset 23-38
        WriteTyreArray(frame, 23, data.tyrePressure, TelemetryAPI.ValidFlags.TyrePressure, data);

        // 四轮胎磨损 (float32 × 4, 0-100) — offset 39-54
        WriteTyreArray(frame, 39, data.tyreWear, TelemetryAPI.ValidFlags.TyreWear, data);

        // 后轮刹车温度 RL/RR (float32 × 2) — offset 55-62
        if (HasFlag(data, TelemetryAPI.ValidFlags.BrakeTemp) && data.brakeTemp != null)
        {
            for (int i = 2; i < 4 && i < data.brakeTemp.Length; i++)
                BitConverter.TryWriteBytes(frame.AsSpan(55 + (i - 2) * 4), data.brakeTemp[i]);
        }

        // 发动机模式（Engine Map） — offset 63
        if (HasFlag(data, TelemetryAPI.ValidFlags.EnginePower))
            frame[63] = (byte)Math.Clamp(data.enginePowerMode, 0, 255);

        return frame;
    }

    // ════════════════════════════════════════════════════════════════
    //  批量构建
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 从 NormalizedData v1.0.0 构建完整的遥测数据三包。
    /// </summary>
    /// <param name="data">归一化遥测数据</param>
    /// <param name="timestampMs">模拟时间戳（毫秒）</param>
    /// <returns>三个遥测数据包（索引 0=0x6101, 1=0x6102, 2=0x6103）</returns>
    public static byte[][] BuildAllPackets(
        TelemetryAPI.NormalizedData data,
        uint? timestampMs = null)
    {
        var timestamp = timestampMs ?? (uint)Environment.TickCount;

        return new[]
        {
            BuildVehicleInfo1Packet(data, timestamp),
            BuildVehicleInfo2Packet(data, timestamp),
            BuildVehicleInfo3Packet(data, timestamp),
        };
    }

    // ════════════════════════════════════════════════════════════════
    //  辅助方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>将 NormalizedData.gear 转换为协议挡位字节</summary>
    /// <remarks>协议：0=N, 1-100=前进挡, 0xFF=R1, 0xFE=R2, 0xFD=R3, 0xFC=R4</remarks>
    public static byte GearFromNormalized(int gear)
    {
        return gear switch
        {
            -1 => GearValue.Reverse1,
            -2 => GearValue.Reverse2,
            -3 => GearValue.Reverse3,
            -4 => GearValue.Reverse4,
            0 => GearValue.Neutral,
            >= 1 and <= 100 => (byte)gear,
            > 100 => 100,                       // 超过 100 → 截断为 100
            _ => GearValue.Neutral              // 未知 → 空挡
        };
    }

    /// <summary>
    /// 将 NormalizedData 的 11 个旗语 bool 打包为 uint16（低 11 位）。
    /// 位序按协议固定：黄→绿→蓝→白→黑→方格→处罚→肉丸(橙)→红→安全车→虚拟安全车（bit0 起）。
    /// 注意与 NormalizedData 字段顺序（绿旗居首）不一致，必须显式重排。
    /// </summary>
    private static ushort PackRaceFlags(TelemetryAPI.NormalizedData data)
    {
        ushort flags = 0;
        if (data.isYellowFlag)    flags |= 1 << 0;  // 黄
        if (data.isGreenFlag)     flags |= 1 << 1;  // 绿
        if (data.isBlueFlag)      flags |= 1 << 2;  // 蓝
        if (data.isWhiteFlag)     flags |= 1 << 3;  // 白
        if (data.isBlackFlag)     flags |= 1 << 4;  // 黑
        if (data.isCheckeredFlag) flags |= 1 << 5;  // 方格
        if (data.isPenaltyFlag)   flags |= 1 << 6;  // 处罚
        if (data.isOrangeFlag)    flags |= 1 << 7;  // 肉丸（橙）
        if (data.isRedFlag)       flags |= 1 << 8;  // 红
        if (data.isScActive)      flags |= 1 << 9;  // 安全车
        if (data.isVscActive)     flags |= 1 << 10; // 虚拟安全车
        return flags;
    }

    /// <summary>将四轮 float 数组写入 frame（flag 覆盖整个数组；数组为空时跳过）</summary>
    private static void WriteTyreArray(
        byte[] frame, int offset, float[]? values, ulong flag, TelemetryAPI.NormalizedData data)
    {
        if (!HasFlag(data, flag) || values == null) return;
        for (int i = 0; i < 4 && i < values.Length; i++)
            BitConverter.TryWriteBytes(frame.AsSpan(offset + i * 4), values[i]);
    }
}
