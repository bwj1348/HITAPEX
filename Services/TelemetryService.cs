using System.Diagnostics;

namespace HITAPEX.Services;

/// <summary>
/// 遥测数据服务：管理 TelemetrySDK v1.0.0 生命周期，
/// 以 ~60Hz 的频率循环读取游戏遥测数据并通过 USB 串口广播到所有已连接设备（基座、面盘、踏板）。
/// 数据到达性以 GetTelemetryStatus 连接状态机为准 —— 仅在 CONNECTED（数据新鲜）时下发数据包。
/// </summary>
public class TelemetryService : IDisposable
{
    private readonly object _lock = new();

    private Thread? _loopThread;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private int _currentGameId = -1;
    private bool _disposed;

    // 时间戳：自遥测启动以来的累计模拟时间（毫秒）
    private long _telemetryStartTick;

    // 自适应最大转速追踪状态
    private float _trackedMaxRpm;
    private int _rpmZeroFrameCount;

    // 最近一次连接状态快照（由采集线程写入，UI 线程读取）
    private TelemetryAPI.TelemetryStatus _lastStatus;
    private TelemetryAPI.TelemetryConnState _lastReportedConnState = TelemetryAPI.TelemetryConnState.Idle;

    /// <summary>最近一次 StartTelemetry 失败的错误码（成功启动后为 None）</summary>
    public TelemetryAPI.TelemetryErrorCode LastStartErrorCode { get; private set; }

    /// <summary>最近一次 StartTelemetry 失败的中文错误消息（可直接弹窗）</summary>
    public string LastStartErrorMessage { get; private set; } = "";

    // 目标循环间隔 ~16ms (60Hz)
    private static readonly TimeSpan LoopInterval = TimeSpan.FromMilliseconds(16);

    // 进程存活检测间隔（每 300 帧 ≈ 5 秒检查一次）
    private const int ProcessCheckIntervalFrames = 300;

    // 自适应最大转速 —— LFS、RBR、BeamNG 三款游戏遥测协议不提供 maxRpm 字段
    private const float DefaultMaxRpm = 6000f;
    private const int RpmZeroResetFrames = 300; // 连续 5 秒转速为 0 → 可能更换车辆，重置为默认值
    private static readonly HashSet<int> GamesNeedingMaxRpmTracking = [22, 25, 284160, 3917090];

    /// <summary>GameId → 进程名列表映射。用于轮询检测目标游戏进程是否仍在运行。</summary>
    private static readonly Dictionary<int, string[]> GameProcessNames = new()
    {
        // Assetto Corsa 系列
        { 244210,  ["AssettoCorsa"] },
        { 805550,  ["acc", "AC2-Win64-Shipping"] },
        { 3917090, ["acr"] },
        { 3058630, ["AssettoCorsaEVO"] },
        // F1 系列
        { 1692250, ["F1_22"] },
        { 2108330, ["F1_23"] },
        { 2488620, ["F1_24"] },
        { 3059520, ["F1_25"] },
        // Forza 系列
        { 2440510, ["forza_steamworks_release_final"] },
        { 1293830, ["ForzaHorizon4"] },
        { 1551360, ["ForzaHorizon5"] },
        { 2483190, ["forzahorizon6"] },
        // DiRT 系列
        { 421020,  ["dirt4"] },
        { 690790,  ["dirtrally2"] },
        // rFactor / LMU 系列
        { 365960,  ["rFactor2"] },
        { 2399420, ["Le Mans Ultimate"] },
        // Project CARS / AMS2 系列
        { 378860,  ["pCARS2", "pCARS2AVX"] },
        { 958400,  ["pCARS3", "pCARS3AVX"] },
        { 1066890, ["AMS2", "AMS2AVX"] },
        // WRC 系列
        { 1004750, ["WRC8"] },
        { 1267540, ["WRC9"] },
        { 1462810, ["WRC10"] },
        { 1953520, ["WRCG", "WRCGenerations"] },
        { 1849250, ["WRC", "EAAntiCheat.GameServiceLauncher", "EAAntiCheat.GameService"] },
        // 其他竞速
        { 266410,  ["iRacing", "iRacingUI", "iRacing.com Simulator"] },
        { 211500,  ["RRRE", "RRRE64", "RRREWebBrowser"] },
        { 284160,  ["BeamNG.drive", "BeamNG.drive.x64"] },
        // 模拟驾驶
        { 227300,  ["eurotrucks2"] },
        { 270880,  ["amtrucks"] },
        // 非 Steam 游戏
        { 22, ["RichardBurnsRally_SSE", "RSF_Launcher"] },
        { 25, ["LFS"] },
    };

