using System.Collections.Concurrent;
using System.Diagnostics;
using HITAPEX.Models;
using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

/// <summary>
/// 固件更新流程阶段枚举。
/// </summary>
public enum FirmwareUpdatePhase
{
    /// <summary>空闲，未开始更新</summary>
    Idle,
    /// <summary>检查设备当前处于正常模式还是更新模式</summary>
    CheckingMode,
    /// <summary>发送切换命令，使设备从正常模式进入更新模式</summary>
    SwitchingToUpdateMode,
    /// <summary>等待设备以更新模式重新连接</summary>
    WaitingForUpdateModeDevice,
    /// <summary>发送更新开始命令</summary>
    StartingUpdate,
    /// <summary>传输固件数据包</summary>
    TransferringData,
    /// <summary>发送更新完成命令，设备执行固件写入和校验</summary>
    CompletingUpdate,
    /// <summary>等待设备重启到正常模式</summary>
    WaitingForNormalModeDevice,
    /// <summary>更新成功完成</summary>
    Success,
    /// <summary>更新失败</summary>
    Failed,
    /// <summary>更新被取消</summary>
    Cancelled
}

/// <summary>
/// 固件更新进度信息。
/// </summary>
public class FirmwareUpdateProgress
{
    /// <summary>当前更新阶段</summary>
    public FirmwareUpdatePhase Phase { get; set; } = FirmwareUpdatePhase.Idle;
    /// <summary>状态描述文本（用于 UI 显示）</summary>
    public string StatusMessage { get; set; } = "";
    /// <summary>当前传输的数据包序号</summary>
    public int CurrentPacket { get; set; }
    /// <summary>总数据包数</summary>
    public int TotalPackets { get; set; }
    /// <summary>传输进度百分比（0-100）</summary>
    public int ProgressPercent => TotalPackets > 0 ? (int)(CurrentPacket * 100L / TotalPackets) : 0;
    /// <summary>错误信息（失败时设置）</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>设备唯一标识</summary>
    public string DeviceKey { get; set; } = "";
    /// <summary>设备显示名称</summary>
    public string DeviceName { get; set; } = "";
}

/// <summary>
/// 固件更新结果。
/// </summary>
public class FirmwareUpdateResult
{
    /// <summary>更新是否成功</summary>
    public bool Success { get; set; }
    /// <summary>失败时的错误信息</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>新固件版本号</summary>
    public string NewVersion { get; set; } = "";
}

/// <summary>
/// 固件更新服务 —— 执行完整的设备固件更新流程。
/// 流程包括：模式检测 → 切换到更新模式 → 等待重连 → 发送开始命令 →
/// 传输数据包 → 发送完成命令 → 设备重启。
/// </summary>
/// <remarks>
/// 协议参考：docs/乘游直驱方向盘与PC软件usb通信协议 v0.1.md 第6节"固件更新帧说明"。
/// 支持设备类型：基座（WheelDeviceCommand=0x7913）、踏板（PedalDeviceCommand=0x7A14）。
/// 数据包大小：MaxFirmwareChunkSize=54 字节，连续 10 次超时自动中止。
/// </remarks>
public class FirmwareUpdateService
{
    private readonly IUsbSerialManager _manager;
    private readonly DeviceProtocolService _protocol;

    private const int MaxFirmwareChunkSize = 54;
    private const int DataPacketTimeoutMs = 1500;
    private const int CompleteCommandTimeoutMs = 5000;
    private const int MaxConsecutiveTimeouts = 10;
    private const int DeviceReconnectTimeoutMs = 10000;

    private CancellationTokenSource? _currentUpdateCts;
    private readonly object _updateLock = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<UsbDeviceInfo>> _deviceWaiters = new();

    /// <summary>是否正在执行固件更新</summary>
    public bool IsUpdating
    {
        get
        {
            var cts = _currentUpdateCts;
            return cts != null && !cts.IsCancellationRequested;
        }
    }

    /// <summary>固件更新进度变化事件</summary>
    public event Action<FirmwareUpdateProgress>? ProgressChanged;
    /// <summary>调试日志事件（用于 UI 日志面板）</summary>
    public event Action<string>? DebugLog;

    // 协议命令码 — 固件更新专用，由 FirmwareUpdateService 管理
    public const int SwitchModeSubCommand = 0x2026;
    public const int WheelDeviceCommand = 0x7913;
    public const int PedalDeviceCommand = 0x7A14;
    public const int SwitchWheelCommand = 0xF3F3;
    public const int SwitchPedalCommand = 0xF4F4;
    public const int BluetoothDeviceCommand = 0x7711;
    public const int MainChipCore1Command = 0x5634;
    public const int MainChipCore2Command = 0x7812;

