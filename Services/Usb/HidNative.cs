using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HITAPEX.Services.Usb;

/// <summary>
/// Windows HID API 原生互操作定义 — 封装 setupapi.dll、hid.dll、kernel32.dll 的 P/Invoke 声明。
/// 提供 HID 设备枚举（SetupDi* 系列）、属性读取（HidD_* 系列）、能力查询（HidP_GetCaps）
/// 和文件 I/O（CreateFile/ReadFile/CloseHandle）的能力。
/// </summary>
/// <remarks>
/// 仅在 Windows 平台使用，通过 [SupportedOSPlatform("windows")] 约束调用方。
/// 所有结构体使用 LayoutKind.Sequential 保证与原生 API 内存布局一致。
/// </remarks>
internal static class HidNative
{
    // --- CreateFile 访问/共享/标志常量 ---
    public const uint GenericRead = 0x80000000;
    public const uint GenericWrite = 0x40000000;
    public const uint FileShareRead = 0x00000001;
    public const uint FileShareWrite = 0x00000002;
    public const uint OpenExisting = 3;
    public const uint FileFlagOverlapped = 0x40000000;

    // --- SetupDi 标志常量 ---
    public const uint DigcfPresent = 0x00000002;
    public const uint DigcfDeviceInterface = 0x00000010;

    /// <summary>HID 设备接口 GUID</summary>
    public static readonly Guid HidGuid = new("4D1E55B2-F16F-11CF-88CB-001111000030");

    /// <summary>设备接口数据（SP_DEVICE_INTERFACE_DATA）</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SpDeviceInterfaceData
    {
        public int Size;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    /// <summary>设备接口详细信息（SP_DEVICE_INTERFACE_DETAIL_DATA）</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SpDeviceInterfaceDetailData
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DevicePath;
    }

    /// <summary>HID 设备属性（HIDD_ATTRIBUTES）：VID、PID、版本号</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HidAttributes
    {
        public int Size;
        public ushort VendorId;
        public ushort ProductId;
        public ushort VersionNumber;
    }

    /// <summary>HID 设备能力（HIDP_CAPS）：输入/输出/特性报告长度等</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    // ════════════════════════════════════════════════════════════════
    //  setupapi.dll — 设备枚举
    // ════════════════════════════════════════════════════════════════

    /// <summary>创建设备信息集句柄，用于枚举指定 GUID 的设备接口</summary>
    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    /// <summary>枚举设备信息集中的设备接口</summary>
    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid,
        uint memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

    /// <summary>获取设备接口的详细信息（包括设备路径）</summary>
    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet, ref SpDeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize,
        out uint requiredSize, IntPtr deviceInfoData);

    /// <summary>销毁设备信息集句柄</summary>
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    // ════════════════════════════════════════════════════════════════
    //  hid.dll — HID 设备属性/能力查询
    // ════════════════════════════════════════════════════════════════

    /// <summary>获取 HID 设备属性（VID、PID、版本号）</summary>
    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetAttributes(
        SafeFileHandle hidDeviceObject, ref HidAttributes attributes);

    /// <summary>获取 HID 设备的预解析数据（用于后续 HidP_GetCaps）</summary>
    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetPreparsedData(
        SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

    /// <summary>释放预解析数据</summary>
    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    /// <summary>从预解析数据中提取 HID 设备能力信息</summary>
    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    // ════════════════════════════════════════════════════════════════
    //  kernel32.dll — 文件 I/O
    // ════════════════════════════════════════════════════════════════

    /// <summary>打开设备文件句柄（用于 HID 设备读写）</summary>
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    /// <summary>从设备句柄读取数据（同步 I/O）</summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadFile(
        SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

    /// <summary>关闭设备句柄</summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);
}
