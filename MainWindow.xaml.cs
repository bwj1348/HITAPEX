using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HITAPEX.Helpers;
using HITAPEX.Models.Usb;
using HITAPEX.Services;
using HITAPEX.Services.Data.Api;
using HITAPEX.ViewModels;
using HITAPEX.Controls;
using HITAPEX.Views;
using HITAPEX.Views.DeviceParameters;

namespace HITAPEX;

/// <summary>
/// 应用程序主窗口。负责 MVVM 导航、系统托盘、未保存修改保护、固件更新模式检测和预设列表弹窗管理。
/// 窗口外观为自定义无边框样式（WindowStyle="None"），标题栏和导航栏完全由 XAML 绘制。
/// </summary>
public partial class MainWindow : Window
{
    // ════════════════════════════════════════════════════════════════
    //  字段
    // ════════════════════════════════════════════════════════════════

    /// <summary>主窗口的 ViewModel，管理导航项和视图切换</summary>
    private readonly MainWindowViewModel _viewModel;

    /// <summary>预设列表弹窗缓存，按设备类型索引。避免重复创建，保持弹窗状态（滚动位置、选中 tab）</summary>
    private readonly Dictionary<DeviceType, PresetListPopup> _presetListPopups = new();

    /// <summary>系统托盘图标实例（持有 Win32 句柄，需在 OnClosed 中释放）</summary>
    private TrayIcon? _trayIcon;

    /// <summary>
    /// 防重入标志。为 true 时表示"未保存确认"对话框正在显示中，
    /// 禁止再次触发导航检查，防止多个对话框叠加。
    /// </summary>
    private bool _isCheckingUnsavedNavigation;

    /// <summary>
    /// 防重复弹窗标志。为 true 时表示"设备处于更新模式"的强制更新弹窗正在显示中，
    /// 后续检测到的更新模式设备将关闭旧弹窗并弹出包含所有设备的新弹窗。
    /// </summary>
    private bool _isShowingUpdateModeDialog;

    // ════════════════════════════════════════════════════════════════
    //  公开属性
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 全局模态弹窗控件引用。供子页面（如 DeviceUserControl）通过
    /// <c>((MainWindow)Application.Current.MainWindow).GlobalDialogControl</c> 访问。
    /// 命名使用 "Control" 后缀以区分 XAML 生成的 <c>GlobalDialog</c> 字段。
    /// </summary>
    public ModalDialog GlobalDialogControl => GlobalDialog;

    // ════════════════════════════════════════════════════════════════
    //  预设列表弹窗管理
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 获取指定设备类型的预设列表弹窗（只读，不创建新实例）。
    /// 如果没有缓存的实例，返回 null。
    /// </summary>
    public PresetListPopup? GetPresetListPopup(DeviceType deviceType)
    {
        _presetListPopups.TryGetValue(deviceType, out var popup);
        return popup;
    }

    /// <summary>
    /// 显示指定设备类型的预设列表弹窗。首次调用时创建并缓存实例，
    /// 后续调用直接复用已有实例（保持弹窗状态，如滚动位置和选中 tab）。
    /// </summary>
    /// <remarks>
    /// 弹窗被添加为窗口 Grid 的直接子元素，通过 Panel.ZIndex 浮在所有内容之上。
    /// </remarks>
    public PresetListPopup ShowPresetListPopup(DeviceType deviceType)
    {
        if (!_presetListPopups.TryGetValue(deviceType, out var popup))
        {
            popup = new PresetListPopup { DeviceType = deviceType };
            _presetListPopups[deviceType] = popup;
            // 将弹窗添加到窗口的内容面板，使其成为视觉树的一部分
            if (Content is Panel rootPanel)
                rootPanel.Children.Add(popup);
        }
        popup.Show();
        return popup;
    }

    // ════════════════════════════════════════════════════════════════
    //  构造与初始化
    // ════════════════════════════════════════════════════════════════