    /// <summary>
    /// 初始化固件更新服务。
    /// </summary>
    /// <param name="manager">USB 串口管理器，用于设备通信</param>
    /// <param name="protocol">设备协议服务，用于构建/解析命令帧</param>
    public FirmwareUpdateService(IUsbSerialManager manager, DeviceProtocolService protocol)
    {
        _manager = manager;
        _protocol = protocol;
        _manager.DeviceConnected += OnDeviceConnected;
    }

    private void OnDeviceConnected(UsbDeviceInfo device)
    {
        DebugLog?.Invoke($"设备已连接: {device.DeviceKey} (VID={device.Vid:X4}, PID={device.Pid:X4})");

        // 通知所有等待更新模式设备的等待者
        if (DeviceRegistry.IsUpdateMode(device.Vid, device.Pid))
        {
            foreach (var kvp in _deviceWaiters)
            {
                if (!kvp.Value.Task.IsCompleted)
                {
                    kvp.Value.TrySetResult(device);
                    break;
                }
            }
        }
    }

    private void ReportProgress(FirmwareUpdateProgress progress)
    {
        Debug.WriteLine($"[FirmwareUpdate] {progress.Phase}: {progress.StatusMessage} ({progress.ProgressPercent}%)");
        DebugLog?.Invoke($"[{progress.Phase}] {progress.StatusMessage}");
        ProgressChanged?.Invoke(progress);
    }

    /// <summary>
    /// 根据 VID/PID 从 DeviceRegistry 查设备类型，返回对应的固件更新设备命令码。
    /// </summary>
    public static int GetDeviceCommandForVid(int vid, int pid)
    {
        var deviceType = DeviceRegistry.GetDeviceType(vid, pid);
        return deviceType == DeviceType.Pedal ? PedalDeviceCommand : WheelDeviceCommand;
    }

    /// <summary>
    /// 根据 VID/PID 从 DeviceRegistry 查设备类型，构建切换更新模式的命令帧。
    /// </summary>
    public static byte[] GetSwitchModeCommandForVid(int vid, int pid)
    {
        var deviceType = DeviceRegistry.GetDeviceType(vid, pid);
        var cmd = deviceType == DeviceType.Pedal ? SwitchPedalCommand : SwitchWheelCommand;

        var frame = new byte[64];
        frame[0] = (byte)(cmd & 0xFF);
        frame[1] = (byte)((cmd >> 8) & 0xFF);
        frame[2] = (byte)(SwitchModeSubCommand & 0xFF);
        frame[3] = (byte)((SwitchModeSubCommand >> 8) & 0xFF);
        return frame;
    }

    /// <summary>
    /// Determine the device type based on VID/PID (from DeviceRegistry).
    /// </summary>
    public static DeviceType GetDeviceTypeFromVidPid(int vid, int pid)
    {
        return DeviceRegistry.GetDeviceType(vid, pid);
    }

    /// <summary>
    /// Get device display name based on VID/PID (from DeviceRegistry).
    /// </summary>
    public static string GetDeviceDisplayName(int vid, int pid)
    {
        return DeviceRegistry.GetDisplayName(vid, pid);
    }

    /// <summary>
    /// Check if a device is in update mode based on its PID (from DeviceRegistry).
    /// </summary>
    public static bool IsUpdateMode(int vid, int pid)
    {
        return DeviceRegistry.IsUpdateMode(vid, pid);
    }

