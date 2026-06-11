using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

public class DeviceProtocolService
{
    private readonly IUsbSerialManager _manager;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<byte[]?>> _pendingCommands = new();

    // 多包预设名称收集状态
    private record class PresetNameCollectionState(
        List<PresetNameResponse> Packets,
        TaskCompletionSource<string?> Tcs,
        CancellationTokenSource Cts);

    private readonly ConcurrentDictionary<string, PresetNameCollectionState> _presetNameCollections = new();

    private const int FrameSize = 64;
    private const int DefaultResponseTimeoutMs = 3000;
    private const int PresetNameMaxBytes = 512;
    private const int PresetNameChunkSize = 56;

    public DeviceProtocolService(IUsbSerialManager manager)
    {
        _manager = manager;
        _manager.RawDataReceived += OnRawDataReceived;
    }

    private void OnRawDataReceived(UsbDeviceInfo device, byte[] data)
    {
        Debug.WriteLine($"[Protocol] 收到数据 [{device.DeviceKey}]: {BitConverter.ToString(data)}");

        // 优先处理多包预设名称收集
        if (_presetNameCollections.TryGetValue(device.DeviceKey, out var collection))
        {
            var namePacket = ParsePresetNameResponse(data);
            if (namePacket != null)
            {
                lock (collection.Packets)
                {
                    // 首个包直接加入，后续包需校验 DeviceType 一致
                    if (collection.Packets.Count > 0 && namePacket.DeviceType != collection.Packets[0].DeviceType)
                        return;

                    collection.Packets.Add(namePacket);
                }

                // 检查是否已收齐所有包
                var totalLen = collection.Packets[0].TotalLength;
                var expectedPackets = Math.Max(1, (totalLen + PresetNameChunkSize - 1) / PresetNameChunkSize);
                if (collection.Packets.Count >= expectedPackets)
                {
                    if (_presetNameCollections.TryRemove(device.DeviceKey, out _))
                    {
                        var name = PresetNameResponse.DecodeNameFromPackets(collection.Packets);
                        collection.Tcs.TrySetResult(name);
                        collection.Cts.Cancel();
                    }
                }
                return;
            }
        }

        if (_pendingCommands.TryGetValue(device.DeviceKey, out var tcs))
        {
            if (!tcs.Task.IsCompleted)
            {
                tcs.TrySetResult(data);
            }
        }
    }

