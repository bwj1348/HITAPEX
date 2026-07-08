namespace HITAPEX.Services;

/// <summary>
/// 将 TelemetrySDK v2.0 NormalizedData 转换为 USB 协议遥测数据包（0x6101~0x6105）。
/// 协议定义参考：docs/乘游直驱方向盘与PC软件usb通信协议 v0.1.md 第8节"遥测数据"。
/// 轮胎数组统一：[0]=FL, [1]=FR, [2]=RL, [3]=RR。
/// </summary>
public static class TelemetryPacketBuilder
{
    private const int FrameSize = 64;

    /// <summary>遥测数据包类型</summary>
    private static class PacketType
    {
        public const ushort VehicleInfo1  = 0x6101;  // 车辆信息包1（基础驾驶参数）
        public const ushort VehicleInfo2  = 0x6102;  // 车辆信息包2（刹车温度+胎面内侧/中间温度）
        public const ushort VehicleInfo3  = 0x6103;  // 车辆信息包3（胎面外侧温度+胎核温度+胎压）
        public const ushort VehicleInfo4  = 0x6104;  // 车辆信息包4（胎磨损+水温+油温+涡轮压力）
        public const ushort RaceSpeed     = 0x6105;  // 比赛/车速信息（圈数+排名+圈速）
    }

    /// <summary>协议挡位常量</summary>
    public static class GearValue
    {
        public const byte Reverse = 0xFF;  // 倒挡
        public const byte Neutral = 0;     // 空挡
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

    private static bool HasFlag(TelemetryAPI.NormalizedData data, ulong flag) =>
        (data.validFlags & flag) != 0;

    // ════════════════════════════════════════════════════════════════
    //  包1: 车辆信息 (0x6101) — 基础驾驶参数
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建车辆信息数据包1（0x6101）。
    /// 包含：速度、转速、档位、油门/刹车/离合/转向、状态标志、档位/旗帜、ERS、发动机、燃油。
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
        if (HasFlag(data, TelemetryAPI.ValidFlags.MaxRpm))
        {
            var maxRpm = (ushort)Math.Clamp(data.maxRpm, 0f, 65535f);
            frame[11] = (byte)(maxRpm & 0xFF);
            frame[12] = (byte)((maxRpm >> 8) & 0xFF);
        }

        // 当前转速 (uint16 LE) — offset 13-14
        if (HasFlag(data, TelemetryAPI.ValidFlags.Rpm))
        {
            var rpm = (ushort)Math.Clamp(data.rpm, 0f, 65535f);
            frame[13] = (byte)(rpm & 0xFF);
            frame[14] = (byte)((rpm >> 8) & 0xFF);
        }

        // offset 15: 保留（协议缺口）

        // 档位 — offset 16
        // 协议：0=N, 1-100=前进挡, 0xFF=倒挡
        if (HasFlag(data, TelemetryAPI.ValidFlags.Gear))
        {
            frame[16] = GearFromNormalized(data.gear);
        }

        // 油门 (float32 LE) — offset 17-20
        if (HasFlag(data, TelemetryAPI.ValidFlags.Throttle))
            BitConverter.TryWriteBytes(frame.AsSpan(17), data.throttle);

        // 刹车 (float32 LE) — offset 21-24
        if (HasFlag(data, TelemetryAPI.ValidFlags.Brake))
            BitConverter.TryWriteBytes(frame.AsSpan(21), data.brake);

        // 离合 (float32 LE) — offset 25-28
        if (HasFlag(data, TelemetryAPI.ValidFlags.Clutch))
            BitConverter.TryWriteBytes(frame.AsSpan(25), data.clutch);

        // 转向 (float32 LE) — offset 29-32
        if (HasFlag(data, TelemetryAPI.ValidFlags.Steer))
            BitConverter.TryWriteBytes(frame.AsSpan(29), data.steer);

