# 新增 iRacing 方向盘力反馈扭矩数据包（0x6104）实现计划

## Context（背景）

遥测链路（60Hz 三包 0x6101~0x6103）需要为 iRacing 新增一项特殊数据：方向盘力反馈扭矩 `steeringWheelTorqueST[6]`（6 个 float 采样）。该字段：
- 放入 `NormalizedData` 结构体 `validFlags` 之后的 24 字节（占用原 `_reserved`，总大小 512 字节不变，ABI 兼容）；
- 单独走一个 **0x6104 数据包**下发（每包 64 字节，offset 7-10 仅一个 float 扭矩值，格式与 0x6101 包头一致）；
- **仅 iRacing（GameId=266410）启动时才下发**；
- 使用**独立 360Hz 高频循环**轮流发送数组 6 个值（每约 2.78ms 一个包，下标循环 0→5，每个值 60Hz、合计 360 包/秒）。

用户已确认：TelemetrySDK.dll（C++ 侧）已同步填充该字段；需要同步更新《遥测数据包字段处理流程技术文档.md》。

## 修改点

### 1. `Services/TelemetryAPI.cs` — 结构体加字段（保持 512 字节布局）

- `NormalizedData`（L156-159）：在 `public ulong validFlags;` 之后插入：
  ```csharp
  [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
  public float[] steeringWheelTorqueST;   // iRacing 方向盘力反馈扭矩（6 个采样）
  ```
  并把 `_reserved` 的 `SizeConst` 从 `249` 改为 `225`，更新注释（263 已用 + 24 + 225 = 512）。
- `CreateNormalizedData()`（L372-388）：`_reserved = new byte[249]` 改为 `new byte[225]`，并新增初始化 `steeringWheelTorqueST = new float[6];`（与其它内联数组成员一致，防 NRE）。

### 2. `Services/TelemetryPacketBuilder.cs` — 新增 0x6104 包构建

- `PacketType` 嵌套类（L13-18）加 `public const ushort VehicleInfo4 = 0x6104;`。
- 新增方法（复用 `WriteHeader`，偏移 7-10 写 float LE，其余保持 0）：
  ```csharp
  /// <summary>构建力反馈扭矩数据包（0x6104）：ID + 包类型 + 时间戳 + 单个扭矩值</summary>
  public static byte[] BuildVehicleInfo4Packet(float torque, uint timestampMs)
  {
      var frame = new byte[FrameSize];
      WriteHeader(frame, PacketType.VehicleInfo4, timestampMs);
      // 方向盘力反馈扭矩 (float32 LE, N·m) — offset 7-10
      BitConverter.TryWriteBytes(frame.AsSpan(7), torque);
      return frame;
  }
  ```
- **不加入 `BuildAllPackets`**（该包仅 iRacing + 360Hz 独立通道，保持三包语义不变）。

### 3. `Services/TelemetryService.cs` — 独立 360Hz 下发线程 + 发送互斥

- 新增私有字段：
  ```csharp
  private readonly object _telemetrySendLock = new();  // 60Hz 三包与 360Hz 扭矩包串口发送互斥
  private float[]? _latestSteeringTorque;              // 最新舵矩快照（lock 保护）
  private Thread? _torqueThread;                       // 360Hz 独立下发线程
  private CancellationTokenSource? _torqueCts;
  ```
- **快照更新**：`ProcessFrame`（L410-429）内，在 `BuildAllPackets` 前后将 `data.steeringWheelTorqueST` 复制到 `_latestSteeringTorque`（`lock(_lock)` 下 `Array.Copy`，避免引用漂移）。
- **线程启动**：`Start(...)` 中，当 `gameId == (int)TelemetryAPI.GameId.IRacing` 时，在启动 `_loopThread` 后追加启动 `_torqueThread`（命名 `TelemetryTorqueLoop`、后台线程），执行 `TorqueLoopProc(token)`：
  ```csharp
  private void TorqueLoopProc(object? state)
  {
      var token = (CancellationToken)state!;
      var index = 0;
      while (!token.IsCancellationRequested)
      {
          float[]? torque;
          lock (_lock) { torque = _latestSteeringTorque; }
          if (torque != null)
          {
              var timestampMs = (uint)Stopwatch.GetElapsedTime(_telemetryStartTick).TotalMilliseconds;
              var packet = TelemetryPacketBuilder.BuildVehicleInfo4Packet(torque[index % 6], timestampMs);
              lock (_telemetrySendLock) { SendTorquePacket(packet); }
          }
          index++;
          token.WaitHandle.WaitOne(TorqueLoopInterval); // 1000/360 ≈ 3ms 或精确 2.78ms
      }
  }
  ```
- **发送方法**：从 `DispatchPackets`（L473-518）中抽取设备过滤逻辑为私有 `List<...> GetTargetDevices()`（USB Manager 运行 + 有设备 + 纯正常模式），新增：
  ```csharp
  private void SendTorquePacket(byte[] packet)   // 单包，逐设备发送，不触发 OnPacketsBuilt/OnPacketsDispatched
  ```
  `DispatchPackets` 改为复用 `GetTargetDevices()`；发送循环统一括在 `lock (_telemetrySendLock)` 内与 360Hz 互斥（关键：`DeviceSerialChannel.Send` 为无锁串口写，双线程并发会交错）。
- **生命周期**：`StopInternal()`（L248-275）中在停止 `_loopThread` 之后，对称取消并 `Join(500)` `_torqueThread`，并清空 `_latestSteeringTorque`。切游戏/停止/释放（Dispose→Stop）均复用此路径。
- 常量：`private static readonly TimeSpan TorqueLoopInterval = TimeSpan.FromMilliseconds(1000d / 360);`（约 2.78ms，单帧耗时导致实际低于 360Hz 属预期，无补偿逻辑）。

### 4. 文档 `docs/遥测数据包字段处理流程技术文档.md`

- NormalizedData 结构说明：新增 `steeringWheelTorqueST[6]` 字段（iRacing 力反馈扭矩，仅 GameId=266410 有值），说明 `_reserved` 相应 249→225、总字节数仍 512。
- 新增 0x6104 包小节：offset 表（0=0x61 / 1-2=0x6104 小端 / 3-6=时间戳 LE / 7-10=扭矩 float LE）、**iRacing 专属 + 360Hz 循环轮流发 6 值（每值 60Hz）**、与 60Hz 三包的关系（独立线程、发送互斥）。
- 如流程图中体现三包下发，同步补充第四包/360Hz 通道。

## 不做的事

- 不改 `BuildAllPackets` 返回值、不改 `DispatchPackets` 对外行为/事件（0x6104 不计入 `OnPacketsDispatched` 计数，语义保持"三包批次"）。
- 不加 ValidFlags 新位（仍以"iRacing & 快照存在"门控，与 maxRpm 数值模式一致）。
- 不修改 `DeviceSerialChannel.Send` 底层（互斥放在 TelemetryService 层，最小侵入）。

## 验证

1. `dotnet build HITAPEX.csproj -c Debug` 通过（0 错误）；
2. 代码审查确认：
   - `NormalizedData` 总大小仍 512（263 + 24 + 225），`Marshal.SizeOf` 可打印核对；
   - `BuildVehicleInfo4Packet` 输出前 11 字节为 `61 04 61 [ts LE] [float LE]`；
   - `Start`/`Stop`/切游戏/`Dispose` 时 360Hz 线程正确启停，无泄漏；
   - 两个发送线程共用 `_telemetrySendLock`，无交错写串口。
3. 真机验证（用户侧）：启动 iRacing 后观测设备收到 0x6104 流（360 包/秒），非 iRacing 游戏不出现该包。