    /// <summary>
    /// Build Get Device Info command frame (64 bytes).
    /// Protocol: [0x81, 0x01, 0x81, deviceType, 0x00...]
    /// </summary>
    public static byte[] BuildGetDeviceInfoCommand(DeviceType deviceType)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x81;          // Get command
        frame[1] = 0x01;          // 0x8101 LE low byte
        frame[2] = 0x81;          // 0x8101 LE high byte
        frame[3] = (byte)deviceType;
        return frame;
    }

    /// <summary>
    /// Parse device info response from raw data.
    /// Expected response format:
    /// [0xC1, 0x01, 0x81, deviceType, usbSpeed, fwLow, fwHigh, bootLow, bootHigh, ...]
    /// When deviceType is Base, extended fields (wheel/pedal connection status & firmware) are parsed.
    /// </summary>
    public static DeviceInfoResponse? ParseDeviceInfoResponse(byte[] data)
    {
        if (data == null || data.Length < 9)
            return null;

        if (data[0] != 0xC1 || data[1] != 0x01 || data[2] != 0x81)
            return null;

        var response = new DeviceInfoResponse
        {
            DeviceType = (DeviceType)data[3],
            UsbSpeed = data[4],
            NormalFirmwareVersion = data[5] | (data[6] << 8),
            BootFirmwareVersion = data[7] | (data[8] << 8)
        };

        // 解析基座特有字段（面盘/踏板连接状态及固件版本）
        if (response.DeviceType == DeviceType.Base && data.Length >= 19)
        {
            response.WheelConnectionStatus = data[9];
            response.WheelNormalFwVersion = data[10] | (data[11] << 8);
            response.WheelBootFwVersion = data[12] | (data[13] << 8);
            response.PedalConnectionStatus = data[14];
            response.PedalNormalFwVersion = data[15] | (data[16] << 8);
            response.PedalBootFwVersion = data[17] | (data[18] << 8);
        }

        // 踏板个数（offset 30，所有设备类型通用）
        if (data.Length > 30)
            response.PedalCount = data[30];

        Debug.WriteLine($"[Protocol] 解析设备信息: {response}");
        return response;
    }

    /// <summary>
    /// Build firmware update start command.
    /// Protocol: [0x80, 0x01, deviceCmdLow, deviceCmdHigh, 0x00...] (0x0180 + device command LE)
    /// </summary>
    public static byte[] BuildUpdateStartCommand(int deviceCommand)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x80;
        frame[1] = 0x01;          // 0x0180 LE
        frame[2] = (byte)(deviceCommand & 0xFF);
        frame[3] = (byte)((deviceCommand >> 8) & 0xFF);
        return frame;
    }

    /// <summary>
    /// Build firmware data packet.
    /// Protocol: [0x80, 0x00, deviceCmdLow, deviceCmdHigh, index(4B LE), len(2B LE), data...]
    /// </summary>
    public static byte[] BuildFirmwareDataCommand(int deviceCommand, int dataIndex, byte[] firmwareChunk)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x80;
        frame[1] = 0x00;          // 0x0080 LE
        frame[2] = (byte)(deviceCommand & 0xFF);
        frame[3] = (byte)((deviceCommand >> 8) & 0xFF);
        // Data index: 4 bytes LE at offset 4
        frame[4] = (byte)(dataIndex & 0xFF);
        frame[5] = (byte)((dataIndex >> 8) & 0xFF);
        frame[6] = (byte)((dataIndex >> 16) & 0xFF);
        frame[7] = (byte)((dataIndex >> 24) & 0xFF);
        // Data length: 2 bytes LE at offset 8
        var len = (ushort)firmwareChunk.Length;
        frame[8] = (byte)(len & 0xFF);
        frame[9] = (byte)((len >> 8) & 0xFF);
        // Firmware data at offset 10
        Array.Copy(firmwareChunk, 0, frame, 10, firmwareChunk.Length);
        return frame;
    }

    /// <summary>
    /// Build firmware update complete command.
    /// Protocol: [0x80, 0x03, deviceCmdLow, deviceCmdHigh, 0x00...] (0x0380 + device command LE)
    /// </summary>
    public static byte[] BuildUpdateCompleteCommand(int deviceCommand)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x80;
        frame[1] = 0x03;          // 0x0380 LE
        frame[2] = (byte)(deviceCommand & 0xFF);
        frame[3] = (byte)((deviceCommand >> 8) & 0xFF);
        return frame;
    }

    /// <summary>
    /// Parse update start response.
    /// Expected: [0xC0, 0x01, devCmdLow, devCmdHigh, status, ...]
    /// Returns status byte, or -1 if invalid.
    /// </summary>
    public static int ParseUpdateStartResponse(byte[] data, int expectedDeviceCommand)
    {
        if (data == null || data.Length < 5)
            return -1;

        if (data[0] != 0xC0 || data[1] != 0x01)
            return -1;

        var devCmd = data[2] | (data[3] << 8);
        if (devCmd != expectedDeviceCommand)
            return -1;

        return data[4];
    }

    /// <summary>
    /// Parse firmware data response.
    /// Expected: [0xC0, 0x00, devCmdLow, devCmdHigh, receivedCount(4B LE), ...]
    /// Returns received count, or -1 if invalid.
    /// </summary>
    public static int ParseFirmwareDataResponse(byte[] data, int expectedDeviceCommand)
    {
        if (data == null || data.Length < 8)
            return -1;

        if (data[0] != 0xC0 || data[1] != 0x00)
            return -1;

        var devCmd = data[2] | (data[3] << 8);
        if (devCmd != expectedDeviceCommand)
            return -1;

        return data[4] | (data[5] << 8) | (data[6] << 16) | (data[7] << 24);
    }

    /// <summary>
    /// Parse update complete response.
    /// Expected: [0xC0, 0x03, devCmdLow, devCmdHigh, status, ...]
    /// Returns status byte, or -1 if invalid.
    /// </summary>
    public static int ParseUpdateCompleteResponse(byte[] data, int expectedDeviceCommand)
    {
        if (data == null || data.Length < 5)
            return -1;

        if (data[0] != 0xC0 || data[1] != 0x03)
            return -1;

        var devCmd = data[2] | (data[3] << 8);
        if (devCmd != expectedDeviceCommand)
            return -1;

        return data[4];
    }

    /// <summary>
    /// Send a command and wait for a response.
    /// Returns the raw response bytes, or null on timeout.
    /// </summary>
    public async Task<byte[]?> SendCommandAsync(string deviceKey, byte[] command, int timeoutMs = DefaultResponseTimeoutMs)
    {
        // Clear any previous pending command for this device
        if (_pendingCommands.TryRemove(deviceKey, out var oldTcs))
        {
            oldTcs.TrySetCanceled();
        }

        var tcs = new TaskCompletionSource<byte[]?>();
        _pendingCommands[deviceKey] = tcs;

        try
        {
            Debug.WriteLine($"[Protocol] 发送命令 [{deviceKey}]: {BitConverter.ToString(command.Take(16).ToArray())}...");

            var ok = _manager.SendToDevice(deviceKey, command);
            if (!ok)
            {
                Debug.WriteLine($"[Protocol] 发送失败 [{deviceKey}]");
                return null;
            }

            var timeoutTask = Task.Delay(timeoutMs);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Debug.WriteLine($"[Protocol] 响应超时 [{deviceKey}] ({timeoutMs}ms)");
                return null;
            }

            return await tcs.Task;
        }
        finally
        {
            _pendingCommands.TryRemove(deviceKey, out _);
        }
    }

    /// <summary>
    /// Build Set Pedal Parameters command frame (64 bytes).
    /// Protocol 0x2110: [0x21, 0x10, 0x21, clutchDir, clutchY1, clutchX1, ... brake... throttle...]
    /// </summary>
    public static byte[] BuildSetPedalParametersCommand(
        byte clutchDir, byte[] clutchPoints, byte clutchDeadZoneFront, byte clutchDeadZoneRear,
        byte brakeDir, byte[] brakePoints, byte brakeDeadZoneFront, byte brakeDeadZoneRear,
        byte throttleDir, byte[] throttlePoints, byte throttleDeadZoneFront, byte throttleDeadZoneRear)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x21;          // Set command
        frame[1] = 0x10;          // 0x2110 LE low byte
        frame[2] = 0x21;          // 0x2110 LE high byte
        frame[3] = clutchDir;
        // Clutch curve: 4 points, each Y then X (bytes 4-11)
        for (int i = 0; i < 8 && i < clutchPoints.Length; i++)
            frame[4 + i] = clutchPoints[i];
        frame[12] = clutchDeadZoneFront;
        frame[13] = clutchDeadZoneRear;
        frame[14] = brakeDir;
        // Brake curve: 4 points, each Y then X (bytes 15-22)
        for (int i = 0; i < 8 && i < brakePoints.Length; i++)
            frame[15 + i] = brakePoints[i];
        frame[23] = brakeDeadZoneFront;
        frame[24] = brakeDeadZoneRear;
        frame[25] = throttleDir;
        // Throttle curve: 4 points, each Y then X (bytes 26-33)
        for (int i = 0; i < 8 && i < throttlePoints.Length; i++)
            frame[26 + i] = throttlePoints[i];
        frame[34] = throttleDeadZoneFront;
        frame[35] = throttleDeadZoneRear;
        return frame;
    }

    /// <summary>
    /// Build Set Base Parameters command frame (64 bytes).
    /// Protocol 0x2101: [0x21, 0x01, 0x21, angleLow, angleHigh, limitRigidity, maxSpeed, smoothLevel,
    ///                     forceStrength, mechInertia, mechCentering, mechDamping, mechFriction,
    ///                     gameInertia, gameElastic, gameDamping, gameFriction, gameInertiaStr,
    ///                     handsOffProtect, forceReverse, reserved...]
    /// </summary>
    public static byte[] BuildSetBaseParametersCommand(
        ushort maxSteeringAngle, byte limitRigidity, byte maxSpeed, byte smoothLevel,
        byte forceStrength, byte mechInertia, byte mechCentering, byte mechDamping,
        byte mechFriction, byte gameInertia, byte gameElastic, byte gameDamping,
        byte gameFriction, byte gameInertiaStr, byte handsOffProtect, byte forceReverse)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x21;          // Set command
        frame[1] = 0x01;          // 0x2101 LE low byte
        frame[2] = 0x21;          // 0x2101 LE high byte
        frame[3] = (byte)(maxSteeringAngle & 0xFF);
        frame[4] = (byte)((maxSteeringAngle >> 8) & 0xFF);
        frame[5] = limitRigidity;
        frame[6] = maxSpeed;
        frame[7] = smoothLevel;
        frame[8] = forceStrength;
        frame[9] = mechInertia;
        frame[10] = mechCentering;
        frame[11] = mechDamping;
        frame[12] = mechFriction;
        frame[13] = gameInertia;
        frame[14] = gameElastic;
        frame[15] = gameDamping;
        frame[16] = gameFriction;
        frame[17] = gameInertiaStr;
        frame[18] = handsOffProtect;
        frame[19] = forceReverse;
        return frame;
    }

    /// <summary>
    /// Build Get Pedal Parameters command frame (64 bytes).
    /// Protocol: [0x81, 0x10, 0x21, 0x00...]
    /// </summary>
    public static byte[] BuildGetPedalParametersCommand()
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x81;          // Get command
        frame[1] = 0x10;          // 0x2110 LE low byte
        frame[2] = 0x21;          // 0x2110 LE high byte
        return frame;
    }

    public const byte CalibrationStart = 1;
    public const byte CalibrationComplete = 2;

    /// <summary>
    /// Build pedal calibration command frame (64 bytes).
    /// Protocol 0x21E1: [0x21, 0xE1, 0x21, clutch, brake, throttle, 0...]
    /// Each axis byte: 1=start, 2=complete, 0=no-op.
    /// </summary>
    public static byte[] BuildPedalCalibrationCommand(byte clutch, byte brake, byte throttle)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x21;          // Set command
        frame[1] = 0xE1;          // 0x21E1 LE low byte
        frame[2] = 0x21;          // 0x21E1 LE high byte
        frame[3] = clutch;
        frame[4] = brake;
        frame[5] = throttle;
        return frame;
    }

    /// <summary>
    /// Parse pedal parameters response.
    /// Expected: [0xC1, 0x10, 0x21, clutchDir, clutch4pts(8B), clutchDeadFront, clutchDeadRear,
    ///            brakeDir, brake4pts(8B), brakeDeadFront, brakeDeadRear,
    ///            throttleDir, throttle4pts(8B), throttleDeadFront, throttleDeadRear, ...]
    /// </summary>
    public static PedalParametersResponse? ParsePedalParametersResponse(byte[] data)
    {
        if (data == null || data.Length < 36)
            return null;

        if (data[0] != 0xC1 || data[1] != 0x10 || data[2] != 0x21)
            return null;

        return new PedalParametersResponse
        {
            ClutchDirection = data[3],
            ClutchPoint1Y = data[4], ClutchPoint1X = data[5],
            ClutchPoint2Y = data[6], ClutchPoint2X = data[7],
            ClutchPoint3Y = data[8], ClutchPoint3X = data[9],
            ClutchPoint4Y = data[10], ClutchPoint4X = data[11],
            ClutchDeadZoneFront = data[12],
            ClutchDeadZoneRear = data[13],
            BrakeDirection = data[14],
            BrakePoint1Y = data[15], BrakePoint1X = data[16],
            BrakePoint2Y = data[17], BrakePoint2X = data[18],
            BrakePoint3Y = data[19], BrakePoint3X = data[20],
            BrakePoint4Y = data[21], BrakePoint4X = data[22],
            BrakeDeadZoneFront = data[23],
            BrakeDeadZoneRear = data[24],
            ThrottleDirection = data[25],
            ThrottlePoint1Y = data[26], ThrottlePoint1X = data[27],
            ThrottlePoint2Y = data[28], ThrottlePoint2X = data[29],
            ThrottlePoint3Y = data[30], ThrottlePoint3X = data[31],
            ThrottlePoint4Y = data[32], ThrottlePoint4X = data[33],
            ThrottleDeadZoneFront = data[34],
            ThrottleDeadZoneRear = data[35],
        };
    }

    /// <summary>
    /// Clear any pending command for a device key.
    /// </summary>
    public void ClearPendingCommand(string deviceKey)
    {
        if (_pendingCommands.TryRemove(deviceKey, out var tcs))
        {
            tcs.TrySetCanceled();
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  面盘转速灯基础模式协议 (0x2103)
    // ════════════════════════════════════════════════════════════════

    /// <summary>预定义的UI颜色索引到RGB的映射表</summary>
    public static readonly byte[][] ColorIndexToRgb =
    {
        new byte[] { 0xC6, 0x0E, 0x0E }, // 0: 红
        new byte[] { 0xFF, 0x6A, 0x00 }, // 1: 橙
        new byte[] { 0xFF, 0xC8, 0x00 }, // 2: 黄
        new byte[] { 0x16, 0xC6, 0x42 }, // 3: 绿
        new byte[] { 0x28, 0xF9, 0xDD }, // 4: 青
        new byte[] { 0x28, 0x40, 0xF9 }, // 5: 蓝
        new byte[] { 0xC1, 0x28, 0xF9 }, // 6: 紫
        new byte[] { 0xEE, 0xEE, 0xEE }, // 7: 白
        new byte[] { 0x00, 0x00, 0x00 }, // 8: 无(灭)
    };

    /// <summary>将RGB字节反查为UI颜色索引，未匹配时返回0（红）</summary>
    public static int RgbToColorIndex(byte r, byte g, byte b)
    {
        for (int i = 0; i < ColorIndexToRgb.Length; i++)
        {
            if (ColorIndexToRgb[i][0] == r && ColorIndexToRgb[i][1] == g && ColorIndexToRgb[i][2] == b)
                return i;
        }
        // 非精确匹配时返回最接近的索引
        if (r == 0 && g == 0 && b == 0) return 8;
        return 0;
    }

    /// <summary>
    /// Build Set Wheel RPM Base Mode command (0x2103).
    /// Protocol: [0x21, 0x03, 0x21, baseMode, speed, LED1_RGB(3B), ... LED12_RGB(3B), reserved...]
    /// </summary>
    public static byte[] BuildSetWheelRpmBaseModeCommand(byte baseMode, byte baseSpeed, byte[][] ledColors)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x21;
        frame[1] = 0x03;
        frame[2] = 0x21;
        frame[3] = baseMode;
        frame[4] = baseSpeed;
        for (int i = 0; i < 12 && i < ledColors.Length; i++)
        {
            var color = ledColors[i];
            if (color != null && color.Length >= 3)
            {
                frame[5 + i * 3] = color[0];
                frame[6 + i * 3] = color[1];
                frame[7 + i * 3] = color[2];
            }
        }
        return frame;
    }

    /// <summary>Build Get Wheel RPM Base Mode command (0x2103).</summary>
    public static byte[] BuildGetWheelRpmBaseModeCommand()
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x81;
        frame[1] = 0x03;
        frame[2] = 0x21;
        return frame;
    }

    /// <summary>Parse Wheel RPM Base Mode response (0x2103).</summary>
    public static WheelRpmBaseModeResponse? ParseWheelRpmBaseModeResponse(byte[] data)
    {
        if (data == null || data.Length < 41)
            return null;
        if (data[0] != 0xC1 || data[1] != 0x03 || data[2] != 0x21)
            return null;

        var response = new WheelRpmBaseModeResponse
        {
            BaseMode = data[3],
            BaseSpeed = data[4]
        };
        for (int i = 0; i < 12; i++)
        {
            response.LedColors[i][0] = data[5 + i * 3];
            response.LedColors[i][1] = data[6 + i * 3];
            response.LedColors[i][2] = data[7 + i * 3];
        }
        return response;
    }

    // ════════════════════════════════════════════════════════════════
    //  面盘转速灯转速指示协议 (0x2104)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Build Set Wheel RPM Indicator command (0x2104).
    /// Protocol: [0x21, 0x04, 0x21, triggerMode, LED1_val(2B), LED1_RGB(3B), ... LED12_val(2B), LED12_RGB(3B)]
    /// </summary>
    public static byte[] BuildSetWheelRpmIndicatorCommand(byte triggerMode, ushort[] triggerValues, byte[][] ledColors)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x21;
        frame[1] = 0x04;
        frame[2] = 0x21;
        frame[3] = triggerMode;
        for (int i = 0; i < 12; i++)
        {
            var offset = 4 + i * 5;
            var val = i < triggerValues.Length ? triggerValues[i] : (ushort)0;
            frame[offset] = (byte)(val & 0xFF);
            frame[offset + 1] = (byte)((val >> 8) & 0xFF);
            if (i < ledColors.Length && ledColors[i] != null && ledColors[i].Length >= 3)
            {
                frame[offset + 2] = ledColors[i][0];
                frame[offset + 3] = ledColors[i][1];
                frame[offset + 4] = ledColors[i][2];
            }
        }
        return frame;
    }

    /// <summary>Build Get Wheel RPM Indicator command (0x2104).</summary>
    public static byte[] BuildGetWheelRpmIndicatorCommand()
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x81;
        frame[1] = 0x04;
        frame[2] = 0x21;
        return frame;
    }

    /// <summary>Parse Wheel RPM Indicator response (0x2104).</summary>
    public static WheelRpmIndicatorResponse? ParseWheelRpmIndicatorResponse(byte[] data)
    {
        if (data == null || data.Length < 64)
            return null;
        if (data[0] != 0xC1 || data[1] != 0x04 || data[2] != 0x21)
            return null;

        var response = new WheelRpmIndicatorResponse
        {
            TriggerMode = data[3]
        };
        for (int i = 0; i < 12; i++)
        {
            var offset = 4 + i * 5;
            response.TriggerValues[i] = (ushort)(data[offset] | (data[offset + 1] << 8));
            response.LedColors[i][0] = data[offset + 2];
            response.LedColors[i][1] = data[offset + 3];
            response.LedColors[i][2] = data[offset + 4];
        }
        return response;
    }

    // ════════════════════════════════════════════════════════════════
    //  面盘转速灯模式等属性协议 (0x2105)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Build Set Wheel RPM Mode command (0x2105).
    /// Protocol: [0x21, 0x05, 0x21, brightness, telemetryOff, lightMode, strobeMode, strobeSpeed, strobeColorRGB(3B), strobeTriggerValue, reserved...]
    /// </summary>
    public static byte[] BuildSetWheelRpmModeCommand(byte brightness, byte telemetryOff, byte lightMode,
        byte strobeMode, byte strobeSpeed, byte strobeColorR, byte strobeColorG, byte strobeColorB, byte strobeTriggerValue)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x21;
        frame[1] = 0x05;
        frame[2] = 0x21;
        frame[3] = brightness;
        frame[4] = telemetryOff;
        frame[5] = lightMode;
        frame[6] = strobeMode;
        frame[7] = strobeSpeed;
        frame[8] = strobeColorR;
        frame[9] = strobeColorG;
        frame[10] = strobeColorB;
        frame[11] = strobeTriggerValue;
        return frame;
    }

    /// <summary>Build Get Wheel RPM Mode command (0x2105).</summary>
    public static byte[] BuildGetWheelRpmModeCommand()
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x81;
        frame[1] = 0x05;
        frame[2] = 0x21;
        return frame;
    }

    /// <summary>Parse Wheel RPM Mode response (0x2105).</summary>
    public static WheelRpmModeResponse? ParseWheelRpmModeResponse(byte[] data)
    {
        if (data == null || data.Length < 12)
            return null;
        if (data[0] != 0xC1 || data[1] != 0x05 || data[2] != 0x21)
            return null;

        return new WheelRpmModeResponse
        {
            Brightness = data[3],
            TelemetryOff = data[4],
            LightMode = data[5],
            StrobeMode = data[6],
            StrobeSpeed = data[7],
            StrobeColorR = data[8],
            StrobeColorG = data[9],
            StrobeColorB = data[10],
            StrobeTriggerValue = data[11]
        };
    }

    // ════════════════════════════════════════════════════════════════
    //  面盘按键灯协议 (0x2107)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Build Set Wheel Button Light command (0x2107).
    /// Protocol: [0x21, 0x07, 0x21, ledMode, ledIndex, brightness, colorRGB(3B), telemetryFunc, flashSpeed, telemetryColorRGB(3B), reserved...]
    /// </summary>
    public static byte[] BuildSetWheelButtonLightCommand(byte ledMode, byte ledIndex, byte brightness,
        byte colorR, byte colorG, byte colorB, byte telemetryFunc, byte flashSpeed,
        byte telemetryColorR, byte telemetryColorG, byte telemetryColorB)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x21;
        frame[1] = 0x07;
        frame[2] = 0x21;
        frame[3] = ledMode;
        frame[4] = ledIndex;
        frame[5] = brightness;
        frame[6] = colorR;
        frame[7] = colorG;
        frame[8] = colorB;
        frame[9] = telemetryFunc;
        frame[10] = flashSpeed;
        frame[11] = telemetryColorR;
        frame[12] = telemetryColorG;
        frame[13] = telemetryColorB;
        return frame;
    }

    /// <summary>Build Get Wheel Button Light command (0x2107), for a specific LED index.</summary>
    public static byte[] BuildGetWheelButtonLightCommand(byte ledIndex)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x81;
        frame[1] = 0x07;
        frame[2] = 0x21;
        frame[3] = ledIndex;
        return frame;
    }

    /// <summary>Parse Wheel Button Light response (0x2107).</summary>
    public static WheelButtonLightResponse? ParseWheelButtonLightResponse(byte[] data)
    {
        if (data == null || data.Length < 14)
            return null;
        if (data[0] != 0xC1 || data[1] != 0x07 || data[2] != 0x21)
            return null;

        return new WheelButtonLightResponse
        {
            LedMode = data[3],
            LedIndex = data[4],
            Brightness = data[5],
            ColorR = data[6],
            ColorG = data[7],
            ColorB = data[8],
            TelemetryFunc = data[9],
            FlashSpeed = data[10],
            TelemetryColorR = data[11],
            TelemetryColorG = data[12],
            TelemetryColorB = data[13]
        };
    }

    // ════════════════════════════════════════════════════════════════
    //  面盘睡眠和拨片协议 (0x2108)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Build Set Wheel Sleep and Paddle command (0x2108).
    /// Protocol: [0x21, 0x08, 0x21, sleepTime, sleepEffect, sleepEffectSpeed, clutchPaddleMode, clutchBitePoint, reserved...]
    /// </summary>
    public static byte[] BuildSetWheelSleepAndPaddleCommand(byte sleepTime, byte sleepEffect, byte sleepEffectSpeed,
        byte clutchPaddleMode, byte clutchBitePoint)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x21;
        frame[1] = 0x08;
        frame[2] = 0x21;
        frame[3] = sleepTime;
        frame[4] = sleepEffect;
        frame[5] = sleepEffectSpeed;
        frame[6] = clutchPaddleMode;
        frame[7] = clutchBitePoint;
        return frame;
    }

    /// <summary>Build Get Wheel Sleep and Paddle command (0x2108).</summary>
    public static byte[] BuildGetWheelSleepAndPaddleCommand()
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x81;
        frame[1] = 0x08;
        frame[2] = 0x21;
        return frame;
    }

    /// <summary>Parse Wheel Sleep and Paddle response (0x2108).</summary>
    public static WheelSleepAndPaddleResponse? ParseWheelSleepAndPaddleResponse(byte[] data)
    {
        if (data == null || data.Length < 8)
            return null;
        if (data[0] != 0xC1 || data[1] != 0x08 || data[2] != 0x21)
            return null;

        return new WheelSleepAndPaddleResponse
        {
            SleepTime = data[3],
            SleepEffect = data[4],
            SleepEffectSpeed = data[5],
            ClutchPaddleMode = data[6],
            ClutchBitePoint = data[7]
        };
    }

    // ════════════════════════════════════════════════════════════════
    //  预设名称协议 (0x21D0)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Build Get Preset Name command frame (64 bytes).
    /// Protocol: [0x81, 0xD0, 0x21, deviceType, reserved...]
    /// </summary>
    public static byte[] BuildGetPresetNameCommand(DeviceType deviceType)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x81;          // Get command
        frame[1] = 0xD0;          // 0x21D0 LE low byte
        frame[2] = 0x21;          // 0x21D0 LE high byte
        frame[3] = (byte)deviceType;
        return frame;
    }

    /// <summary>
    /// Build Set Preset Name command frame for a single packet (64 bytes).
    /// Protocol: [0x21, 0xD0, 0x21, deviceType, totalLen(2B), packetIndex, nameChunk(56B), 0x00]
    /// </summary>
    public static byte[] BuildSetPresetNameCommand(DeviceType deviceType, byte[] nameBytes, int totalLength, int packetIndex)
    {
        var frame = new byte[FrameSize];
        frame[0] = 0x21;          // Set command
        frame[1] = 0xD0;          // 0x21D0 LE low byte
        frame[2] = 0x21;          // 0x21D0 LE high byte
        frame[3] = (byte)deviceType;
        frame[4] = (byte)(totalLength & 0xFF);
        frame[5] = (byte)((totalLength >> 8) & 0xFF);
        frame[6] = (byte)packetIndex;

        var offset = packetIndex * PresetNameChunkSize;
        var chunkSize = Math.Min(PresetNameChunkSize, nameBytes.Length - offset);
        if (chunkSize > 0)
            Array.Copy(nameBytes, offset, frame, 7, chunkSize);

        return frame;
    }

    /// <summary>
    /// Parse a single preset name response packet.
    /// Expected: [0xC1, 0xD0, 0x21, deviceType, totalLen(2B), packetIndex, nameData(up to 56B)]
    /// </summary>
    public static PresetNameResponse? ParsePresetNameResponse(byte[] data)
    {
        if (data == null || data.Length < 7)
            return null;

        if (data[0] != 0xC1 || data[1] != 0xD0 || data[2] != 0x21)
            return null;

        var totalLength = data[4] | (data[5] << 8);
        var packetIndex = data[6];
        var dataLen = Math.Min(PresetNameChunkSize, Math.Max(0, data.Length - 8));
        var nameData = new byte[dataLen];
        if (dataLen > 0)
            Array.Copy(data, 7, nameData, 0, dataLen);

        return new PresetNameResponse
        {
            DeviceType = (DeviceType)data[3],
            TotalLength = totalLength,
            PacketIndex = packetIndex,
            NameData = nameData
        };
    }

    /// <summary>
    /// Send Get Preset Name command and collect multi-packet response.
    /// Returns the decoded UTF-8 name string, or null on timeout/error.
    /// </summary>
    public async Task<string?> GetPresetNameAsync(string deviceKey, DeviceType deviceType, int timeoutMs = DefaultResponseTimeoutMs)
    {
        _presetNameCollections.TryRemove(deviceKey, out _);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource<string?>();
        var packets = new List<PresetNameResponse>();
        var state = new PresetNameCollectionState(packets, tcs, cts);
        _presetNameCollections[deviceKey] = state;

        try
        {
            var cmd = BuildGetPresetNameCommand(deviceType);
            Debug.WriteLine($"[Protocol] 获取预设名称 [{deviceKey}] deviceType={deviceType}");
            var ok = _manager.SendToDevice(deviceKey, cmd);
            if (!ok)
            {
                Debug.WriteLine($"[Protocol] 获取预设名称发送失败 [{deviceKey}]");
                return null;
            }

            var timeoutTask = Task.Delay(timeoutMs, cts.Token);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Debug.WriteLine($"[Protocol] 获取预设名称超时 [{deviceKey}]");
                return null;
            }

            return await tcs.Task;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        finally
        {
            _presetNameCollections.TryRemove(deviceKey, out _);
        }
    }

    /// <summary>
    /// Send Set Preset Name command (potentially multi-packet).
    /// Returns true if all packets were sent successfully.
    /// </summary>
    public bool SetPresetName(string deviceKey, DeviceType deviceType, string name)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);
        if (nameBytes.Length > PresetNameMaxBytes)
        {
            Debug.WriteLine($"[Protocol] 预设名称过长 ({nameBytes.Length} > {PresetNameMaxBytes})");
            return false;
        }

        var totalPackets = (nameBytes.Length + PresetNameChunkSize - 1) / PresetNameChunkSize;
        if (totalPackets == 0) totalPackets = 1;

        Debug.WriteLine($"[Protocol] 设置预设名称 [{deviceKey}] deviceType={deviceType}, name=\"{name}\", packets={totalPackets}");

        for (int i = 0; i < totalPackets; i++)
        {
            var cmd = BuildSetPresetNameCommand(deviceType, nameBytes, nameBytes.Length, i);
            var ok = _manager.SendToDevice(deviceKey, cmd);
            if (!ok)
            {
                Debug.WriteLine($"[Protocol] 设置预设名称发送失败 (packet {i}/{totalPackets})");
                return false;
            }
        }

        return true;
    }
}