        // 状态标志 (bool → byte) — offset 33-37
        if (HasFlag(data, TelemetryAPI.ValidFlags.PitLimiter))
            frame[33] = data.isPitLimiterActive ? (byte)1 : (byte)0;
        if (HasFlag(data, TelemetryAPI.ValidFlags.TcActive))
            frame[34] = data.isTcActive ? (byte)1 : (byte)0;
        if (HasFlag(data, TelemetryAPI.ValidFlags.AbsActive))
            frame[35] = data.isAbsActive ? (byte)1 : (byte)0;
        if (HasFlag(data, TelemetryAPI.ValidFlags.DrsAvailable))
            frame[36] = data.isDrsAvailable ? (byte)1 : (byte)0;
        if (HasFlag(data, TelemetryAPI.ValidFlags.DrsActive))
            frame[37] = data.isDrsActive ? (byte)1 : (byte)0;

        // TC / ABS / TC Cut 档位 — offset 38-40
        if (HasFlag(data, TelemetryAPI.ValidFlags.TcLevel))
            frame[38] = (byte)Math.Clamp(data.tcLevel, 0, 255);
        if (HasFlag(data, TelemetryAPI.ValidFlags.AbsLevel))
            frame[39] = (byte)Math.Clamp(data.absLevel, 0, 255);
        if (HasFlag(data, TelemetryAPI.ValidFlags.TcCut))
            frame[40] = (byte)Math.Clamp(data.tcCutLevel, 0, 255);

        // 旗语 — offset 41
        if (HasFlag(data, TelemetryAPI.ValidFlags.RaceFlag))
            frame[41] = (byte)Math.Clamp(data.raceFlag, 0, 10);

        // ERS 电量 (float32 LE, 0.0-1.0) — offset 42-45
        if (HasFlag(data, TelemetryAPI.ValidFlags.ErsCharge))
            BitConverter.TryWriteBytes(frame.AsSpan(42), data.ersCharge);

        // ERS 部署档位 — offset 46
        if (HasFlag(data, TelemetryAPI.ValidFlags.ErsDeploy))
            frame[46] = (byte)Math.Clamp(data.ersDeployMode, 0, 255);

        // ERS 是否工作 — offset 47
        if (HasFlag(data, TelemetryAPI.ValidFlags.ErsActive))
            frame[47] = data.isErsActive ? (byte)1 : (byte)0;

        // ERS 回收级别 — offset 48
        if (HasFlag(data, TelemetryAPI.ValidFlags.ErsRecovery))
            frame[48] = (byte)Math.Clamp(data.ersRecoveryLevel, 0, 100);

        // 发动机是否启动 — offset 49
        if (HasFlag(data, TelemetryAPI.ValidFlags.EngineRunning))
            frame[49] = data.isEngineRunning ? (byte)1 : (byte)0;

        // 发动机点火状态 — offset 50
        if (HasFlag(data, TelemetryAPI.ValidFlags.Ignition))
            frame[50] = data.isIgnitionOn ? (byte)1 : (byte)0;

        // 发动机动力档位 — offset 51
        if (HasFlag(data, TelemetryAPI.ValidFlags.EnginePower))
            frame[51] = (byte)Math.Clamp(data.enginePowerMode, 0, 255);

        // 剩余油量 (float32 LE) — offset 52-55
        if (HasFlag(data, TelemetryAPI.ValidFlags.Fuel))
            BitConverter.TryWriteBytes(frame.AsSpan(52), data.fuelRemaining);

        // 剩余油量百分比 (float32 LE) — offset 56-59
        if (HasFlag(data, TelemetryAPI.ValidFlags.FuelPct))
            BitConverter.TryWriteBytes(frame.AsSpan(56), data.fuelRemainingPct);

        // offset 60-63: 保留

        return frame;
    }

