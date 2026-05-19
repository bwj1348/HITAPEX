using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using HITAPEX.Models.Usb;
using HITAPEX.Services.Data.Api;
using HITAPEX.Services.Usb;

namespace HITAPEX;

public partial class App : Application
{
    public static bool IsSessionEnding { get; private set; }
    public static UsbSerialManager? UsbManager { get; private set; }
    public static DeviceProtocolService? ProtocolService { get; private set; }
    public static FirmwareUpdateService? FirmwareUpdater { get; private set; }
    public static FirmwareApiService? FirmwareApi { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        InitializeUsbManager();

        var mainWindow = new MainWindow();

        SessionEnding += (_, _) => { IsSessionEnding = true; };

        if (HITAPEX.Properties.Settings.Default.StartMinimizedToTray)
        {
            mainWindow.MinimizeToTray();
        }
        else
        {
            mainWindow.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        UsbManager?.Dispose();
        UsbManager = null;
        base.OnExit(e);
    }

    private void InitializeUsbManager()
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs", "usb");
        UsbManager = new UsbSerialManager(logDir);

        // 注册目标 VID/PID 对（从设备注册表统一管理）
        UsbManager.RegisterTargetDevices(DeviceRegistry.GetAllVidPids());

        UsbManager.DeviceConnected += device =>
            Debug.WriteLine($"[USB] 设备已连接: {device}");

        UsbManager.DeviceDisconnected += device =>
            Debug.WriteLine($"[USB] 设备已断开: {device}");

        UsbManager.RawDataReceived += (device, data) =>
        {
            // 原始数据由各业务模块订阅处理
        };

        UsbManager.DeviceError += (device, error) =>
            Debug.WriteLine($"[USB] 设备错误 [{device.DeviceKey}]: {error}");

        UsbManager.LogEntryAdded += entry =>
        {
            // 日志条目由 DeviceLogger 直接写入文件
        };

        // 初始化协议服务和固件更新服务
        ProtocolService = new DeviceProtocolService(UsbManager);
        FirmwareUpdater = new FirmwareUpdateService(UsbManager, ProtocolService);
        FirmwareApi = new FirmwareApiService();

        FirmwareUpdater.DebugLog += msg =>
            Debug.WriteLine($"[FirmwareUpdate] {msg}");

        UsbManager.Start();
    }
}
