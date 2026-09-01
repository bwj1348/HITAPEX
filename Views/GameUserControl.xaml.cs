using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HITAPEX.Controls;
using HITAPEX.Models;
using HITAPEX.Models.Usb;
using HITAPEX.Services;
using HITAPEX.Services.Data;
using HITAPEX.Services.Data.Api;
using HITAPEX.Services.Usb;
using HITAPEX.Views.DeviceParameters;
using Microsoft.Win32;

namespace HITAPEX.Views;

public enum GameFilterType
{
    All,
    Installed,
    NotInstalled
}

/// <summary>
/// 游戏库/启动器页面的主视图控件。
/// 提供可筛选的游戏卡片列表，支持启动游戏、置顶（Pin）动画、
/// 遥测配置、自定义滚动条以及悬停（Hover）平移动画等功能。
/// </summary>
public partial class GameUserControl : UserControl
{
    // ═══════════════════════════════════════════════════
    // 私有字段
    // ═══════════════════════════════════════════════════

    private ObservableCollection<GameItem>? _allGameList;
    private ObservableCollection<GameItem>? _filteredGameList;
    private bool _isInitialized;
    private GameFilterType _currentFilter = GameFilterType.All;
    private bool _isPinning = false;
    private bool _isLoading = false;
    private GameItem? _selectedGame;
    private GameDataService? _gameDataService;
    private CancellationTokenSource? _loadGamesCts;

    /// <summary>遥测设置的 UDP 转发目标条目集合。</summary>
    private readonly ObservableCollection<ForwardTargetRow> _forwardTargetRows = new();
    
    private DispatcherTimer? _telemetryAnimationTimer;
    private int _packetCount = 0;
    
    private bool _isDraggingThumb = false;
    private Point _lastMousePosition;

    public GameUserControl()
    {
        InitializeComponent();

        // 转发目标列表的数据源
        ForwardTargetItemsControl.ItemsSource = _forwardTargetRows;
    }

    private void GameUserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        // 订阅语言切换事件，以便动态更新游戏介绍文本
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;

