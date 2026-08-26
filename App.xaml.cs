using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;
using HITAPEX.Models.Usb;
using HITAPEX.Services;
using HITAPEX.Services.Data;
using HITAPEX.Services.Data.Api;
using HITAPEX.Services.Usb;

namespace HITAPEX;

public partial class App : Application
{
    public static bool IsSessionEnding { get; private set; }
    public static UsbSerialManager? UsbManager { get; private set; }
    public static HidService? HidService { get; private set; }
    public static DeviceProtocolService? ProtocolService { get; private set; }
    public static FirmwareUpdateService? FirmwareUpdater { get; private set; }
    public static FirmwareApiService? FirmwareApi { get; private set; }
    public static ClientInstallerApiService? ClientInstallerApi { get; private set; }
    public static PresetService? PresetService { get; private set; }
    public static UserApiService? UserApi { get; private set; }
    public static TelemetryService? TelemetryService { get; private set; }
    public static GameDataService? GameDataService { get; private set; }

    private SplashWindow? _splash;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 初始化本地化服务（必须在显示任何 UI 之前）
        var language = HITAPEX.Properties.Settings.Default.Language ?? "zh-CN";
        LocalizationService.Instance.Initialize(language);

        //在独立 STA 线程上显示 splash，避免主线程初始化阻塞导致动画卡顿
        var splashReady = new ManualResetEventSlim();
        var splashThread = new Thread(() =>
        {
            _splash = new SplashWindow();
            _splash.Loaded += (_, _) => splashReady.Set();
            _splash.Show();
            System.Windows.Threading.Dispatcher.Run();
        })
        {
            IsBackground = true
        };
        splashThread.TrySetApartmentState(ApartmentState.STA);
        splashThread.Start();
        splashReady.Wait();

        // 注册全局未处理异常处理，防止 fire-and-forget 任务异常静默丢失
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Debug.WriteLine($"[App] 未观测任务异常: {args.Exception?.Message}");
            args.SetObserved();
        };

        DispatcherUnhandledException += (_, args) =>
        {
            Debug.WriteLine($"[App] 未处理UI异常: {args.Exception?.Message}");
            args.Handled = true;
        };

        InitializeUsbManager();

        var mainWindow = new MainWindow();
        SessionEnding += (_, _) => { IsSessionEnding = true; };

        if (HITAPEX.Properties.Settings.Default.StartMinimizedToTray)
        {
            CloseSplash();
            mainWindow.MinimizeToTray();
        }
        else
        {
            // 先关 splash 再显示主窗口，避免 Topmost splash 关闭时导致主窗口闪烁
            CloseSplash();
            mainWindow.Show();
            // 确保主窗口出现在最上层，不被其他应用遮挡
            mainWindow.Activate();
        }
    }

    /// <summary>
    /// 安全关闭独立线程上的 splash 窗口，并释放线程和 Dispatcher 资源
    /// </summary>
    private void CloseSplash()
    {
        var splash = _splash;
        _splash = null;
        if (splash == null) return;

        if (!splash.Dispatcher.HasShutdownStarted)
        {
            splash.Dispatcher.Invoke(() =>
            {
                splash.Close();
                // 关闭 Dispatcher 消息循环，释放 STA 线程及所有 WPF 资源
                splash.Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Normal);
            });
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        TelemetryService?.Dispose();
        TelemetryService = null;
        GameDataService?.Dispose();
        GameDataService = null;
        ClientInstallerApi?.Dispose();
        ClientInstallerApi = null;
        HidService?.Dispose();
        HidService = null;
        UsbManager?.Dispose();
        UsbManager = null;
        base.OnExit(e);
    }

    private void InitializeUsbManager()
    {
        UsbManager = new UsbSerialManager();

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

        // 初始化协议服务和固件更新服务
        ProtocolService = new DeviceProtocolService(UsbManager);
        FirmwareUpdater = new FirmwareUpdateService(UsbManager, ProtocolService);
        FirmwareApi = new FirmwareApiService();
        ClientInstallerApi = new ClientInstallerApiService();
        PresetService = new PresetService();
        UserApi = new UserApiService();

        // 后台异步恢复登录态（fire-and-forget，不阻塞启动）
        _ = Task.Run(async () =>
        {
            var restored = await UserApi.TryRestoreSessionAsync();
            // 登录/续期接口返回的 user 不含头像，需拉取 users/me 补齐完整资料
            if (restored)
                await UserApi.RefreshCurrentUserAsync();
            Debug.WriteLine($"[App] 登录态恢复结果: {restored}, CurrentUser={UserApi.CurrentUser?.Username}");
        });

        // 后台异步刷新官方预设缓存（fire-and-forget，不阻塞启动）
        _ = Task.Run(() => PresetService.EnsureOfficialPresetsRefreshedAsync());
        TelemetryService = new TelemetryService();
        GameDataService = new GameDataService();

        FirmwareUpdater.DebugLog += msg =>
            Debug.WriteLine($"[FirmwareUpdate] {msg}");

        // 初始化 HID 设备服务（与串口并行）
        HidService = new HidService();

        HidService.PedalDataReceived += (device, data) =>
        {
            // 踏板 HID 数据由 PedalParameterControl 订阅处理
            Debug.WriteLine($"[HID] 踏板数据 [{device.DeviceKey}]: 离合={data.ClutchPercent:F1}% 刹车={data.BrakePercent:F1}% 油门={data.GasPercent:F1}%");
        };

        HidService.BaseDataReceived += (device, data) =>
        {
            // 基座 HID 数据由 BaseParameterControl 订阅处理
            Debug.WriteLine($"[HID] 基座数据 [{device.DeviceKey}]: 转向={data.Steering}");
        };

        HidService.WheelDataReceived += (device, data) =>
        {
            // 面盘 HID 数据由 SteeringWheelParameterControl 订阅处理
            //Debug.WriteLine($"[HID] 面盘数据 [{device.DeviceKey}]: 按键位图={BitConverter.ToString(data.ButtonBits)}");
        };

        HidService.Start();
        UsbManager.Start();
    }
}