    // ════════════════════════════════════════════════════════════════
    //  包2: 车辆信息 (0x6102) — 刹车温度 + 胎面内侧/中间温度
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建车辆信息数据包2（0x6102）。
    /// 包含：四轮刹车温度、四轮胎面内侧温度、四轮胎面中间温度。
    /// </summary>
    public static byte[] BuildVehicleInfo2Packet(
        TelemetryAPI.NormalizedData data, uint timestampMs)
    {
        var frame = new byte[FrameSize];
        WriteHeader(frame, PacketType.VehicleInfo2, timestampMs);

        // 四轮刹车温度 (float32 × 4) — offset 7-22
        // 协议顺序: FL, FR, RL, RR
        if (HasFlag(data, TelemetryAPI.ValidFlags.BrakeTemp) && data.brakeTemp != null)
        {
            for (int i = 0; i < 4 && i < data.brakeTemp.Length; i++)
                BitConverter.TryWriteBytes(frame.AsSpan(7 + i * 4), data.brakeTemp[i]);
        }

        // 四轮胎面内侧温度 (float32 × 4) — offset 23-38
        if (HasFlag(data, TelemetryAPI.ValidFlags.TyreTempInner) && data.tyreTempInner != null)
        {
            for (int i = 0; i < 4 && i < data.tyreTempInner.Length; i++)
                BitConverter.TryWriteBytes(frame.AsSpan(23 + i * 4), data.tyreTempInner[i]);
        }

        // 四轮胎面中间温度 (float32 × 4) — offset 39-54
        if (HasFlag(data, TelemetryAPI.ValidFlags.TyreTempMiddle) && data.tyreTempMiddle != null)
        {
            for (int i = 0; i < 4 && i < data.tyreTempMiddle.Length; i++)
                BitConverter.TryWriteBytes(frame.AsSpan(39 + i * 4), data.tyreTempMiddle[i]);
        }

        // offset 55-63: 保留

        return frame;
    }

    // ════════════════════════════════════════════════════════════════
    //  包3: 车辆信息 (0x6103) — 胎面外侧温度 + 胎核温度 + 胎压
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建车辆信息数据包3（0x6103）。
    /// 包含：四轮胎面外侧温度、四轮胎核心温度、四轮胎压力。
    /// </summary>
    public static byte[] BuildVehicleInfo3Packet(
        TelemetryAPI.NormalizedData data, uint timestampMs)
    {
        var frame = new byte[FrameSize];
        WriteHeader(frame, PacketType.VehicleInfo3, timestampMs);

        // 四轮胎面外侧温度 (float32 × 4) — offset 7-22
        if (HasFlag(data, TelemetryAPI.ValidFlags.TyreTempOuter) && data.tyreTempOuter != null)
        {
            for (int i = 0; i < 4 && i < data.tyreTempOuter.Length; i++)
                BitConverter.TryWriteBytes(frame.AsSpan(7 + i * 4), data.tyreTempOuter[i]);
        }

        // 四轮胎核心温度 (float32 × 4) — offset 23-38
        if (HasFlag(data, TelemetryAPI.ValidFlags.TyreCoreTemp) && data.tyreCoreTemp != null)
        {
            for (int i = 0; i < 4 && i < data.tyreCoreTemp.Length; i++)
                BitConverter.TryWriteBytes(frame.AsSpan(23 + i * 4), data.tyreCoreTemp[i]);
        }

        // 四轮胎压力 (float32 × 4) — offset 39-54
        if (HasFlag(data, TelemetryAPI.ValidFlags.TyrePressure) && data.tyrePressure != null)
        {
            for (int i = 0; i < 4 && i < data.tyrePressure.Length; i++)
                BitConverter.TryWriteBytes(frame.AsSpan(39 + i * 4), data.tyrePressure[i]);
        }

        // offset 55-63: 保留

        return frame;
    }

    // ════════════════════════════════════════════════════════════════
    //  包4: 车辆信息 (0x6104) — 胎磨损 + 水温 + 油温 + 涡轮压力
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建车辆信息数据包4（0x6104）。
    /// 包含：四轮胎磨损百分比、冷却水温、机油温度、涡轮增压压力。
    /// </summary>
    public static byte[] BuildVehicleInfo4Packet(
        TelemetryAPI.NormalizedData data, uint timestampMs)
    {
        var frame = new byte[FrameSize];
        WriteHeader(frame, PacketType.VehicleInfo4, timestampMs);

        // 四轮胎磨损百分比 (float32 × 4, 0-100) — offset 7-22
        if (HasFlag(data, TelemetryAPI.ValidFlags.TyreWear) && data.tyreWear != null)
        {
            for (int i = 0; i < 4 && i < data.tyreWear.Length; i++)
                BitConverter.TryWriteBytes(frame.AsSpan(7 + i * 4), data.tyreWear[i]);
        }

        // 冷却水温度 (float32 LE) — offset 23-26
        if (HasFlag(data, TelemetryAPI.ValidFlags.WaterTemp))
            BitConverter.TryWriteBytes(frame.AsSpan(23), data.waterTemp);

        // 机油温度 (float32 LE) — offset 27-30
        if (HasFlag(data, TelemetryAPI.ValidFlags.OilTemp))
            BitConverter.TryWriteBytes(frame.AsSpan(27), data.oilTemp);

        // 涡轮增压压力 (float32 LE, bar) — offset 31-34
        if (HasFlag(data, TelemetryAPI.ValidFlags.TurboPressure))
            BitConverter.TryWriteBytes(frame.AsSpan(31), data.turboPressure);

        // offset 35-63: 保留

        return frame;
    }