    /// <summary>
    /// Execute the complete firmware update flow.
    /// </summary>
    public async Task<FirmwareUpdateResult> UpdateFirmwareAsync(
        UsbDeviceInfo device,
        FirmwareVersionInfo firmwareInfo,
        byte[] firmwareData,
        CancellationToken ct = default)
    {
        _currentUpdateCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linkedCt = _currentUpdateCts.Token;

        var progress = new FirmwareUpdateProgress
        {
            DeviceKey = device.DeviceKey,
            DeviceName = firmwareInfo.DeviceName
        };

        try
        {
            var deviceCommand = GetDeviceCommandForVid(device.Vid, device.Pid);
            var deviceName = GetDeviceDisplayName(device.Vid, device.Pid);

            // Step 1: Check if device is in update mode or normal mode
            progress.Phase = FirmwareUpdatePhase.CheckingMode;
            progress.StatusMessage = $"正在检查{deviceName}模式...";
            ReportProgress(progress);

            var currentDevice = device;
            var inUpdateMode = IsUpdateMode(currentDevice.Vid, currentDevice.Pid);

            // Step 2: If normal mode, switch to update mode
            if (!inUpdateMode)
            {
                progress.Phase = FirmwareUpdatePhase.SwitchingToUpdateMode;
                progress.StatusMessage = $"正在切换{deviceName}到更新模式...";
                ReportProgress(progress);

                var switchCmd = GetSwitchModeCommandForVid(currentDevice.Vid, currentDevice.Pid);
                DebugLog?.Invoke($"发送切换模式命令 -> {deviceName}: {BitConverter.ToString(switchCmd.Take(4).ToArray())}");

                _manager.SendToDevice(currentDevice.DeviceKey, switchCmd);

                // Wait for device to reconnect in update mode
                progress.Phase = FirmwareUpdatePhase.WaitingForUpdateModeDevice;
                progress.StatusMessage = "等待设备以更新模式重新连接...";
                ReportProgress(progress);

                var waiter = new TaskCompletionSource<UsbDeviceInfo>();
                _deviceWaiters.TryAdd("update_mode", waiter);

                var timeoutTask = Task.Delay(DeviceReconnectTimeoutMs, linkedCt);
                var completedTask = await Task.WhenAny(waiter.Task, timeoutTask);

                _deviceWaiters.TryRemove("update_mode", out _);

                if (completedTask == timeoutTask || linkedCt.IsCancellationRequested)
                {
                    return new FirmwareUpdateResult
                    {
                        Success = false,
                        ErrorMessage = "等待设备进入更新模式超时，请检查设备连接"
                    };
                }

                currentDevice = await waiter.Task;
                DebugLog?.Invoke($"设备已进入更新模式: {currentDevice.DeviceKey}");
            }

            // Step 3: Send update start command
            progress.Phase = FirmwareUpdatePhase.StartingUpdate;
            progress.StatusMessage = "正在发送更新开始命令...";
            ReportProgress(progress);

            var startCmd = DeviceProtocolService.BuildUpdateStartCommand(deviceCommand);
            var startResponse = await _protocol.SendCommandAsync(currentDevice.DeviceKey, startCmd, 3000);

            if (startResponse == null)
            {
                DebugLog?.Invoke("未收到更新开始回复，3秒后重试...");
                await Task.Delay(3000, linkedCt);
                startResponse = await _protocol.SendCommandAsync(currentDevice.DeviceKey, startCmd, 3000);
            }

            if (startResponse == null)
            {
                return new FirmwareUpdateResult
                {
                    Success = false,
                    ErrorMessage = "设备未响应更新开始命令"
                };
            }

            var startStatus = DeviceProtocolService.ParseUpdateStartResponse(startResponse, deviceCommand);
            DebugLog?.Invoke($"更新开始回复状态: {startStatus}");

            if (startStatus != 0)
            {
                var errorMsg = startStatus == 3 ? "擦除FLASH失败" : $"未知失败状态: {startStatus}";
                return new FirmwareUpdateResult { Success = false, ErrorMessage = $"更新开始失败: {errorMsg}" };
            }

            // Step 4: Send firmware data
            progress.Phase = FirmwareUpdatePhase.TransferringData;
            var totalPackets = (firmwareData.Length + MaxFirmwareChunkSize - 1) / MaxFirmwareChunkSize;
            progress.TotalPackets = totalPackets;
            progress.CurrentPacket = 0;
            progress.StatusMessage = $"正在传输固件数据 (0/{totalPackets})...";
            ReportProgress(progress);

            var consecutiveTimeouts = 0;

            for (int dataIndex = 0; dataIndex < firmwareData.Length; dataIndex += MaxFirmwareChunkSize)
            {
                linkedCt.ThrowIfCancellationRequested();

                var chunkSize = Math.Min(MaxFirmwareChunkSize, firmwareData.Length - dataIndex);
                var chunk = new byte[chunkSize];
                Array.Copy(firmwareData, dataIndex, chunk, 0, chunkSize);

                var dataCmd = DeviceProtocolService.BuildFirmwareDataCommand(deviceCommand, dataIndex, chunk);
                var dataResponse = await _protocol.SendCommandAsync(currentDevice.DeviceKey, dataCmd, DataPacketTimeoutMs);

                if (dataResponse != null)
                {
                    consecutiveTimeouts = 0;
                    var receivedCount = DeviceProtocolService.ParseFirmwareDataResponse(dataResponse, deviceCommand);
                    DebugLog?.Invoke($"固件数据包 {progress.CurrentPacket + 1}/{totalPackets}: 索引={dataIndex}, 设备已收到={receivedCount}");
                }
                else
                {
                    consecutiveTimeouts++;
                    DebugLog?.Invoke($"固件数据包 {progress.CurrentPacket + 1}/{totalPackets}: 无回复 (连续超时: {consecutiveTimeouts})");

                    if (consecutiveTimeouts >= MaxConsecutiveTimeouts)
                    {
                        return new FirmwareUpdateResult
                        {
                            Success = false,
                            ErrorMessage = $"连续 {MaxConsecutiveTimeouts} 个数据包未收到回复，更新已停止"
                        };
                    }
                }

                progress.CurrentPacket++;
                progress.StatusMessage = $"正在传输固件数据 ({progress.CurrentPacket}/{totalPackets})...";
                ReportProgress(progress);
            }

            // Step 5: Send complete command
            progress.Phase = FirmwareUpdatePhase.CompletingUpdate;
            progress.StatusMessage = "正在完成固件更新...";
            ReportProgress(progress);

            var completeCmd = DeviceProtocolService.BuildUpdateCompleteCommand(deviceCommand);
            var completeResponse = await _protocol.SendCommandAsync(currentDevice.DeviceKey, completeCmd, CompleteCommandTimeoutMs);

            if (completeResponse == null)
            {
                DebugLog?.Invoke("未收到完成回复，5秒后重试...");
                await Task.Delay(5000, linkedCt);
                completeResponse = await _protocol.SendCommandAsync(currentDevice.DeviceKey, completeCmd, CompleteCommandTimeoutMs);
            }

            if (completeResponse == null)
            {
                return new FirmwareUpdateResult
                {
                    Success = false,
                    ErrorMessage = "设备未响应更新完成命令"
                };
            }

            var completeStatus = DeviceProtocolService.ParseUpdateCompleteResponse(completeResponse, deviceCommand);
            DebugLog?.Invoke($"更新完成回复状态: {completeStatus}");

            if (completeStatus != 0)
            {
                var errorMsg = completeStatus switch
                {
                    1 => "固件数据长度不对",
                    2 => "固件数据校验不对",
                    3 => "擦除失败",
                    _ => $"未知失败状态: {completeStatus}"
                };
                return new FirmwareUpdateResult { Success = false, ErrorMessage = $"更新失败: {errorMsg}" };
            }

            // Update successful - device will reset to normal mode
            progress.Phase = FirmwareUpdatePhase.WaitingForNormalModeDevice;
            progress.StatusMessage = "更新成功，等待设备重启到正常模式...";
            ReportProgress(progress);

            progress.Phase = FirmwareUpdatePhase.Success;
            progress.StatusMessage = "固件更新成功";
            ReportProgress(progress);

            return new FirmwareUpdateResult
            {
                Success = true,
                NewVersion = firmwareInfo.Version
            };
        }
        catch (OperationCanceledException)
        {
            progress.Phase = FirmwareUpdatePhase.Cancelled;
            progress.StatusMessage = "更新已取消";
            ReportProgress(progress);

            return new FirmwareUpdateResult { Success = false, ErrorMessage = "更新已取消" };
        }
        catch (Exception ex)
        {
            DebugLog?.Invoke($"更新异常: {ex.Message}");
            progress.Phase = FirmwareUpdatePhase.Failed;
            progress.StatusMessage = $"更新失败: {ex.Message}";
            progress.ErrorMessage = ex.Message;
            ReportProgress(progress);

            return new FirmwareUpdateResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            _currentUpdateCts?.Dispose();
            _currentUpdateCts = null;
        }
    }