        InitializeGameList();
        PopulatePresetComboBoxes();
        StartTelemetrySimulation();
        UpdateScrollbarThumb();
    }

    // ═══════════════════════════════════════════════════
    // 游戏列表管理 — 初始化、数据加载、筛选与选择
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 初始化游戏列表：绑定数据服务事件，创建集合并从服务加载游戏数据。
    /// 加载完成后自动选中第一个游戏项。
    /// </summary>
    private async void InitializeGameList()
    {
        _gameDataService = App.GameDataService;
        _gameDataService!.StateChanged += OnGameDataStateChanged;

        _allGameList = new ObservableCollection<GameItem>();
        _filteredGameList = new ObservableCollection<GameItem>();
        GameListItemsControl.ItemsSource = _filteredGameList;

        ShowLoadingState();
        await LoadGamesAsync();
        HideLoadingState();

        if (_filteredGameList is { Count: > 0 })
            SelectGame(_filteredGameList[0]);
    }

    /// <summary>
    /// 异步加载游戏数据，填充安装状态，处理网络异常并回退到缓存数据。
    /// 加载完成后自动应用当前筛选条件。
    /// </summary>
    private async Task LoadGamesAsync(bool forceRefresh = false)
    {
        if (_gameDataService == null || _isLoading) return;
        _isLoading = true;

        _loadGamesCts?.Cancel();
        _loadGamesCts = new CancellationTokenSource();

        try
        {
            var games = await _gameDataService.GetGamesAsync(forceRefresh, _loadGamesCts.Token);
            _gameDataService.EnrichWithInstallStatus(games);

            _allGameList?.Clear();
            foreach (var game in games)
                _allGameList?.Add(game);

            ApplyFilter(_currentFilter);
        }
        catch (GameServiceException ex) when (ex.IsClientError)
        {
            ApplyFilter(_currentFilter);
        }
        catch (Exception)
        {
            var cached = _gameDataService.GetCachedGames();
            if (cached != null && cached.Count > 0)
            {
                _allGameList?.Clear();
                foreach (var game in cached)
                    _allGameList?.Add(game);
                ApplyFilter(_currentFilter);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// 游戏数据状态变更回调：当数据从缓存/网络加载完成后，刷新 UI。
    /// </summary>
    private async void OnGameDataStateChanged(GameDataState state)
    {
        if (state == GameDataState.Loaded)
            await Dispatcher.InvokeAsync(async () => await LoadGamesAsync());
    }

    /// <summary>
    /// 显示加载遮罩层。
    /// </summary>
    private void ShowLoadingState()
    {
        if (LoadingOverlay != null)
            LoadingOverlay.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 隐藏加载遮罩层。
    /// </summary>
    private void HideLoadingState()
    {
        if (LoadingOverlay != null)
            LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 根据指定筛选类型过滤游戏列表，并按排序规则重新排列。
    /// 排序优先级：置顶（Pinned） &gt; 已安装（Installed） &gt; 最近启动时间 &gt; 名称。
    /// </summary>
    private void ApplyFilter(GameFilterType filter)
    {
        if (_allGameList == null || _filteredGameList == null) return;

        _filteredGameList.Clear();

        // 根据筛选类型选取子集
        var filtered = filter switch
        {
            GameFilterType.Installed => _allGameList.Where(g => g.IsInstalled),
            GameFilterType.NotInstalled => _allGameList.Where(g => !g.IsInstalled),
            _ => _allGameList
        };

        // 排序：置顶优先 → 已安装次之 → 最近启动时间 → 名称字母序
        var sortedFiltered = filtered
            .OrderByDescending(g => g.IsPinned)
            .ThenByDescending(g => g.IsInstalled)
            .ThenByDescending(g => g.LastLaunchTime ?? DateTime.MinValue)
            .ThenBy(g => g.Name);

        foreach (var game in sortedFiltered)
        {
            _filteredGameList.Add(game);
        }

        GameListItemsControl.ItemsSource = _filteredGameList;
        _currentFilter = filter;

        // 筛选后重置滚动条位置到最左侧
        GameScrollViewer.ScrollToHorizontalOffset(0);
        Dispatcher.BeginInvoke(() => UpdateScrollbarThumb(), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 选中指定游戏，更新标题、背景图、描述文本，
    /// 根据缓存的启动模式恢复单选按钮状态和启动按钮文字。
    /// </summary>
    private void SelectGame(GameItem game)
    {
        _selectedGame = game;
        GameTitleText.Text = game.Name;
        GameTitleText2.Text = game.Name;
        UpdateGameDescription();

        GameBackgroundImage.SetBinding(Image.SourceProperty, new Binding("BgImageUrl") { Source = game });

        // 根据缓存的启动模式恢复单选按钮
        if (game.LaunchMode == LaunchModeUdf.CustomPath)
        {
            CustomLaunchRadio.IsChecked = true;
            SteamLaunchRadio.IsChecked = false;
            CustomPathPanel.Visibility = Visibility.Visible;
            if (!string.IsNullOrEmpty(game.LaunchPath))
                CustomPathText.Text = game.LaunchPath;
            else
                SetCustomPathPlaceholder();
        }
        else
        {
            SteamLaunchRadio.IsChecked = true;
            CustomLaunchRadio.IsChecked = false;
            CustomPathPanel.Visibility = Visibility.Collapsed;
        }

        if (game.IsInstalled)
        {
            LaunchButtonPath.Visibility = Visibility.Visible;
            LaunchButtonPathNotInstalled.Visibility = Visibility.Collapsed;
            SetLaunchButtonBinding("Game.LaunchGame");

            // 仅对已安装且需要遥测配置的游戏显示按钮
            TelemetryConfigButton.Visibility = game.NeedsTelemetryConfig
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        else
        {
            LaunchButtonPath.Visibility = Visibility.Collapsed;
            LaunchButtonPathNotInstalled.Visibility = Visibility.Visible;
            SetLaunchButtonBinding("Common.NotInstalled");
            TelemetryConfigButton.Visibility = Visibility.Collapsed;
        }

        // 根据当前游戏是否需要 UDP 遥测配置，控制"遥测设置"选项卡的显示
        UpdateTelemetrySettingsTabVisibility(game);
    }

    /// <summary>
    /// 控制"遥测设置"选项卡的显示：仅对通过 UDP 传输遥测数据、需要配置端口的游戏显示。
    /// </summary>
    private void UpdateTelemetrySettingsTabVisibility(GameItem game)
    {
        var shouldShow = game.NeedUdpPortConfig;
        TelemetrySettingsTabItem.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;

        // 若隐藏的选项卡正处于选中状态，切回"设备配置"选项卡，避免空内容
        if (!shouldShow && ConfigTabControl.SelectedItem == TelemetrySettingsTabItem)
        {
            ConfigTabControl.SelectedIndex = 0;
        }
    }

    // ═══════════════════════════════════════════════════
    // 遥测设置 — UDP 转发目标：添加 / 删除
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// "添加转发目标"文本点击：新增一个空的转发目标条目。
    /// </summary>
    private void AddForwardTargetText_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _forwardTargetRows.Add(new ForwardTargetRow
        {
            Enabled = true,
            Port = string.Empty,
            Ip = string.Empty
        });
    }

    /// <summary>
    /// 删除按钮点击：移除所属转发目标条目。
    /// </summary>
    private void DeleteForwardTargetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ForwardTargetRow row })
        {
            _forwardTargetRows.Remove(row);
        }
    }

    /// <summary>
    /// 通过绑定设置启动按钮文字，避免破坏 XAML 中的 {lex:Loc} 绑定。
    /// 直接赋值 Text 属性会清除绑定，导致语言切换时文字不再更新。
    /// </summary>
    private void SetLaunchButtonBinding(string locKey)
    {
        LaunchButtonText.SetBinding(TextBlock.TextProperty, new Binding
        {
            Source = LocalizationService.Instance,
            Path = new PropertyPath($"[{locKey}]"),
            Mode = BindingMode.OneWay
        });
    }

    /// <summary>
    /// 通过绑定设置自定义路径的占位提示文字（而非直接赋值），
    /// 避免清除 XAML 中的 {lex:Loc} 绑定，保证语言切换时占位文字同步更新。
    /// </summary>
    private void SetCustomPathPlaceholder()
    {
        CustomPathText.SetBinding(TextBlock.TextProperty, new Binding
        {
            Source = LocalizationService.Instance,
            Path = new PropertyPath("[Game.SelectGamePath]"),
            Mode = BindingMode.OneWay
        });
    }

    /// <summary>
    /// 底部筛选单选按钮点击：切换"全部"/"已安装"/"未安装"视图。
    /// 筛选后自动选中列表第一项。
    /// </summary>
    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton)
        {
            var filter = radioButton.Name switch
            {
                "FilterAllButton" => GameFilterType.All,
                "FilterInstalledButton" => GameFilterType.Installed,
                "FilterNotInstalledButton" => GameFilterType.NotInstalled,
                _ => GameFilterType.All
            };
            ApplyFilter(filter);

            if (_filteredGameList != null && _filteredGameList.Count > 0)
                SelectGame(_filteredGameList[0]);
        }
    }

    /// <summary>
    /// 刷新按钮点击：重新检测 Steam 已安装状态，保持当前选中项不变。
    /// </summary>
    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _gameDataService == null || _allGameList == null) return;

        ShowLoadingState();

        var previousGameId = _selectedGame?.Id;

        // 重新检测 Steam 安装状态（游戏列表已是硬编码数据，无需从 API 重新获取）
        _gameDataService.EnrichWithInstallStatus(_allGameList);

        // 重新应用筛选和排序（置顶 → 已安装 → 最近启动时间）
        ApplyFilter(_currentFilter);

        if (_filteredGameList is { Count: > 0 })
        {
            var keepSelected = previousGameId != null
                ? _filteredGameList.FirstOrDefault(g => g.Id == previousGameId)
                : null;
            SelectGame(keepSelected ?? _filteredGameList[0]);
        }

        HideLoadingState();
    }

    // ═══════════════════════════════════════════════════
    // 遥测配置 Toast 提示 — 成功/失败两种样式
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 遥测配置按钮点击：对当前选中的游戏应用遥测配置。
    /// 成功则显示绿色勾号 Toast，失败则显示红色警告 Toast。
    /// </summary>
    private void TelemetryConfigButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (_selectedGame == null) return;

        var success = TelemetryConfigService.ApplyConfig(_selectedGame);
        if (success)
        {
            ShowTelemetryConfigSuccessToast();
        }
        else
        {
            ShowTelemetryConfigFailToast();
        }
    }

    /// <summary>
    /// 显示遥测配置成功的 Toast 提示（绿色勾号图标，1 秒后自动消失）。
    /// 通过代码动态创建 Grid 布局，包含背景层、边框层和图标+文字的横向堆叠内容。
    /// </summary>
    private void ShowTelemetryConfigSuccessToast()
    {
        var rootPanel = (Window.GetWindow(this)?.Content as Panel);
        if (rootPanel == null) return;

        // 创建 Toast 容器 Grid，居中显示于父窗口
        var toast = new Grid
        {
            Width = 360,
            Height = 100,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Panel.SetZIndex(toast, 2000); // 置于所有 UI 元素之上

        // 深灰色梯形背景
        toast.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M360 0H9L0 9V100H351L360 91V0Z"),
            Fill = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)),
            Stretch = Stretch.Fill
        });

        // SVG 装饰图标
        toast.Children.Add(new SharpVectors.Converters.SvgViewbox
        {
            Source = new Uri("/Assets/Group126548867.svg", UriKind.Relative),
            Stretch = Stretch.Fill
        });

        // 内边框
        toast.Children.Add(new System.Windows.Shapes.Path
        {
            Width = 340,
            Height = 80,
            Data = Geometry.Parse("M339.5 0.5V73.793L333.793 79.5H0.5V6.20703L6.20703 0.5H339.5Z"),
            Stroke = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
            StrokeThickness = 1,
            Stretch = Stretch.Fill
        });

        // 内容层：图标 + 文字
        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 绿色勾号图标（表示成功）
        var iconCanvas = new Canvas { Width = 22, Height = 22 };
        iconCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M6.13672 12.2886L9.29057 14.8117C9.37527 14.8814 9.47445 14.9314 9.5809 14.9581C9.68735 14.9847 9.79839 14.9872 9.90595 14.9655C10.0145 14.9452 10.1175 14.9016 10.2077 14.8379C10.298 14.7742 10.3735 14.6918 10.429 14.5963L15.3675 6.13477"),
            Stroke = new SolidColorBrush(Color.FromRgb(0x16, 0xC6, 0x42)),
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        });
        iconCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M10.75 20.75C16.2728 20.75 20.75 16.2728 20.75 10.75C20.75 5.22715 16.2728 0.75 10.75 0.75C5.22715 0.75 0.75 5.22715 0.75 10.75C0.75 16.2728 5.22715 20.75 10.75 20.75Z"),
            Stroke = new SolidColorBrush(Color.FromRgb(0x16, 0xC6, 0x42)),
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        });

        var iconViewbox = new Viewbox { Width = 22, Height = 22, Margin = new Thickness(0, 0, 20, 0), Child = iconCanvas };
        contentPanel.Children.Add(iconViewbox);

        contentPanel.Children.Add(new TextBlock
        {
            Text = LocalizationService.Instance["Game.ConfigSuccess"],
            FontSize = 30,
            Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        });

        toast.Children.Add(contentPanel);
        rootPanel.Children.Add(toast); // 将 Toast 挂载到父窗口的根面板

        // 1 秒后自动移除 Toast
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (rootPanel.Children.Contains(toast))
                rootPanel.Children.Remove(toast);
        };
        timer.Start();
    }

    /// <summary>
    /// 显示遥测配置失败的 Toast 提示（红色警告图标，1 秒后自动消失）。
    /// 结构与成功提示相同，仅图标和文字不同。
    /// </summary>
    private void ShowTelemetryConfigFailToast()
    {
        var rootPanel = (Window.GetWindow(this)?.Content as Panel);
        if (rootPanel == null) return;

        // 创建 Toast 容器 Grid，居中显示于父窗口
        var toast = new Grid
        {
            Width = 360,
            Height = 100,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Panel.SetZIndex(toast, 2000); // 置于所有 UI 元素之上

        // 深灰色梯形背景
        toast.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M360 0H9L0 9V100H351L360 91V0Z"),
            Fill = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)),
            Stretch = Stretch.Fill
        });

        // SVG 装饰图标
        toast.Children.Add(new SharpVectors.Converters.SvgViewbox
        {
            Source = new Uri("/Assets/Group126548867.svg", UriKind.Relative),
            Stretch = Stretch.Fill
        });

        // 内边框
        toast.Children.Add(new System.Windows.Shapes.Path
        {
            Width = 340,
            Height = 80,
            Data = Geometry.Parse("M339.5 0.5V73.793L333.793 79.5H0.5V6.20703L6.20703 0.5H339.5Z"),
            Stroke = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
            StrokeThickness = 1,
            Stretch = Stretch.Fill
        });

        // 内容层：图标 + 文字
        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 红色警告图标（表示失败）
        var iconCanvas = new Canvas { Width = 22, Height = 22 };
        iconCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M11 21C16.5228 21 21 16.5228 21 11C21 5.47715 16.5228 1 11 1C5.47715 1 1 5.47715 1 11C1 16.5228 5.47715 21 11 21Z"),
            Stroke = new SolidColorBrush(Color.FromRgb(0xC6, 0x0E, 0x0E)),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        });
        iconCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M11.0508 5.66602V11.0506"),
            Stroke = new SolidColorBrush(Color.FromRgb(0xC6, 0x0E, 0x0E)),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        });
        iconCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M11.0496 15.9234C11.6443 15.9234 12.1265 15.4412 12.1265 14.8465C12.1265 14.2517 11.6443 13.7695 11.0496 13.7695C10.4548 13.7695 9.97266 14.2517 9.97266 14.8465C9.97266 15.4412 10.4548 15.9234 11.0496 15.9234Z"),
            Fill = new SolidColorBrush(Color.FromRgb(0xC6, 0x0E, 0x0E))
        });

        var iconViewbox = new Viewbox { Width = 22, Height = 22, Margin = new Thickness(0, 0, 20, 0), Child = iconCanvas };
        contentPanel.Children.Add(iconViewbox);

        contentPanel.Children.Add(new TextBlock
        {
            Text = LocalizationService.Instance["Game.ConfigFailed"],
            FontSize = 30,
            Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        });

        toast.Children.Add(contentPanel);
        rootPanel.Children.Add(toast);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (rootPanel.Children.Contains(toast))
                rootPanel.Children.Remove(toast);
        };
        timer.Start();
    }

    // ═══════════════════════════════════════════════════
    // 启动游戏及自定义路径
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 右侧详情面板的"启动游戏"按钮点击：自动应用预设后根据当前选中的游戏和启动模式执行启动，
    /// 启动成功则保存用户数据，失败则弹出错误对话框。
    /// </summary>
    private void LaunchGameButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (_selectedGame == null) return;

        ApplyPresetsIfAutoApplyEnabled();

        var mode = _selectedGame?.LaunchMode == LaunchModeUdf.CustomPath ? LaunchMode.CustomPath : LaunchMode.Steam;
        if (GameLauncher.Launch(_selectedGame, mode))
        {
            _gameDataService?.SaveUserData(_selectedGame);
            return;
        }

        ShowLaunchErrorDialog();
    }

    /// <summary>
    /// 启动模式单选按钮变化：切换 Steam 启动与自定义路径启动模式。
    /// 切换时自动保存用户偏好到本地数据。
    /// </summary>
    private void LaunchModeRadio_Changed(object sender, RoutedEventArgs e)
    {
        if (CustomPathPanel == null || _selectedGame == null) return;

        if (CustomLaunchRadio.IsChecked == true)
        {
            _selectedGame.LaunchMode = LaunchModeUdf.CustomPath;
            CustomPathPanel.Visibility = Visibility.Visible;
            if (!string.IsNullOrEmpty(_selectedGame.LaunchPath))
            {
                CustomPathText.Text = _selectedGame.LaunchPath;
            }
            else
            {
                SetCustomPathPlaceholder();
            }
        }
        else
        {
            _selectedGame.LaunchMode = LaunchModeUdf.Steam;
            CustomPathPanel.Visibility = Visibility.Collapsed;
        }

        _gameDataService?.SaveUserData(_selectedGame);
    }

    /// <summary>
    /// 浏览并选择自定义游戏启动文件（.exe）。
    /// 选择后更新路径显示；若文件存在但游戏原标记为未安装则自动标记为已安装并刷新筛选。
    /// </summary>
    private void BrowseCustomPath_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        
        var dialog = new OpenFileDialog
        {
            Filter = LocalizationService.Instance["Game.ExeFilter"],
            Title = LocalizationService.Instance["Game.SelectGameExe"]
        };

        if (dialog.ShowDialog() == true)
        {
            CustomPathText.Text = dialog.FileName;
            if (_selectedGame != null)
            {
                _selectedGame.LaunchPath = dialog.FileName;
                if (!_selectedGame.IsInstalled && File.Exists(dialog.FileName))
                {
                    _selectedGame.IsInstalled = true;
                    ApplyFilter(_currentFilter);
                    SelectGame(_selectedGame);
                }
                _gameDataService?.SaveUserData(_selectedGame);
            }
        }
    }

    /// <summary>
    /// 自动应用预设复选框状态变化处理。
    /// </summary>
    private void AutoApplyPreset_Changed(object sender, RoutedEventArgs e)
    {
        // 状态由 AutoApplyPresetCheckBox.IsChecked 实时反映，无需额外处理
    }

    /// <summary>
    /// 游戏启动时，若"自动应用预设"开关打开，则将四个下拉框中选中的预设下发到对应设备。
    /// </summary>
    private void ApplyPresetsIfAutoApplyEnabled()
    {
        if (AutoApplyPresetCheckBox?.IsChecked != true)
            return;

        ApplySinglePresetFromComboBox(BasePresetComboBox, Models.Usb.DeviceType.Base);
        ApplySinglePresetFromComboBox(WheelPresetComboBox, Models.Usb.DeviceType.Wheel);
        ApplySinglePresetFromComboBox(PedalPresetComboBox, Models.Usb.DeviceType.Pedal);
        ApplySinglePresetFromComboBox(ShifterPresetComboBox, Models.Usb.DeviceType.Shifter);
    }

    /// <summary>
    /// 从指定下拉框获取选中的预设，并下发到对应类型的已连接设备。
    /// </summary>
    private static void ApplySinglePresetFromComboBox(ComboBox comboBox, Models.Usb.DeviceType deviceType)
    {
        if (comboBox.SelectedItem is not ComboBoxItem item || item.Tag is not PresetItem preset)
            return;

        // 查找对应设备类型的已连接设备
        var connectedDevices = App.UsbManager?.ConnectedDevices
            ?? System.Collections.ObjectModel.ReadOnlyCollection<UsbDeviceInfo>.Empty;

        var targetDevice = connectedDevices.FirstOrDefault(d =>
        {
            var descriptor = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
            return descriptor != null && descriptor.DeviceType == deviceType
                   && descriptor.IsNormalMode(d.Vid, d.Pid);
        });

        if (targetDevice == null)
        {
            Debug.WriteLine($"[GameUI] 未找到已连接的{deviceType}设备，跳过预设应用: {preset.Name}");
            return;
        }

        try
        {
            switch (deviceType)
            {
                case Models.Usb.DeviceType.Pedal when preset.PedalParameters != null:
                    ApplyPedalPreset(targetDevice, preset);
                    break;
                case Models.Usb.DeviceType.Wheel when preset.WheelParameters != null:
                    ApplyWheelPreset(targetDevice, preset);
                    break;
                case Models.Usb.DeviceType.Base when preset.BaseParameters != null:
                    ApplyBasePreset(targetDevice, preset);
                    break;
                // Shifter 的预设快照模型尚未实现，暂跳过
                default:
                    Debug.WriteLine($"[GameUI] {deviceType} 预设应用尚未实现或参数为空: {preset.Name}");
                    return;
            }

            // 下发预设名称到设备
            if (App.ProtocolService != null)
                App.ProtocolService.SetPresetName(targetDevice.DeviceKey, deviceType, preset.Name);

            Debug.WriteLine($"[GameUI] 预设已应用到{deviceType}设备: {preset.Name}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameUI] 应用预设失败 ({deviceType}): {ex.Message}");
        }
    }

    /// <summary>将踏板预设参数下发到设备</summary>
    private static void ApplyPedalPreset(UsbDeviceInfo device, PresetItem preset)
    {
        if (App.UsbManager == null || preset.PedalParameters == null) return;

        var p = preset.PedalParameters;
        var clutchPoints = new byte[]
        {
            p.ClutchPoint1Y, p.ClutchPoint1X,
            p.ClutchPoint2Y, p.ClutchPoint2X,
            p.ClutchPoint3Y, p.ClutchPoint3X,
            p.ClutchPoint4Y, p.ClutchPoint4X,
        };
        var brakePoints = new byte[]
        {
            p.BrakePoint1Y, p.BrakePoint1X,
            p.BrakePoint2Y, p.BrakePoint2X,
            p.BrakePoint3Y, p.BrakePoint3X,
            p.BrakePoint4Y, p.BrakePoint4X,
        };
        var throttlePoints = new byte[]
        {
            p.ThrottlePoint1Y, p.ThrottlePoint1X,
            p.ThrottlePoint2Y, p.ThrottlePoint2X,
            p.ThrottlePoint3Y, p.ThrottlePoint3X,
            p.ThrottlePoint4Y, p.ThrottlePoint4X,
        };

        var cmd = DeviceProtocolService.BuildSetPedalParametersCommand(
            p.ClutchDirection, clutchPoints, p.ClutchDeadZoneFront, p.ClutchDeadZoneRear,
            p.BrakeDirection, brakePoints, p.BrakeDeadZoneFront, p.BrakeDeadZoneRear,
            p.ThrottleDirection, throttlePoints, p.ThrottleDeadZoneFront, p.ThrottleDeadZoneRear);

        App.UsbManager.SendToDevice(device.DeviceKey, cmd);
    }

    /// <summary>将面盘预设参数下发到设备（6 条协议命令）</summary>
    private static void ApplyWheelPreset(UsbDeviceInfo device, PresetItem preset)
    {
        if (App.UsbManager == null || preset.WheelParameters == null) return;

        var s = preset.WheelParameters;

        // 0x2103 转速灯基础模式
        var rpmLedColors = new byte[12][];
        for (int i = 0; i < 12; i++)
        {
            var idx = Math.Clamp(s.RpmColors[i], 0, 8);
            rpmLedColors[i] = (byte[])DeviceProtocolService.ColorIndexToRgb[idx].Clone();
        }
        var cmd1 = DeviceProtocolService.BuildSetWheelRpmBaseModeCommand(
            (byte)s.RpmBaseLightMode, (byte)s.RpmBaseLightSpeed, rpmLedColors);
        App.UsbManager.SendToDevice(device.DeviceKey, cmd1);

        // 0x2104 转速灯转速指示
        var triggerValues = new ushort[12];
        var triggerLedColors = new byte[12][];
        for (int i = 0; i < 12; i++)
        {
            triggerValues[i] = (ushort)s.RpmValues[i];
            var ci = Math.Clamp(s.RpmColors[i], 0, 8);
            triggerLedColors[i] = (byte[])DeviceProtocolService.ColorIndexToRgb[ci].Clone();
        }
        var cmd2 = DeviceProtocolService.BuildSetWheelRpmIndicatorCommand(
            (byte)s.RpmDisplayMode, triggerValues, triggerLedColors);
        App.UsbManager.SendToDevice(device.DeviceKey, cmd2);

        // 0x2105 转速灯模式属性
        var strobeColorIdx = Math.Clamp(s.RpmStrobeColor, 0, 8);
        var strobeColor = DeviceProtocolService.ColorIndexToRgb[strobeColorIdx];
        var cmd3 = DeviceProtocolService.BuildSetWheelRpmModeCommand(
            (byte)s.RpmBrightness, (byte)(s.RpmTelemetryEnabled ? 0 : 1), (byte)s.RpmLightMode,
            (byte)s.RpmStrobeMode, (byte)s.RpmSpeed,
            strobeColor[0], strobeColor[1], strobeColor[2], (byte)s.RpmCapValue);
        App.UsbManager.SendToDevice(device.DeviceKey, cmd3);

        // 0x2106 按键灯全局属性
        var unifiedColorIdx = Math.Clamp(s.GlobalKeyColor, 0, 8);
        var unifiedColor = DeviceProtocolService.ColorIndexToRgb[unifiedColorIdx];
        var cmd4 = DeviceProtocolService.BuildSetWheelButtonLightGlobalCommand(
            (byte)(s.KeyColorEnabled ? 1 : 0), (byte)s.KeyBrightness,
            unifiedColor[0], unifiedColor[1], unifiedColor[2]);
        App.UsbManager.SendToDevice(device.DeviceKey, cmd4);

        // 0x2107 按键灯单独效果（14 个可调按键）
        for (int adjIdx = 0; adjIdx < 14; adjIdx++)
        {
            var btnColorIdx = Math.Clamp(s.ButtonColors[adjIdx], 0, 8);
            var btnColor = DeviceProtocolService.ColorIndexToRgb[btnColorIdx];
            var telemetryFunc = s.ButtonTelemetryEnabled[adjIdx]
                ? (byte)(s.ButtonTelemetryFunc[adjIdx] + 1) : (byte)0;
            var flashSpeed = s.ButtonTelemetryLightEffect[adjIdx] == 0
                ? (byte)0xFF : (byte)s.ButtonSpeeds[adjIdx];
            var tcIdx = Math.Clamp(s.ButtonTelemetryTriggerColor[adjIdx], 0, 8);
            var tcColor = DeviceProtocolService.ColorIndexToRgb[tcIdx];

            var cmd5 = DeviceProtocolService.BuildSetWheelButtonLightCommand(
                (byte)adjIdx, btnColor[0], btnColor[1], btnColor[2],
                telemetryFunc, flashSpeed, tcColor[0], tcColor[1], tcColor[2]);
            App.UsbManager.SendToDevice(device.DeviceKey, cmd5);
        }

        // 0x2108 睡眠和拨片
        var sleepTime = s.SleepLightDuration switch
        {
            0 => (byte)1, 1 => (byte)2, 2 => (byte)3, 3 => (byte)4, 4 => (byte)5, 5 => (byte)0, _ => (byte)5
        };
        var sleepEffect = s.StandbyLightEffect == 0 ? (byte)1 : (byte)0;
        var clutchPaddleMode = s.ClutchMode switch
        {
            0 => (byte)1, 1 => (byte)0, 2 => (byte)2, _ => (byte)0
        };
        var cmd6 = DeviceProtocolService.BuildSetWheelSleepAndPaddleCommand(
            sleepTime, sleepEffect, (byte)s.GlobalFlashSpeed,
            clutchPaddleMode, (byte)Math.Round(s.ClutchPointValue));
        App.UsbManager.SendToDevice(device.DeviceKey, cmd6);
    }

    /// <summary>将基座预设参数下发到设备（0x2101 协议）</summary>
    private static void ApplyBasePreset(UsbDeviceInfo device, PresetItem preset)
    {
        if (App.UsbManager == null || preset.BaseParameters == null) return;

        var p = preset.BaseParameters;
        var cmd = DeviceProtocolService.BuildSetBaseParametersCommand(
            p.MaxSteeringAngle, p.LimitRigidity, p.MaxSpeed, p.SmoothLevel,
            p.ForceStrength, p.MechInertia, p.MechCentering, p.MechDamping,
            p.MechFriction, p.GameInertia, p.GameElastic, p.GameDamping,
            p.GameFriction, p.GameInertiaStr, p.HandsOffProtect, p.ForceReverse);

        App.UsbManager.SendToDevice(device.DeviceKey, cmd);
    }

    /// <summary>
    /// 显示启动失败对话框，提供"重试"和"取消"两个操作按钮。
    /// </summary>
    private void ShowLaunchErrorDialog()
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            var dialog = mainWindow.GlobalDialogControl;

            dialog.Title = LocalizationService.Instance["Dialog.Prompt"];
            dialog.ShowIcon = true;
            dialog.ClearButtons();

            dialog.DialogContent = new TextBlock
            {
                Text = LocalizationService.Instance["Dialog.LaunchFailedMessage"],
                FontSize = 22,
                Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Regular
            };

            dialog.AddButton(LocalizationService.Instance["Dialog.Restart"], (s, args) =>
            {
                dialog.Hide();
                if (_selectedGame != null)
                {
                    ApplyPresetsIfAutoApplyEnabled();
                    var mode = _selectedGame?.LaunchMode == LaunchModeUdf.CustomPath ? LaunchMode.CustomPath : LaunchMode.Steam;
                    if (GameLauncher.Launch(_selectedGame, mode))
                        _gameDataService?.SaveUserData(_selectedGame);
                }
            }, isPrimary: true);

            dialog.AddButton(LocalizationService.Instance["Dialog.Cancel"], (s, args) =>
            {
                dialog.Hide();
            });

            dialog.Show();
        }
    }

    // ═══════════════════════════════════════════════════
    // 置顶（Pin）动画 — FLIP 技术实现卡片位置平滑过渡
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 置顶按钮点击：切换游戏的置顶状态。
    /// 同一时间只能有一个游戏被置顶；取消置顶时卡片滑回对应的排位位置。
    /// </summary>
    private void PinButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isPinning || _filteredGameList == null) return;

        if (sender is FrameworkElement element && element.DataContext is GameItem gameItem)
        {
            e.Handled = true;
            _isPinning = true;

            var currentIndex = _filteredGameList.IndexOf(gameItem);

            if (gameItem.IsPinned)
            {
                gameItem.IsPinned = false;
                _gameDataService?.SaveUserData(gameItem);
                var pinnedCount = _filteredGameList.Count(g => g.IsPinned);
                var newIndex = pinnedCount;
                if (currentIndex != newIndex)
                {
                    AnimateMoveToPosition(currentIndex, newIndex);
                }
                else
                {
                    _isPinning = false;
                }
            }
            else
            {
                var currentPinned = _filteredGameList.FirstOrDefault(g => g.IsPinned);
                if (currentPinned != null && currentPinned != gameItem)
                {
                    currentPinned.IsPinned = false;
                    _gameDataService?.SaveUserData(currentPinned);
                }

                gameItem.IsPinned = true;
                _gameDataService?.SaveUserData(gameItem);

                if (currentIndex != 0)
                {
                    AnimateMoveToPosition(currentIndex, 0);
                }
                else
                {
                    _isPinning = false;
                }
            }
        }
    }

    /// <summary>
    /// 使用 FLIP（First, Last, Invert, Play）动画技术将列表项从 fromIndex 移动到 toIndex。
    /// 先记录所有受影响卡片的旧位置（First），移动数据源后读取新位置（Last），
    /// 计算位移差值（Invert），最后通过 TranslateTransform 动画平滑过渡到目标位置（Play）。
    /// </summary>
    private void AnimateMoveToPosition(int fromIndex, int toIndex)
    {
        if (_filteredGameList == null || fromIndex == toIndex)
        {
            _isPinning = false;
            return;
        }

        // FLIP 第一步 — First：记录所有受影响卡片在移动前的当前位置
        var positionMap = new Dictionary<int, double>();
        int minIndex = Math.Min(fromIndex, toIndex);
        int maxIndex = Math.Max(fromIndex, toIndex);

        for (int i = minIndex; i <= maxIndex; i++)
        {
            var container = GameListItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
            if (container != null)
            {
                var pos = container.TransformToAncestor(GameListItemsControl).Transform(new Point(0, 0));
                positionMap[i] = pos.X;
            }
        }

        // FLIP 第二步 — Last：移动数据源中的元素，强制布局更新以获取新位置
        var movingItem = _filteredGameList[fromIndex];
        _filteredGameList.Move(fromIndex, toIndex);
        GameListItemsControl.UpdateLayout();

        var storyboard = new Storyboard();
        var duration = new Duration(TimeSpan.FromMilliseconds(450));
        var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

        // FLIP 第三步/第四步 — Invert + Play：计算新旧位置差值作为动画起始值，通过动画驱动回到 0
        for (int i = minIndex; i <= maxIndex; i++)
        {
            var container = GameListItemsControl.ItemContainerGenerator.ContainerFromItem(_filteredGameList[i]) as FrameworkElement;

            // getIndexInMap 将移动后的索引映射回原来的位置以查找旧的 X 坐标
            if (container != null && positionMap.TryGetValue(getIndexInMap(i, fromIndex, toIndex), out double oldX))
            {
                var newPos = container.TransformToAncestor(GameListItemsControl).Transform(new Point(0, 0));
                double deltaX = oldX - newPos.X; // 计算需要补偿的位移差值（Invert）

                // 使用 TranslateTransform 做位移动画（仅影响渲染，不触发布局重排）
                if (container.RenderTransform is not TranslateTransform)
                {
                    container.RenderTransform = new TranslateTransform();
                }

                // 被移动的卡片提高 Z 层级，确保它在动画期间显示于其他卡片之上
                Panel.SetZIndex(container, (_filteredGameList[i] == movingItem) ? 999 : 0);

                var anim = new DoubleAnimation
                {
                    From = deltaX,
                    To = 0,
                    Duration = duration,
                    EasingFunction = easing
                };

                Storyboard.SetTarget(anim, container);
                Storyboard.SetTargetProperty(anim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                storyboard.Children.Add(anim);
            }
        }

        int getIndexInMap(int currentIndex, int fIdx, int tIdx)
        {
            if (currentIndex == tIdx) return fIdx;
            if (fIdx > tIdx) return currentIndex - 1;
            return currentIndex + 1;
        }

        storyboard.Completed += (s, e) => {
            _isPinning = false;

            foreach (var item in _filteredGameList)
            {
                var c = GameListItemsControl.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (c != null)
                {
                    Panel.SetZIndex(c, 0);

                    if (c.RenderTransform is TranslateTransform t)
                    {
                        t.BeginAnimation(TranslateTransform.XProperty, null);
                        t.X = 0;
                    }
                }
            }
        };

        storyboard.Begin();
    }

    /// <summary>
    /// 卡片上的启动按钮点击：先选中对应游戏，自动应用预设后再执行启动逻辑。
    /// </summary>
    private void LaunchButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (sender is FrameworkElement element && element.DataContext is GameItem gameItem)
        {
            SelectGame(gameItem);

            ApplyPresetsIfAutoApplyEnabled();

            var mode = _selectedGame?.LaunchMode == LaunchModeUdf.CustomPath ? LaunchMode.CustomPath : LaunchMode.Steam;
            if (GameLauncher.Launch(gameItem, mode))
            {
                _gameDataService?.SaveUserData(gameItem);
                return;
            }

            ShowLaunchErrorDialog();
        }
    }

    // ═══════════════════════════════════════════════════
    // 自定义滚动条 — 鼠标滚轮、拖拽、拇指尺寸计算
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 自定义鼠标滚轮行为：将垂直滚轮操作转换为水平滚动，
    /// 除以 3 以降低滚动灵敏度。
    /// </summary>
    private void GameScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            e.Handled = true;
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta / 3);
        }
    }

    /// <summary>
    /// 滚动内容变化时同步更新自定义滚动条拇指的位置和大小。
    /// </summary>
    private void GameScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateScrollbarThumb();
    }

    /// <summary>
    /// 根据滚动区域的 Viewport/Extent 比例计算并更新自定义滚动条拇指的宽度和位置。
    /// 拇指宽度 = (可视区域宽度 / 内容总宽度) × 轨道宽度，最小为 10px。
    /// 拇指位置 = (当前偏移 / 最大可滚动距离) × 轨道可用移动空间。
    /// </summary>
    private void UpdateScrollbarThumb()
    {
        if (GameScrollViewer == null || ScrollbarThumb == null || ScrollbarTrack == null) return;

        var viewportWidth = GameScrollViewer.ViewportWidth;
        var extentWidth = GameScrollViewer.ExtentWidth;
        var horizontalOffset = GameScrollViewer.HorizontalOffset;

        // 内容未超出可视区域时，拇指撑满轨道并重置位置
        if (extentWidth <= viewportWidth)
        {
            ScrollbarThumb.Width = ScrollbarTrack.ActualWidth;
            ResetThumbPosition();
            return;
        }

        // 拇指宽度 = 可视比例 × 轨道宽度，不低于 10px 以保证可操作
        var thumbWidth = Math.Max(10, (viewportWidth / extentWidth) * ScrollbarTrack.ActualWidth);
        ScrollbarThumb.Width = thumbWidth;

        // 拇指位置 = 滚动偏移 / 最大偏移 × 轨道可用空间
        var maxOffset = extentWidth - viewportWidth;
        var thumbPosition = (horizontalOffset / maxOffset) * (ScrollbarTrack.ActualWidth - thumbWidth);

        // 通过 RenderTransform 中的 TranslateTransform 控制拇指水平位置
        if (ScrollbarThumb.RenderTransform is TransformGroup transformGroup)
        {
            foreach (var transform in transformGroup.Children)
            {
                if (transform is TranslateTransform translateTransform)
                {
                    translateTransform.X = thumbPosition;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 将自定义滚动条拇指位置重置到轨道最左侧（X=0）。
    /// </summary>
    private void ResetThumbPosition()
    {
        if (ScrollbarThumb?.RenderTransform is TransformGroup transformGroup)
        {
            foreach (var transform in transformGroup.Children)
            {
                if (transform is TranslateTransform translateTransform)
                {
                    translateTransform.X = 0;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 滚动条拇指按下：开始拖拽，捕获鼠标并记录起始位置。
    /// </summary>
    private void ScrollbarThumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingThumb = true;
        _lastMousePosition = e.GetPosition(ScrollbarTrack);
        ScrollbarThumb.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// 滚动条拇指拖拽移动：根据鼠标位移量按比例换算为内容滚动偏移。
    /// </summary>
    private void ScrollbarThumb_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingThumb || GameScrollViewer == null || ScrollbarTrack == null) return;

        Point currentPosition = e.GetPosition(ScrollbarTrack);
        double deltaX = currentPosition.X - _lastMousePosition.X;

        double extentWidth = GameScrollViewer.ExtentWidth;
        double viewportWidth = GameScrollViewer.ViewportWidth;
        double scrollableWidth = extentWidth - viewportWidth;

        double maxThumbX = ScrollbarTrack.ActualWidth - ScrollbarThumb.Width;

        if (maxThumbX > 0 && scrollableWidth > 0)
        {
            double deltaOffset = (deltaX / maxThumbX) * scrollableWidth;
            GameScrollViewer.ScrollToHorizontalOffset(GameScrollViewer.HorizontalOffset + deltaOffset);
        }

        _lastMousePosition = currentPosition;
        e.Handled = true;
    }

    /// <summary>
    /// 滚动条拇指释放：结束拖拽状态，释放鼠标捕获。
    /// </summary>
    private void ScrollbarThumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingThumb)
        {
            _isDraggingThumb = false;
            ScrollbarThumb.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    // ═══════════════════════════════════════════════════
    // 遥测数据模拟 — 监听遥测服务事件并定时刷新状态
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 启动遥测数据模拟：订阅遥测服务的启动/停止/发包事件，
    /// 并启动 500ms 定时器用于周期性刷新 UI 状态。
    /// </summary>
    private void StartTelemetrySimulation()
    {
        var telemetryService = App.TelemetryService;
        if (telemetryService == null) return;

        // 订阅遥测启动事件
        telemetryService.OnStarted += gameId =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                Debug.WriteLine($"[GameUI] 遥测已启动, GameId={gameId}");
            });
        };

        // 订阅遥测停止事件，重置发包计数
        telemetryService.OnStopped += () =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _packetCount = 0;
                Debug.WriteLine("[GameUI] 遥测已停止");
            });
        };

        // 订阅遥测发包事件
        // OnPacketsDispatched 在后台线程中触发，需要通过 Dispatcher 调度到 UI 线程更新计数
        telemetryService.OnPacketsDispatched += _ =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _packetCount++;
            });
        };

        // 启动遥测状态刷新定时器，每 500ms 触发一次
        _telemetryAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _telemetryAnimationTimer.Tick += (_, _) =>
        {
            // 定期刷新遥测状态（如有 UI 绑定可在此更新）
        };
        _telemetryAnimationTimer.Start();
    }

    /// <summary>
    /// 遥测动画定时器 Tick 事件处理（已废弃，保留用于兼容）。
    /// </summary>
    private void TelemetryAnimationTimer_Tick(object? sender, EventArgs e)
    {
        // 此方法不再使用，遥测数据由 TelemetryService 后台线程驱动
    }

    // ═══════════════════════════════════════════════════
    // 悬停（Hover）效果 — 鼠标进入/移出时平移后续卡片
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 卡片点击：选中对应的游戏。
    /// </summary>
    private void CardRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is GameItem gameItem)
        {
            SelectGame(gameItem);
        }
    }

    /// <summary>
    /// 鼠标进入卡片区域：将该卡片之后的所有卡片向右平移 8.12 像素，
    /// 产生视觉上的"推开"效果。
    /// </summary>
    private void CardRoot_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is GameItem gameItem)
        {
            int hoveredIndex = _filteredGameList?.IndexOf(gameItem) ?? -1;
            if (hoveredIndex >= 0)
            {
                // 将该索引之后的所有卡片向右平移 8.12 像素
                ShiftRightSiblings(hoveredIndex, 8.12);
            }
        }
    }

    /// <summary>
    /// 鼠标离开卡片区域：将后续所有卡片平移恢复到 0，即收回"推开"效果。
    /// </summary>
    private void CardRoot_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is GameItem gameItem)
        {
            int hoveredIndex = _filteredGameList?.IndexOf(gameItem) ?? -1;
            if (hoveredIndex >= 0)
            {
                // 鼠标移出，所有后续卡片回归 0 位移
                ShiftRightSiblings(hoveredIndex, 0);
            }
        }
    }

    /// <summary>
    /// 将指定索引之后的所有卡片通过 GPU 友好的 TranslateTransform 动画水平平移。
    /// 使用 RenderTransform 而非 LayoutTransform，因此变换仅影响渲染结果，
    /// 不触发 WPF 布局系统的 Measure/Arrange 重排，性能开销极低。
    /// </summary>
    /// <param name="startIndex">起始卡片索引（不含此卡片本身）</param>
    /// <param name="offsetX">目标水平位移（像素），0 表示恢复原位</param>
    private void ShiftRightSiblings(int startIndex, double offsetX)
    {
        if (GameListItemsControl == null || _filteredGameList == null) return;

        var duration = new Duration(TimeSpan.FromSeconds(0.3));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        // 从当前卡片的下一个索引开始遍历（鼠标悬停的卡片本身不移动）
        for (int i = startIndex + 1; i < _filteredGameList.Count; i++)
        {
            // 获取 ItemsControl 生成的 UI 容器元素
            if (GameListItemsControl.ItemContainerGenerator.ContainerFromIndex(i) is UIElement container)
            {
                // 确保容器拥有 TranslateTransform 以支持位移动画
                if (container.RenderTransform is not TranslateTransform)
                {
                    container.RenderTransform = new TranslateTransform();
                }

                var transform = (TranslateTransform)container.RenderTransform;
                var anim = new DoubleAnimation
                {
                    To = offsetX,
                    Duration = duration,
                    EasingFunction = easing
                };

                // 基于 GPU 加速的渲染变换动画，零布局重排
                transform.BeginAnimation(TranslateTransform.XProperty, anim);
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 本地化与 UI 适配 — 语言切换、遥测按钮形状重绘
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 语言切换时更新游戏介绍文本和适配按钮。
    /// </summary>
    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null) // null 表示全部属性刷新（SetLanguage 触发）
        {
            UpdateGameDescription();
            // 延迟更新：等布局完成（内容层文本已变化、ActualWidth 已定）后再重绘背景
            Dispatcher.BeginInvoke(new Action(UpdateTelemetryButtonShape), DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// 遥测配置按钮内容区域尺寸变化时，重新绘制背景平行四边形。
    /// </summary>
    private void TelemetryButtonContent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTelemetryButtonShape();
    }

    /// <summary>
    /// 根据内容层实际宽度绘制平行四边形背景，切角偏移固定不变。
    /// </summary>
    private void UpdateTelemetryButtonShape()
    {
        var w = TelemetryButtonContent.ActualWidth;
        if (w <= 0) return;

        TelemetryButtonBackground.Width = w;
        TelemetryButtonBackground.Data = Geometry.Parse(
            $"M0,5.0625 V27 H{w - 6.19048:F4} L{w},21.9375 V0 H6.19048 Z");
    }

    /// <summary>
    /// 根据当前语言选择中文或英文描述。
    /// </summary>
    private void UpdateGameDescription()
    {
        if (_selectedGame == null) return;

        var isEnglish = LocalizationService.Instance.CurrentLanguage != "zh-CN";
        var desc = isEnglish && !string.IsNullOrEmpty(_selectedGame.DescriptionEn)
            ? _selectedGame.DescriptionEn
            : _selectedGame.Description;
        GameDescriptionText.Text = desc;
    }

    // ═══════════════════════════════════════════════════
    // 预设下拉框 — 从 PresetService 加载真实预设数据
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 从 PresetService 加载官方和个人预设，填充四个设备类型的预设下拉框。
    /// 每个下拉框首个选项为 "Default"（设备内置默认预设），
    /// 随后按官方预设 → 个人预设的顺序列出所有可用预设。
    /// </summary>
    private void PopulatePresetComboBoxes()
    {
        if (App.PresetService == null) return;

        PopulateSinglePresetComboBox(BasePresetComboBox, Models.Usb.DeviceType.Base);
        PopulateSinglePresetComboBox(WheelPresetComboBox, Models.Usb.DeviceType.Wheel);
        PopulateSinglePresetComboBox(PedalPresetComboBox, Models.Usb.DeviceType.Pedal);
        PopulateSinglePresetComboBox(ShifterPresetComboBox, Models.Usb.DeviceType.Shifter);
    }

    /// <summary>
    /// 为单个 ComboBox 填充预设列表。
    /// 按官方预设、个人预设的顺序列出所有可用预设，不设默认选中项。
    /// </summary>
    private static void PopulateSinglePresetComboBox(ComboBox comboBox, Models.Usb.DeviceType deviceType)
    {
        var items = comboBox.Items;
        items.Clear();

        // 加载官方预设
        var officialPresets = App.PresetService!.LoadOfficialPresets(deviceType);
        foreach (var preset in officialPresets)
        {
            items.Add(new ComboBoxItem
            {
                Content = preset.Name,
                Tag = preset
            });
        }

        // 加载个人预设
        var personalPresets = App.PresetService.LoadPersonalPresets(deviceType);
        foreach (var preset in personalPresets)
        {
            items.Add(new ComboBoxItem
            {
                Content = preset.Name,
                Tag = preset
            });
        }

        // 不设默认选中项，下拉框为空显示
        comboBox.SelectedIndex = -1;
    }

    /// <summary>
    /// 预设下拉框选择变更的统一处理入口。
    /// 当 AutoApplyPreset 开启时，选中的预设将在游戏启动时自动应用到对应设备。
    /// </summary>
    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 预设选择变更时的逻辑：
        // - 仅记录选中项；实际预设应用由游戏启动流程（LaunchGameButton_Click）触发
        // - 启动时根据 AutoApplyPresetCheckBox 状态决定是否下发预设到设备
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
        {
            var presetName = item.Content?.ToString() ?? "Default";
            System.Diagnostics.Debug.WriteLine($"[GameUI] 预设选择变更: {comboBox.Name} -> {presetName}");
        }
    }

    /// <summary>
    /// ComboBox 鼠标按下事件：通过 MouseButtonEventArgs 的 OriginalSource
    /// 判断是否点击了编辑图标。如果是则拦截事件并跳转到设备参数界面的预设列表弹窗。
    /// 如果当前有选中的预设，则在弹窗中定位并选中该预设。
    /// </summary>
    private void PresetComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ComboBox comboBox) return;

        // 沿视觉树向上查找，判断点击是否来自编辑图标（PresetEditIcon）
        if (!HitTestOriginatedFromPresetEditIcon(e.OriginalSource as DependencyObject, comboBox))
            return; // 非编辑图标，正常展开下拉框

        // 点击编辑图标：拦截事件，跳转到预设弹窗
        e.Handled = true;

        var selectedPresetName = (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

        var (deviceType, tabIndex) = comboBox.Name switch
        {
            "BasePresetComboBox" => (Models.Usb.DeviceType.Base, 0),
            "WheelPresetComboBox" => (Models.Usb.DeviceType.Wheel, 1),
            "PedalPresetComboBox" => (Models.Usb.DeviceType.Pedal, 2),
            "ShifterPresetComboBox" => (Models.Usb.DeviceType.Shifter, 3),
            _ => (Models.Usb.DeviceType.Base, 0)
        };

        NavigateToPresetPopup(deviceType, tabIndex, selectedPresetName);
    }

    /// <summary>
    /// 沿视觉树向上查找，判断点击源是否来自 ComboBox 模板中的 PresetEditIcon。
    /// </summary>
    private static bool HitTestOriginatedFromPresetEditIcon(DependencyObject? source, ComboBox comboBox)
    {
        var editIcon = comboBox.Template.FindName("PresetEditIcon", comboBox);
        if (editIcon == null) return false;

        while (source != null)
        {
            if (ReferenceEquals(source, editIcon))
                return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    /// <summary>
    /// 导航到设备参数界面并打开预设列表弹窗，可选地定位到指定预设。
    /// </summary>
    private static void NavigateToPresetPopup(Models.Usb.DeviceType deviceType, int tabIndex, string? presetName)
    {
        if (Window.GetWindow(Application.Current.MainWindow) is not MainWindow mainWindow)
            return;

        var vm = mainWindow.DataContext as ViewModels.MainWindowViewModel;
        if (vm == null) return;

        // 切换到设备参数页面
        var deviceItem = vm.NavigationItems.FirstOrDefault(n => n.Name == "Device");
        if (deviceItem != null)
            vm.SelectedNavigationItem = deviceItem;

        // 延迟到 UI 加载完成后：导航到对应的设备 tab 并打开预设弹窗
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            // 导航到对应设备 tab（0=基座, 1=面盘, 2=踏板, 3=排挡）
            if (vm.CurrentView is DeviceUserControl deviceView)
                deviceView.NavigateToTab(tabIndex);

            // 打开预设弹窗
            var popup = mainWindow.ShowPresetListPopup(deviceType);

            // 如果有选中预设，在弹窗中定位到该预设
            if (!string.IsNullOrEmpty(presetName))
                popup.SelectAndScrollToPreset(presetName);
        });
    }

    private void ConfigTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }
}

/// <summary>
/// 遥测设置中一个 UDP 转发目标条目的数据模型。
/// 实现 INotifyPropertyChanged 以便实时反映用户的端口 / IP 输入。
/// </summary>
internal sealed class ForwardTargetRow : INotifyPropertyChanged
{
    private bool _enabled;
    private string _port = string.Empty;
    private string _ip = string.Empty;

    /// <summary>该转发目标是否启用。</summary>
    public bool Enabled
    {
        get => _enabled;
        set { if (_enabled != value) { _enabled = value; OnPropertyChanged(); } }
    }

    /// <summary>转发目标端口号。</summary>
    public string Port
    {
        get => _port;
        set { if (_port != value) { _port = value; OnPropertyChanged(); } }
    }

    /// <summary>转发目标 IP 地址。</summary>
    public string Ip
    {
        get => _ip;
        set { if (_ip != value) { _ip = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