    // ════════════════════════════════════════════════════════════════
    //  包5: 比赛/车速信息 (0x6105) — 圈数+排名+圈速
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建比赛/车速信息数据包（0x6105）。
    /// 包含：总圈数、当前圈数、排名、当前圈时、上一圈时、最佳圈时。
    /// </summary>
    public static byte[] BuildRaceSpeedPacket(
        TelemetryAPI.NormalizedData data, uint timestampMs)
    {
        var frame = new byte[FrameSize];
        WriteHeader(frame, PacketType.RaceSpeed, timestampMs);

        // 总圈数 (uint16 LE) — offset 7-8
        if (HasFlag(data, TelemetryAPI.ValidFlags.TotalLaps))
        {
            var total = (ushort)Math.Clamp(data.totalLaps, 0, 65535);
            frame[7] = (byte)(total & 0xFF);
            frame[8] = (byte)((total >> 8) & 0xFF);
        }

        // 当前圈数 (uint16 LE) — offset 9-10
        if (HasFlag(data, TelemetryAPI.ValidFlags.CurrentLapNum))
        {
            var lap = (ushort)Math.Clamp(data.currentLap, 0, 65535);
            frame[9] = (byte)(lap & 0xFF);
            frame[10] = (byte)((lap >> 8) & 0xFF);
        }

        // 排名 — offset 11
        if (HasFlag(data, TelemetryAPI.ValidFlags.Position))
            frame[11] = (byte)Math.Clamp(data.position, 0, 255);

        // 当前圈时 (float32 LE, 秒) — offset 12-15
        if (HasFlag(data, TelemetryAPI.ValidFlags.CurrentLapTime))
            BitConverter.TryWriteBytes(frame.AsSpan(12), data.currentLapTime);

        // 上一圈时 (float32 LE, 秒) — offset 16-19
        if (HasFlag(data, TelemetryAPI.ValidFlags.LastLap))
            BitConverter.TryWriteBytes(frame.AsSpan(16), data.lastLapTime);

        // 最佳圈时 (float32 LE, 秒) — offset 20-23
        if (HasFlag(data, TelemetryAPI.ValidFlags.BestLap))
            BitConverter.TryWriteBytes(frame.AsSpan(20), data.bestLapTime);

        // offset 24-63: 保留

        return frame;
    }

    // ════════════════════════════════════════════════════════════════
    //  批量构建
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 从 NormalizedData v2.0 构建完整的遥测数据五包。
    /// </summary>
    /// <param name="data">归一化遥测数据</param>
    /// <param name="timestampMs">模拟时间戳（毫秒）</param>
    /// <returns>五个遥测数据包（索引 0=0x6101, 1=0x6102, 2=0x6103, 3=0x6104, 4=0x6105）</returns>
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
            BuildVehicleInfo4Packet(data, timestamp),
            BuildRaceSpeedPacket(data, timestamp),
        };
    }

    // ════════════════════════════════════════════════════════════════
    //  辅助方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>将 NormalizedData.gear 转换为协议挡位字节</summary>
    /// <remarks>协议：0=N, 1-100=前进挡, 0xFF=倒挡</remarks>
    public static byte GearFromNormalized(int gear)
    {
        return gear switch
        {
            -1 => GearValue.Reverse,                  // 倒挡 → 0xFF
            0  => GearValue.Neutral,                  // 空挡 → 0x00
            >= 1 and <= 100 => (byte)gear,            // 前进挡 1-100 → 直接映射
            > 100 => 100,                             // 超过 100 → 截断为 100
            _ => GearValue.Neutral                    // 未知 → 空挡
        };
    }
}