    // ════════════════════════════════════════════════════════════════
    //  事件
    // ════════════════════════════════════════════════════════════════

    /// <summary>遥测启动成功时触发</summary>
    public event Action<int>? OnStarted;          // (gameId)

    /// <summary>遥测启动失败时触发（失败原因见 LastStartErrorCode / LastStartErrorMessage）</summary>
    public event Action<int>? OnStartFailed;      // (gameId)

    /// <summary>遥测停止时触发</summary>
    public event Action? OnStopped;

    /// <summary>连接状态变化时触发（connState 变化才触发，避免 60Hz 刷屏；初值为 Idle）</summary>
    public event Action<TelemetryAPI.TelemetryStatus>? OnStatusChanged;

    /// <summary>数据包已构建并准备发送时触发（可用于调试/日志）</summary>
    public event Action<byte[][]>? OnPacketsBuilt;  // (three packets: 0x6101~0x6103)

    /// <summary>遥测数据已下发到基座时触发</summary>
    public event Action<uint>? OnPacketsDispatched; // (timestampMs)

    // ════════════════════════════════════════════════════════════════
    //  公开属性
    // ════════════════════════════════════════════════════════════════

    /// <summary>遥测是否正在运行</summary>
    public bool IsRunning
    {
        get { lock (_lock) return _isRunning; }
    }

    /// <summary>当前采集的游戏 ID，-1 表示未启动</summary>
    public int CurrentGameId
    {
        get { lock (_lock) return _currentGameId; }
    }

    /// <summary>当前 SDK 版本号</summary>
    public int SdkVersion => TelemetryAPI.GetSDKVersion();

    /// <summary>当前游戏支持的字段掩码（启动后有效；未启动返回 0）</summary>
    public ulong SupportedFlags
    {
        get
        {
            lock (_lock) return _isRunning ? TelemetryAPI.GetSupportedFlags() : 0;
        }
    }

    /// <summary>最新连接状态快照（未启动时为全默认字段）</summary>
    public TelemetryAPI.TelemetryStatus CurrentStatus
    {
        get { lock (_lock) return _lastStatus; }
    }

    /// <summary>最新连接状态枚举</summary>
    public TelemetryAPI.TelemetryConnState ConnectionState
    {
        get { lock (_lock) return (TelemetryAPI.TelemetryConnState)_lastStatus.connState; }
    }

    // ════════════════════════════════════════════════════════════════
    //  生命周期管理
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 启动遥测数据采集。如果已有游戏在采集中，先停止（SDK 内部也会自动停旧会话，等价且安全）。
    /// UDP 游戏会先应用该游戏的 UDP 监听/转发设置（SetUDPSettings 须在 StartTelemetry 之前调用）。
    /// </summary>
    /// <param name="gameId">TelemetrySDK 的游戏 ID（Steam App ID 或自定义 ID）</param>
    /// <returns>是否成功启动</returns>
    public bool Start(int gameId)
    {
        lock (_lock)
        {
            if (_disposed) return false;

            // 如果已在运行同一游戏，直接返回
            if (_isRunning && _currentGameId == gameId)
                return true;

            // 停止现有采集
            StopInternal();
        }

        Debug.WriteLine($"[Telemetry] 正在启动遥测采集，GameId={gameId}");

        // 应用 UDP 设置（仅 UDP 游戏有效；共享内存游戏 SDK 返回 false，无害）
        try
        {
            if (TelemetryUdpSettingsService.Apply(gameId))
                Debug.WriteLine($"[Telemetry] UDP 设置已应用，GameId={gameId}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Telemetry] UDP 设置应用异常: {ex.Message}");
        }

        if (!TelemetryAPI.StartTelemetry(gameId))
        {
            LastStartErrorCode = (TelemetryAPI.TelemetryErrorCode)TelemetryAPI.GetLastTelemetryError();
            LastStartErrorMessage = TelemetryAPI.GetLastErrorMessage();
            Debug.WriteLine($"[Telemetry] StartTelemetry 失败，GameId={gameId}, Err={LastStartErrorCode}({(int)LastStartErrorCode}), Msg={LastStartErrorMessage}");
            OnStartFailed?.Invoke(gameId);
            return false;
        }

        LastStartErrorCode = TelemetryAPI.TelemetryErrorCode.None;
        LastStartErrorMessage = "";

        lock (_lock)
        {
            _currentGameId = gameId;
            _telemetryStartTick = Stopwatch.GetTimestamp();
            _isRunning = true;
        }

        // 初始化自适应最大转速追踪状态
        if (GamesNeedingMaxRpmTracking.Contains(gameId))
        {
            _trackedMaxRpm = DefaultMaxRpm;
            _rpmZeroFrameCount = 0;
        }

        // 启动后台采集线程
        _cts = new CancellationTokenSource();
        _loopThread = new Thread(LoopProc)
        {
            Name = "TelemetryLoop",
            IsBackground = true
        };
        _loopThread.Start(_cts.Token);

        Debug.WriteLine($"[Telemetry] 遥测采集已启动，GameId={gameId}, 支持字段=0x{TelemetryAPI.GetSupportedFlags():X16}");
        OnStarted?.Invoke(gameId);
        return true;
    }

