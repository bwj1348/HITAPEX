using System.Collections.Generic;

namespace HITAPEX.Models.Usb;

/// <summary>
/// 统一的 USB 设备注册表，集中管理所有已知设备的 VID/PID、设备类型和型号信息。
/// 新增设备型号只需在此注册即可。
/// </summary>
public static class DeviceRegistry
{
    private static readonly List<DeviceDescriptor> _devices = new()
    {
        new DeviceDescriptor
        {
            ModelName = "A1踏板",
            DeviceType = DeviceType.Pedal,
            NormalMode = new VidPidPair(0xFF3F, 0x0002),
            UpdateMode = new VidPidPair(0xFF3F, 0xF002),
        },
        new DeviceDescriptor
        {
            ModelName = "A1面盘",
            DeviceType = DeviceType.Wheel,
            NormalMode = new VidPidPair(0xFF86, 0xFF0C),
            UpdateMode = new VidPidPair(0xFF86, 0xFF0D),
        },
        new DeviceDescriptor
        {
            ModelName = "A1基座",
            DeviceType = DeviceType.Base,
            NormalMode = new VidPidPair(0x1A86, 0xFE0C),
            UpdateMode = new VidPidPair(0x1A86, 0xFE0D),
        },
        // 后续新增设备在此注册，例如：
        // new DeviceDescriptor
        // {
        //     ModelName = "A2踏板",
        //     DeviceType = DeviceType.Pedal,
        //     NormalMode = new VidPidPair(0xFF3F, 0x0003),
        //     UpdateMode = new VidPidPair(0xFF3F, 0xF003),
        // },
    };

    /// <summary>所有已注册的设备描述符</summary>
    public static IReadOnlyList<DeviceDescriptor> Devices => _devices.AsReadOnly();

    /// <summary>获取所有需要监听的 VID/PID（含正常模式和更新模式）</summary>
    public static IEnumerable<VidPidPair> GetAllVidPids()
    {
        foreach (var dev in _devices)
        {
            yield return dev.NormalMode;
            yield return dev.UpdateMode;
        }
    }

    /// <summary>根据 VID/PID 查找匹配的设备描述符</summary>
    public static DeviceDescriptor? FindByVidPid(int vid, int pid)
    {
        return _devices.Find(d => d.Matches(vid, pid));
    }

    /// <summary>判断给定的 VID/PID 是否处于更新模式</summary>
    public static bool IsUpdateMode(int vid, int pid)
    {
        return _devices.Exists(d => d.IsUpdateMode(vid, pid));
    }

    /// <summary>根据 VID/PID 获取设备类型</summary>
    public static DeviceType GetDeviceType(int vid, int pid)
    {
        return FindByVidPid(vid, pid)?.DeviceType ?? DeviceType.Unknown;
    }

    /// <summary>根据 VID/PID 获取设备显示名称</summary>
    public static string GetDisplayName(int vid, int pid)
    {
        var dev = FindByVidPid(vid, pid);
        if (dev == null) return "未知设备";
        return dev.IsUpdateMode(vid, pid)
            ? $"{dev.ModelName} (更新模式)"
            : dev.ModelName;
    }

    /// <summary>注册新设备（用于运行时动态添加，如通过 API 发现的新型号）</summary>
    public static void Register(DeviceDescriptor descriptor)
    {
        if (!_devices.Exists(d => d.Matches(descriptor.NormalMode.Vid, descriptor.NormalMode.Pid)))
        {
            _devices.Add(descriptor);
        }
    }
}