    public MainWindow()
    {
        // 1. 创建 ViewModel——内部构建 5 个导航项并选中首项（Home），触发首次视图加载
        _viewModel = new MainWindowViewModel();

        // 2. 设置 XAML 数据上下文，使所有 {Binding ...} 绑定生效
        DataContext = _viewModel;

        // 3. 加载 XAML 编译后的 BAML → 构建视觉树 → 初始化 x:Name 字段
        InitializeComponent();

        // 4. 初始化系统托盘图标
        InitializeTrayIcon();

        // 5. 订阅登录状态变化事件——登录/退出时实时刷新左下角用户信息区
        if (App.UserApi != null)
            App.UserApi.LoginStateChanged += OnLoginStateChanged;

        // 6. 订阅 Loaded 事件——窗口完成首次布局和渲染后执行一次性初始化
        Loaded += OnMainWindowLoaded;
    }

    /// <summary>
    /// 窗口首次加载完成时的一次性初始化：设置动态版本号、检测更新模式设备、监听运行时设备连接。
    /// 执行后立即取消订阅自身，保证只运行一次。
    /// </summary>
    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        // 取消订阅自身——Loaded 只触发一次
        Loaded -= OnMainWindowLoaded;

        // ── 动态设置标题栏版本号 ──
        // 从程序集元数据读取版本号（与 .csproj 中 <AssemblyVersion>0.1.0</AssemblyVersion> 同步）
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version != null)
        {
            var versionText = $"HITAPEX V {version.Major}.{version.Minor}.{version.Build}";
            Title = versionText;
            TitleVersionText.Text = versionText;
        }

        // ── 更新模式检测 ──
        // 场景1: 程序启动时设备已插入且处于更新模式（如上次固件更新中断）
        CheckAndShowUpdateModeDevicesOnStartup();

        // 场景2: 运行时设备热插拔——每当有新设备连接，检查是否为更新模式
        if (App.UsbManager != null)
        {
            App.UsbManager.DeviceConnected += OnUsbDeviceConnectedForUpdateMode;
        }

        // ── 初始化左下角用户信息区（根据当前登录状态显示默认图标/头像、游客/用户文字） ──
        RefreshLoginState();
    }

    // ════════════════════════════════════════════════════════════════
    //  左下角用户信息区
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 登录状态变化回调。事件可能来自后台线程（会话恢复的 fire-and-forget 任务），
    /// 必须封送到 UI 线程再刷新。用 BeginInvoke 非阻塞排队——启动阶段主线程消息泵尚未
    /// 运行时，同步 Invoke 会阻塞后台线程，导致会话恢复链（users/me 拉取头像）卡住。
    /// </summary>
    private void OnLoginStateChanged()
    {
        if (Dispatcher.CheckAccess())
            RefreshLoginState();
        else
            Dispatcher.BeginInvoke(RefreshLoginState);
    }

    /// <summary>
    /// 刷新左下角用户信息区：
    /// 图标 —— 未登录显示默认图标，登录后显示用户头像；
    /// 角色文字 —— 未登录显示"游客"，登录后显示"用户"；
    /// 名称 —— 未登录显示"Guest Mode"，登录后显示用户名。
    /// </summary>
    public void RefreshLoginState()
    {
        var isLoggedIn = App.UserApi?.IsLoggedIn == true;
        var user = App.UserApi?.CurrentUser;

        // 图标：未登录显示默认图标，登录后显示用户头像
        UserIconDefault.Visibility = isLoggedIn ? Visibility.Collapsed : Visibility.Visible;
        UserAvatarImage.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;

        // 右侧上方文字：未登录"游客"，登录后"用户"
        UserRoleText.Text = LocalizationService.Instance[isLoggedIn ? "Window.User" : "Window.Guest"];

        // 下方名称：未登录"Guest Mode"，登录后显示用户名
        UserDisplayNameText.Text = isLoggedIn && user != null && !string.IsNullOrEmpty(user.Username)
            ? user.Username
            : LocalizationService.Instance["Window.GuestMode"];

        // 已登录时异步加载用户头像
        if (isLoggedIn && user != null)
            LoadUserAvatar(user);
    }

    /// <summary>
    /// 加载用户头像到左下角图标区。
    /// 头像 URL 是服务器返回的相对路径，需拼接 API 基础地址构成完整地址后异步加载。
    /// </summary>
    private void LoadUserAvatar(UserInfo user)
    {
        var relativeUrl = user.Image?.Url;
        if (string.IsNullOrEmpty(relativeUrl))
        {
            UserAvatarImage.Source = null;
            return;
        }

        try
        {
            var fullUrl = UserApiService.BaseUrl + relativeUrl;
            var bitmap = new BitmapImage(new Uri(fullUrl));
            bitmap.DecodeFailed += (_, _) =>
                Debug.WriteLine($"[MainWindow] 用户头像解码失败: {fullUrl}");
            UserAvatarImage.Source = bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainWindow] 加载用户头像失败: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  固件更新模式检测与弹窗
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 启动时检查所有已连接的设备，如果存在更新模式设备，弹出强制更新对话框。
    /// </summary>
    /// <remarks>
    /// 更新模式设备是指 VID/PID 处于 DeviceRegistry 中定义的 UpdateMode 的设备——
    /// 这些设备的固件异常或正在等待更新，在完成更新前无法正常通信。
    /// </remarks>
    private void CheckAndShowUpdateModeDevicesOnStartup()
    {
        // 安全获取已连接设备列表（UsbManager 理论已初始化，但防御性编程）
        var connectedDevices = App.UsbManager?.ConnectedDevices
                               ?? new List<UsbDeviceInfo>().AsReadOnly();

        // 过滤：只保留处于更新模式的设备（通过 DeviceRegistry 判断 VID/PID）
        var updateModeDevices = connectedDevices
            .Where(d => DeviceRegistry.IsUpdateMode(d.Vid, d.Pid))
            .ToList();

        if (updateModeDevices.Count > 0)
        {
            ShowUpdateModeDialog(updateModeDevices);
        }
    }

    /// <summary>
    /// 运行时设备连接回调：检测到更新模式设备时弹出强制更新对话框。
    /// </summary>
    /// <remarks>
    /// 如果当前正在执行固件更新流程（FirmwareUpdateService.IsUpdating），
    /// 说明设备进入更新模式是固件更新流程主动触发的，此时不弹窗——用户已在更新页面操作，不需要额外打扰。
    /// 设备连接事件可能来自非 UI 线程（WMI 事件线程），因此必须通过 Dispatcher.Invoke 封送到 UI 线程弹窗。
    /// </remarks>
    private void OnUsbDeviceConnectedForUpdateMode(UsbDeviceInfo device)
    {
        // 非更新模式设备 → 跳过
        if (!DeviceRegistry.IsUpdateMode(device.Vid, device.Pid))
            return;

        // 固件更新流程中主动切换的更新模式设备 → 不弹窗
        if (App.FirmwareUpdater?.IsUpdating == true)
            return;

        // 封送到 UI 线程弹窗（设备事件可能来自 WMI 事件线程等非 UI 线程）
        Dispatcher.Invoke(() =>
        {
            ShowUpdateModeDialog(new List<UsbDeviceInfo> { device });
        });
    }

    /// <summary>
    /// 显示更新模式强制弹窗。
    /// 列出所有处于更新模式的设备名称，用户点击"前往更新"后跳转固件更新界面并自动开始更新。
    /// </summary>
    /// <remarks>
    /// 防重复逻辑：如果已有一个弹窗在显示，先关闭旧弹窗再弹出新的——
    /// 新弹窗包含所有当前处于更新模式的设备，避免两个弹窗叠加。
    /// 弹窗内容（消息文本、按钮）完全由代码动态构建，不使用 XAML 预定义。
    /// </remarks>
    private void ShowUpdateModeDialog(List<UsbDeviceInfo> updateModeDevices)
    {
        if (updateModeDevices.Count == 0)
            return;

        // ── 防重复弹窗 ──
        if (_isShowingUpdateModeDialog)
        {
            // 关闭旧弹窗（新弹窗将包含增加/合并后的设备列表）
            GlobalDialog.Hide();
        }
        _isShowingUpdateModeDialog = true;

        // ── 配置弹窗基础属性 ──
        GlobalDialog.Title = LocalizationService.Instance["Firmware.DeviceAbnormal"];
        GlobalDialog.ClearButtons();

        // ── 提取设备名称列表 ──
        // 通过 DeviceRegistry 查找显示名称（如"A1踏板"），未知设备显示原始 VID/PID
        var deviceNames = updateModeDevices
            .Select(d =>
            {
                var desc = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
                return desc?.ModelName ?? $"未知设备 (VID={d.Vid:X4} PID={d.Pid:X4})";
            })
            .Distinct()  // 去重——同一型号设备可能因多次检测重复列出
            .ToList();

        // ── 构造本地化提示文本 ──
        // JSON 模板: "检测到 {0} 设备固件异常，需要更新固件，请前往更新！"
        var namesText = string.Join("、", deviceNames);
        var messageText = LocalizationService.Instance.Format("Firmware.DeviceFirmwareAbnormal", namesText);

        // ── 构造消息 TextBlock（样式复用 ButtonSettingsPopup 等弹窗的配色方案） ──
        var messageBlock = new TextBlock
        {
            Text = messageText,
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),  // #EEEEEE
            TextWrapping = TextWrapping.Wrap,      // 长文本自动换行
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 30)    // 底部 30px 留给按钮
        };

        // ── 构造"前往更新"按钮 ──
        var button = BuildPrimaryButton(LocalizationService.Instance["Firmware.GoToUpdate"]);
        button.Click += (_, _) =>
        {
            _isShowingUpdateModeDialog = false;
            GlobalDialog.Hide();
            // 跳转到设置 → 固件更新 tab，传入设备列表自动开始更新
            NavigateToFirmwareUpdate(updateModeDevices);
        };

        // ── 组装弹窗内容（两行 Grid: 消息 + 按钮） ──
        var contentPanel = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        contentPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });  // 消息占据剩余空间
        contentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                        // 按钮自适应高度

        messageBlock.VerticalAlignment = VerticalAlignment.Center;
        contentPanel.Children.Add(messageBlock);

        Grid.SetRow(button, 1);
        contentPanel.Children.Add(button);

        // ── 显示弹窗 ──
        GlobalDialog.DialogContent = contentPanel;
        GlobalDialog.Show();
    }

    /// <summary>
    /// 用代码构建一个红色渐变斜切角主按钮（等价于 XAML 中预定义样式）。
    /// 使用 <see cref="FrameworkElementFactory"/> 代替 XAML 元素节点构建 ControlTemplate。
    /// </summary>
    /// <param name="text">按钮文本</param>
    /// <returns>准备好模板的 Button 实例</returns>
    private Button BuildPrimaryButton(string text)
    {
        var button = new Button
        {
            Content = text,
            Width = 172,
            Height = 32,
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // ── 构建 ControlTemplate（等价于 XAML 的 <ControlTemplate TargetType="Button">） ──
        var template = new ControlTemplate(typeof(Button));

        // 根布局: Grid
        var gridFactory = new FrameworkElementFactory(typeof(Grid));

        // 背景形状: Path（斜切角矩形，左上和右下有 6px 的 45° 斜面）
        var pathFactory = new FrameworkElementFactory(typeof(Path));
        // M0 6V32H166L172 26V0H6L0 6Z  →  172×32 的斜切角矩形
        pathFactory.SetValue(Path.DataProperty, Geometry.Parse("M0 6V32H166L172 26V0H6L0 6Z"));
        pathFactory.SetValue(Path.StretchProperty, Stretch.Fill);
        pathFactory.SetValue(Path.WidthProperty, 172.0);
        pathFactory.SetValue(Path.HeightProperty, 32.0);

        // Path 的红色渐变填充（从上到下: 亮红 #C60E0E → 暗红 #600707, 整体 80% 不透明度）
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),   // 渐变起点: 顶部
            EndPoint = new Point(0, 1),      // 渐变终点: 底部
            Opacity = 0.8
        };
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(198, 14, 14), 0));   // #C60E0E 顶部亮红
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(96, 7, 7), 1));       // #600707 底部暗红
        pathFactory.SetValue(Path.FillProperty, gradient);

        // 内容展示器（文字层，叠加在 Path 之上）
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        // 设置内容文字颜色（通过继承属性，ContentPresenter 中的 TextBlock 会自动使用这些值）
        contentFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(238, 238, 238)));
        contentFactory.SetValue(TextBlock.FontSizeProperty, 18.0);
        contentFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);

        // ── 组装视觉树: Grid > Path（底层背景）→ ContentPresenter（上层文字） ──
        gridFactory.AppendChild(pathFactory);       // 先添加 → 在下层
        gridFactory.AppendChild(contentFactory);    // 后添加 → 在上层
        template.VisualTree = gridFactory;
        button.Template = template;

        return button;
    }

    /// <summary>
    /// 导航到设置界面的固件更新选项卡，并传入待更新设备列表以自动开始批量更新。
    /// </summary>
    /// <remarks>
    /// 使用 <c>BeginInvoke(DispatcherPriority.Loaded)</c> 延迟执行——确保
    /// SettingsUserControl 在完成加载（Loaded 事件已触发）后再调用
    /// SwitchToFirmwareUpdateTab，此时控件内部的 tab 和子元素已完全初始化。
    /// </remarks>
    private void NavigateToFirmwareUpdate(List<UsbDeviceInfo> updateModeDevices)
    {
        var settingsItem = _viewModel.NavigationItems.FirstOrDefault(n => n.Name == "Settings");
        if (settingsItem == null) return;

        // 触发 ViewModel 导航——创建/切换到 SettingsUserControl
        _viewModel.SelectedNavigationItem = settingsItem;

        // 延迟到控件加载完成后再切换 tab，确保固件更新子元素已初始化
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            var settingsView = _viewModel.CurrentView as SettingsUserControl;
            settingsView?.SwitchToFirmwareUpdateTab(updateModeDevices);
        });
    }

    // ════════════════════════════════════════════════════════════════
    //  系统托盘管理
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 初始化系统托盘图标：创建图标、设置提示文字、绑定双击和退出事件、
    /// 拦截窗口 Closing 事件实现"最小化到托盘"功能。
    /// </summary>
    private void InitializeTrayIcon()
    {
        _trayIcon = new TrayIcon(this);
        _trayIcon.SetTooltip("HITAPEX");
        _trayIcon.DoubleClick += RestoreFromTray;    // 双击托盘 → 恢复窗口
        _trayIcon.ExitRequested += ExitApplication;   // 右键"退出" → 彻底关闭

        // ── 拦截窗口关闭事件 ──
        Closing += (s, e) =>
        {
            // Windows 会话结束（关机/注销）→ 不拦截，让窗口正常关闭
            if (App.IsSessionEnding)
                return;

            // 用户启用了"关闭时最小化到托盘" → 拦截关闭，改为隐藏到托盘
            if (Properties.Settings.Default.CloseMinimizedToTray)
            {
                e.Cancel = true;        // 取消关闭
                MinimizeToTray();       // 隐藏到托盘
            }
            // 否则: 正常关闭，触发 Application.Shutdown
        };
    }

    /// <summary>
    /// 最小化到系统托盘：隐藏窗口，显示托盘图标。
    /// 供 App.xaml.cs（启动时 StartMinimizedToTray=true）和 CloseButton_Click 调用。
    /// </summary>
    public void MinimizeToTray()
    {
        Hide();
        if (_trayIcon != null)
            _trayIcon.Visible = true;
    }

    /// <summary>
    /// 从系统托盘恢复窗口：显示窗口、取消最小化状态、激活带到前台。
    /// </summary>
    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;   // 确保不是最小化状态（用户可能按了 Win+D 等）
        Activate();                         // 强制激活——在其他窗口之上
    }

    /// <summary>
    /// 退出应用程序：释放托盘图标资源后关闭整个进程。
    /// </summary>
    private void ExitApplication()
    {
        _trayIcon?.Dispose();             // 释放 Win32 图标句柄
        Application.Current.Shutdown();   // 触发 App.OnExit → 连锁释放所有硬件服务
    }

    // ════════════════════════════════════════════════════════════════
    //  标题栏事件处理
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 自定义标题栏的鼠标左键事件：
    /// 双击 → 切换最大化/还原；单击/拖拽 → 实现无边框窗口的拖拽移动。
    /// 因为 WindowStyle="None" 隐藏了系统标题栏，必须手动实现这两个行为。
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 双击: 切换最大化/还原（模拟标准 Windows 窗口行为）
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            // 单击/拖拽: WPF 内置的无边框窗口拖拽 API
            DragMove();
        }
    }

    /// <summary>最小化按钮点击 → 窗口最小化到任务栏</summary>
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// 关闭按钮点击 → 根据用户设置决定是最小化到托盘还是退出应用。
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (Properties.Settings.Default.CloseMinimizedToTray)
        {
            MinimizeToTray();                    // 隐藏到托盘（不退出）
        }
        else
        {
            Application.Current.Shutdown();      // 退出应用
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  导航切换与未保存保护
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 导航按钮选中事件（RadioButton.Checked 触发）。
    /// 在切换页面前检查当前设备参数页面是否有未保存的修改，
    /// 如果有则弹出确认对话框，用户可选择保存后跳转或放弃修改直接跳转。
    /// </summary>
    /// <remarks>
    /// <para>防重入机制：<c>_isCheckingUnsavedNavigation</c> 标志为 true 时，
    /// 表示确认对话框正在显示中，此时忽略新的导航事件，防止多个对话框叠加。</para>
    /// <para>为什么检查 IsLoaded：控件在构造时可能因内部初始化（设置默认值、加载预设）
    /// 意外触发 HasUnsavedChanges=true，但这不是用户操作导致的。
    /// IsLoaded 为 true 才说明控件已显示在界面上，此时的修改才是用户行为。</para>
    /// </remarks>
    private void NavigationItem_Checked(object sender, RoutedEventArgs e)
    {
        // 防重入：正在处理未保存确认对话框中的导航 → 忽略
        if (_isCheckingUnsavedNavigation) return;

        // 双重校验：sender 必须是 RadioButton，其 DataContext 必须是 NavigationItem
        if (sender is RadioButton radioButton && radioButton.DataContext is NavigationItem navItem)
        {
            // 只有当前页面是设备参数页时才需要检查未保存修改
            if (_viewModel.CurrentView is DeviceUserControl deviceControl)
            {
                // ── 检查踏板参数是否有未保存修改 ──
                if (deviceControl.PedalControl is { IsLoaded: true, HasUnsavedChanges: true })
                {
                    _isCheckingUnsavedNavigation = true;
                    deviceControl.PedalControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            // 用户点击"保存"→ 参数已保存到设备 → 放行导航
                            _isCheckingUnsavedNavigation = false;
                            _viewModel.SelectedNavigationItem = navItem;
                        },
                        onCancelled: () =>
                        {
                            // 用户点击"不保存"→ 放弃修改 → 放行导航
                            _isCheckingUnsavedNavigation = false;
                            _viewModel.SelectedNavigationItem = navItem;
                        });
                    return;  // 等待用户决策，不执行后续跳转
                }

                // ── 检查面盘参数是否有未保存修改 ──
                if (deviceControl.SteeringWheelControl is { IsLoaded: true, HasUnsavedChanges: true })
                {
                    _isCheckingUnsavedNavigation = true;
                    deviceControl.SteeringWheelControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            _isCheckingUnsavedNavigation = false;
                            _viewModel.SelectedNavigationItem = navItem;
                        },
                        onCancelled: () =>
                        {
                            _isCheckingUnsavedNavigation = false;
                            _viewModel.SelectedNavigationItem = navItem;
                        });
                    return;
                }

                // ── 检查基座参数是否有未保存修改 ──
                if (deviceControl.BaseControl is { IsLoaded: true, HasUnsavedChanges: true })
                {
                    _isCheckingUnsavedNavigation = true;
                    deviceControl.BaseControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            _isCheckingUnsavedNavigation = false;
                            _viewModel.SelectedNavigationItem = navItem;
                        },
                        onCancelled: () =>
                        {
                            _isCheckingUnsavedNavigation = false;
                            _viewModel.SelectedNavigationItem = navItem;
                        });
                    return;
                }
            }

            // 无未保存修改或不在设备参数页 → 直接切换视图
            // ViewModel 的 setter 内部更新 CurrentView → ContentControl 绑定刷新
            _viewModel.SelectedNavigationItem = navItem;
        }
    }

    /// <summary>
    /// 获取当前可见的 SettingsUserControl（如果已创建并正在显示）。
    /// 登录成功后 LoginPopup 调用此方法直接刷新登录状态。
    /// </summary>
    public SettingsUserControl? GetCurrentSettingsView()
        => _viewModel.CurrentView as SettingsUserControl;

    // ════════════════════════════════════════════════════════════════
    //  窗口生命周期
    // ════════════════════════════════════════════════════════════════

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
    }

    /// <summary>
    /// 窗口关闭时释放托盘图标资源。
    /// 先调用基类完成 WPF 内部清理，再释放托盘图标的 Win32 非托管句柄。
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _trayIcon?.Dispose();   // 释放 Shell_NotifyIcon 创建的托盘图标句柄
    }
}