    /// <summary>停止遥测数据采集</summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_disposed) return;
            StopInternal();
        }
    }

    /// <summary>
    /// 万能兜底重连：StopTelemetry() → StartTelemetry(gameId)，等价于重建会话。
    /// </summary>
    public bool Reconnect(int gameId) => Start(gameId);

    private void StopInternal()
    {
        // 取消后台线程
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_loopThread != null && _loopThread.IsAlive)
        {
            // 给线程 500ms 退出时间
            if (!_loopThread.Join(500))
            {
                Debug.WriteLine("[Telemetry] 后台线程未能及时退出");
            }
            _loopThread = null;
        }

        if (_isRunning)
        {
            TelemetryAPI.StopTelemetry();
            _isRunning = false;
            _currentGameId = -1;
            _lastStatus = default;
            _lastReportedConnState = TelemetryAPI.TelemetryConnState.Idle;
            Debug.WriteLine("[Telemetry] 遥测采集已停止");
            OnStopped?.Invoke();
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  后台采集循环
    // ════════════════════════════════════════════════════════════════

    private void LoopProc(object? state)
    {
        var token = (CancellationToken)state!;
        var data = TelemetryAPI.CreateNormalizedData();
        var frameCount = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var tickStart = Stopwatch.GetTimestamp();
                frameCount++;

                // 读取连接状态：仅 CONNECTED（数据新鲜）时才取数并下发，
                // WAITING_DATA / STALE / DISCONNECTED 时不下发（设备端保持无数据状态）。
                var status = new TelemetryAPI.TelemetryStatus();
                if (TelemetryAPI.GetTelemetryStatus(ref status))
                {
                    UpdateStatus(status);

                    if (TelemetryAPI.IsDataFresh(status))
                    {
                        if (TelemetryAPI.GetTelemetryData(ref data))
                        {
                            // 自适应最大转速追踪（LFS/RBR/BeamNG 不提供 maxRpm）
                            ApplyAdaptiveMaxRpm(ref data);
                            ProcessFrame(data);
                        }
                    }
                }

                // 每 ProcessCheckIntervalFrames 帧（~5 秒）检查一次目标游戏进程是否仍在运行
                if (frameCount % ProcessCheckIntervalFrames == 0)
                {
                    if (!IsTargetProcessAlive())
                    {
                        Debug.WriteLine("[Telemetry] 目标游戏进程已退出，自动停止遥测");
                        Task.Run(() => Stop());
                        break;
                    }
                }

                // 计算剩余睡眠时间，保持 ~60Hz 频率
                var elapsed = Stopwatch.GetElapsedTime(tickStart);
                var sleepMs = (int)(LoopInterval - elapsed).TotalMilliseconds;
                if (sleepMs > 0)
                {
                    token.WaitHandle.WaitOne(Math.Max(1, sleepMs));
                }
                else if (sleepMs < -10)
                {
                    // 如果单次处理耗时超过 10ms，记录警告
                    Debug.WriteLine($"[Telemetry] 帧处理超时: {elapsed.TotalMilliseconds:F1}ms");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 预期退出路径
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Telemetry] 采集循环异常: {ex}");
        }
    }

    /// <summary>更新最新状态快照，连接状态变化时触发 OnStatusChanged（轻量，供 UI 状态灯使用）</summary>
    private void UpdateStatus(TelemetryAPI.TelemetryStatus status)
    {
        var connState = (TelemetryAPI.TelemetryConnState)status.connState;

        lock (_lock)
        {
            _lastStatus = status;
        }

        // connState 变化（含回 Idle）才通知，避免 60Hz 刷屏
        if (connState != _lastReportedConnState)
        {
            _lastReportedConnState = connState;
            OnStatusChanged?.Invoke(status);
        }
    }

    /// <summary>
    /// 检测当前遥测目标游戏的进程是否仍在运行。
    /// </summary>
    private bool IsTargetProcessAlive()
    {
        int gameId;
        lock (_lock) { gameId = _currentGameId; }

        if (gameId < 0) return false;

        return IsGameProcessRunning(gameId);
    }

    /// <summary>
    /// 指定游戏是否有进程在运行；进程名未知时保守返回 true（避免误停止/误停重试）。
    /// </summary>
    public bool IsGameProcessRunning(int gameId)
    {
        if (!GameProcessNames.TryGetValue(gameId, out var processNames) || processNames.Length == 0)
        {
            // 进程名未知，保守返回 true 避免误停止
            Debug.WriteLine($"[Telemetry] 未找到 GameId={gameId} 的进程名映射，跳过进程检测");
            return true;
        }

        foreach (var name in processNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                {
                    foreach (var p in procs) p.Dispose();
                    return true;
                }
            }
            catch
            {
                // 权限问题或进程已退出，继续检查下一个名称
            }
        }

        return false;
    }

    private void ProcessFrame(TelemetryAPI.NormalizedData data)
    {
        try
        {
            // 计算模拟时间戳（自启动以来的毫秒数）
            var timestampMs = (uint)Stopwatch.GetElapsedTime(_telemetryStartTick).TotalMilliseconds;

            // 构建三个数据包 (0x6101~0x6103)
            var packets = TelemetryPacketBuilder.BuildAllPackets(data, timestampMs);

            OnPacketsBuilt?.Invoke(packets);

            // 下发到基座设备
            DispatchPackets(packets);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Telemetry] 帧处理异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 自适应最大转速追踪。
    /// LFS、RBR、BeamNG 三款游戏遥测协议不提供 maxRpm 字段（始终为 0）。
    /// 启动时使用默认值 6000 RPM，随后追踪 rpm 峰值作为 maxRpm；
    /// 当 rpm 连续 5 秒为 0 时重置为默认值（可能更换了车辆）。
    /// </summary>
    private void ApplyAdaptiveMaxRpm(ref TelemetryAPI.NormalizedData data)
    {
        if (data.maxRpm > 0) return; // 游戏本身提供 maxRpm，不需要追踪

        if (data.rpm > 0)
        {
            _rpmZeroFrameCount = 0;

            if (data.rpm > _trackedMaxRpm)
            {
                _trackedMaxRpm = data.rpm;
            }
        }
        else
        {
            _rpmZeroFrameCount++;

            if (_rpmZeroFrameCount >= RpmZeroResetFrames)
            {
                // 恢复默认值，下次有转速时重新追踪
                _trackedMaxRpm = DefaultMaxRpm;
                _rpmZeroFrameCount = 0;
            }
        }

        data.maxRpm = _trackedMaxRpm;
    }

    // ════════════════════════════════════════════════════════════════
    //  USB 广播下发
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 向所有已连接的设备广播遥测数据包（共 3 包：0x6101~0x6103）。
    /// 基座、面盘、踏板可能各自独立直连到电脑，不是只能通过基座中转。
    /// </summary>
    private void DispatchPackets(byte[][] packets)
    {
        var manager = App.UsbManager;
        if (manager is not { IsRunning: true })
        {
            Debug.WriteLine("[Telemetry] USB Manager 未运行，跳过数据下发");
            return;
        }

        var devices = manager.ConnectedDevices;
        if (devices is not { Count: > 0 })
        {
            Debug.WriteLine("[Telemetry] 无已连接设备，跳过数据下发");
            return;
        }

        // 只向处于正常模式的设备广播（跳过更新模式）
        var targetDevices = devices.Where(d =>
        {
            var descriptor = Models.Usb.DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
            return descriptor != null && descriptor.IsNormalMode(d.Vid, d.Pid);
        }).ToList();

        if (targetDevices.Count == 0)
        {
            Debug.WriteLine("[Telemetry] 无正常模式设备，跳过数据下发");
            return;
        }

        foreach (var device in targetDevices)
        {
            foreach (var packet in packets)
            {
                if (!manager.SendToDevice(device.DeviceKey, packet))
                {
                    Debug.WriteLine($"[Telemetry] 下发失败 → {device.DeviceKey}");
                    break; // 该设备发送失败，跳过剩余包，继续下一个设备
                }
            }
        }

        var timestampMs = packets.Length > 0
            ? BitConverter.ToUInt32(packets[0].AsSpan(3, 4))
            : 0;
        OnPacketsDispatched?.Invoke(timestampMs);
    }

    // ════════════════════════════════════════════════════════════════
    //  IDisposable
    // ════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        Stop();
        Debug.WriteLine("[Telemetry] TelemetryService 已释放");
    }
}
