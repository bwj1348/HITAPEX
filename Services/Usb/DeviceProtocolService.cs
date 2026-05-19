using System.Collections.Concurrent;
using System.Diagnostics;
using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

public class DeviceProtocolService
{
    private readonly IUsbSerialManager _manager;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<byte[]?>> _pendingCommands = new();

    private const int FrameSize = 64;
    private const int DefaultResponseTimeoutMs = 3000;

    public DeviceProtocolService(IUsbSerialManager manager)
    {
        _manager = manager;
        _manager.RawDataReceived += OnRawDataReceived;
    }

    private void OnRawDataReceived(UsbDeviceInfo device, byte[] data)
    {
        Debug.WriteLine($"[Protocol] 收到数据 [{device.DeviceKey}]: {BitConverter.ToString(data)}");

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
    /// Clear any pending command for a device key.
    /// </summary>
    public void ClearPendingCommand(string deviceKey)
    {
        if (_pendingCommands.TryRemove(deviceKey, out var tcs))
        {
            tcs.TrySetCanceled();
        }
    }
}