    /// <summary>
    /// Get device info from a connected device.
    /// Sends the Get Device Info command and parses the response.
    /// </summary>
    public async Task<DeviceInfoResponse?> GetDeviceInfoAsync(UsbDeviceInfo device, DeviceType deviceType)
    {
        DebugLog?.Invoke($"正在获取设备信息: {device.DeviceKey} (类型={deviceType})");

        var cmd = DeviceProtocolService.BuildGetDeviceInfoCommand(deviceType);
        var response = await _protocol.SendCommandAsync(device.DeviceKey, cmd);

        if (response == null)
        {
            DebugLog?.Invoke($"获取设备信息无响应: {device.DeviceKey}");
            return null;
        }

        return DeviceProtocolService.ParseDeviceInfoResponse(response);
    }

    /// <summary>
    /// Compare two firmware version strings (e.g., "v1.0" vs "1.0").
    /// Returns true if apiVersion is newer than deviceVersion.
    /// </summary>
    public static bool IsNewerVersion(string deviceVersion, string apiVersion)
    {
        if (string.IsNullOrWhiteSpace(apiVersion))
            return false;

        var apiVer = apiVersion.TrimStart('v', 'V');
        var devVer = deviceVersion.TrimStart('v', 'V');

        if (Version.TryParse(apiVer, out var apiV) && Version.TryParse(devVer, out var devV))
        {
            return apiV > devV;
        }

        return string.Compare(apiVer, devVer, StringComparison.OrdinalIgnoreCase) > 0;
    }

    public void CancelUpdate()
    {
        _currentUpdateCts?.Cancel();
    }
}
