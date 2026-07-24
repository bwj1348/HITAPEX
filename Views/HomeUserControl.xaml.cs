using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using HITAPEX.Controls;
using HITAPEX.Models;
using HITAPEX.Services;
using HITAPEX.Services.Data;
using HITAPEX.Services.Data.Api;

namespace HITAPEX.Views;

/// <summary>
/// 首页仪表盘用户控件，包含以下核心模块：
/// 1. 顶部 Banner 轮播图 —— 支持自动/手动切换，带指示器圆点导航
/// 2. 游戏快速启动列表 —— 支持置顶（FLIP 动画）、滚动条拖拽、鼠标悬浮位移效果
/// 3. 力反馈弧形仪表 —— 模拟方向盘力度反馈数值的圆弧形仪表盘
/// 4. 温度仪表 —— 15 段色块组成的温度指示器，颜色随温度梯度变化
/// 5. 方向盘角度显示 —— 34 段圆环形排列的刻度段，模拟方向盘旋转角度
/// 6. 踏板位置显示 —— 离合器/刹车/油门三通道柱状高度比例显示
/// </summary>
public partial class HomeUserControl : UserControl
{
    // ═══════════════════════════════════════════════════════════════
    // 常量定义
    // ═══════════════════════════════════════════════════════════════

    // —— Banner 轮播常量 ——
    private const int SlideCount = 3;
    private const int AutoPlayInterval = 5000;
    private const double AnimationDuration = 0.5;

    private List<BannerItem>? _banners;
    private Image[]? _slideImages;

    // —— 力反馈弧形仪表（Force Feedback Gauge）常量 ——
    // 圆心坐标 (71.5, 71.5)，半径 60，起始角度 135°（左下），覆盖 270° 弧长
    // 角度计算：以圆心为中心，Math.Cos(rad) 获取 X 偏移，Math.Sin(rad) 获取 Y 偏移
    // WPF 坐标系 Y 轴向下，因此正弦正值向下
    private const double GaugeCenterX = 71.5;
    private const double GaugeCenterY = 71.5;
    private const double GaugeRadius = 60;
    private const double GaugeStartAngle = 135;
    private const double GaugeTotalAngle = 270;

    // —— 温度仪表常量 ——
    // 15 个色块，每块代表 6°C（总刻度 90°C），实际模拟范围 0~120°C
    private const int TemperatureBlockCount = 15;
    private const double TemperaturePerBlock = 6.0;
    private const double MaxTemperature = 90.0;
    
    // ═══════════════════════════════════════════════════════════════
    // 字段 — Banner 轮播
    // ═══════════════════════════════════════════════════════════════
    private int _currentSlide = 0;
    private bool _isInitialized;
    private DispatcherTimer? _autoPlayTimer;
    private Border[]? _indicators;
    private Border[]? _slides;
    private bool _isAnimating = false;

    // ═══════════════════════════════════════════════════════════════
    // 字段 — 游戏列表
    // ═══════════════════════════════════════════════════════════════
    // _isPinning 防抖锁，避免置顶动画期间触发二次点击
    private ObservableCollection<GameItem>? _gameList;
    private bool _isPinning = false;
    private GameDataService? _gameDataService;
    private CancellationTokenSource? _loadGamesCts;
    private bool _isLoadingGames;

    // ═══════════════════════════════════════════════════════════════
    // 字段 — 力反馈仪表
    // ═══════════════════════════════════════════════════════════════
    private double _forceFeedbackValue = 75;
    private DispatcherTimer? _forceFeedbackAnimationTimer;
    private Random? _random;

    // ═══════════════════════════════════════════════════════════════
    // 字段 — 温度仪表
    // ═══════════════════════════════════════════════════════════════
    private Path[]? _temperatureBlocks;
    private double _temperatureValue = 0;
    private DispatcherTimer? _temperatureAnimationTimer;
    private bool _temperatureIncreasing = true;
    
    // —— 方向盘常量 ——
    // 34 段刻度围绕圆心排列（360°/34 ≈ 10.59° 间隔），模拟 ±900° 转向范围
    // 顶部一段使用粗刻度（27.52 宽），上半部分为红色区域，下半部分为暗红色区域
    private const int SteeringWheelSegmentCount = 34;
    private const double SteeringWheelRadius = 60;
    private const double SteeringWheelSegmentWidth = 16.51;
    private const double SteeringWheelSegmentHeight = 5.5;
    private const double MaxSteeringAngle = 900;

    // —— 踏板常量 ——
    // 踏板总高度 135px，顶/底边距确保填充区域始终可见
    private const double PedalHeight = 135;
    private const double PedalTopMargin = 4;
    private const double PedalBottomMargin = 5;

    // ═══════════════════════════════════════════════════════════════
    // 字段 — 方向盘
    // ═══════════════════════════════════════════════════════════════
    private Rectangle[]? _steeringWheelSegments;
    private double _steeringAngle = 0;
    private DispatcherTimer? _steeringAnimationTimer;
    private bool _steeringIncreasing = true;

    // ═══════════════════════════════════════════════════════════════
    // 字段 — 踏板模拟
    // ═══════════════════════════════════════════════════════════════
    private double _clutchValue = 0;
    private double _brakeValue = 0;
    private double _throttleValue = 0;
    private DispatcherTimer? _pedalAnimationTimer;
    private Random? _pedalRandom;

    // ═══════════════════════════════════════════════════════════════
    // 构造函数 & 初始化入口
    // ═══════════════════════════════════════════════════════════════

    public HomeUserControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 页面加载入口，初始化所有子模块：
    /// Banner 轮播、力反馈仪表、温度仪表、方向盘、踏板模拟、游戏列表
    /// 使用 _isInitialized 标志确保只初始化一次
    /// </summary>
    private void HomeUserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _indicators = new[] { Indicator0, Indicator1, Indicator2 };
        _slides = new[] { Slide0, Slide1, Slide2 };
        _slideImages = new[] { SlideImage0, SlideImage1, SlideImage2 };
        UpdateSlideWidths();
        InitializeSlidePositions();
        StartAutoPlay();

        InitializeForceFeedbackGauge();
        StartForceFeedbackSimulation();

        InitializeTemperatureGauge();
        StartTemperatureSimulation();

        InitializeSteeringWheel();
        StartSteeringSimulation();

        StartPedalSimulation();

        InitializeGameList();
        _ = LoadBannersAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    // 游戏列表 — 数据加载与初始化
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 初始化游戏列表数据源，注册 GameDataService 状态变更监听，
    /// 异步加载游戏数据，并在布局完成后更新滚动条尺寸
    /// </summary>
    private async void InitializeGameList()
    {
        _gameDataService = App.GameDataService;
        _gameDataService!.StateChanged += OnGameDataStateChanged;

        _gameList = new ObservableCollection<GameItem>();
        GameListItemsControl.ItemsSource = _gameList;

        ShowGameLoadingState();
        await LoadGamesAsync();

        Dispatcher.BeginInvoke(() => UpdateScrollbarThumb(), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 异步加载游戏列表，按置顶状态、安装状态、最近启动时间排序
    /// 网络失败时回退到本地缓存数据
    /// </summary>
    private async Task LoadGamesAsync(bool forceRefresh = false)
    {
        if (_gameDataService == null || _isLoadingGames) return;
        _isLoadingGames = true;

        _loadGamesCts?.Cancel();
        _loadGamesCts = new CancellationTokenSource();

        try
        {
            var games = await _gameDataService.GetGamesAsync(forceRefresh, _loadGamesCts.Token);
            _gameDataService.EnrichWithInstallStatus(games);

            var sorted = games
                .OrderByDescending(g => g.IsPinned)
                .ThenByDescending(g => g.IsInstalled)
                .ThenByDescending(g => g.LastLaunchTime ?? DateTime.MinValue)
                .ThenBy(g => g.Name);

            _gameList?.Clear();
            foreach (var game in sorted)
            {
                _gameList?.Add(game);
            }

            HideGameLoadingState();
        }
        catch (GameServiceException ex) when (ex.IsClientError)
        {
            HideGameLoadingState();
        }
        catch (Exception)
        {
            HideGameLoadingState();
            var cached = _gameDataService.GetCachedGames();
            if (cached != null && cached.Count > 0)
            {
                var sorted = cached
                    .OrderByDescending(g => g.IsPinned)
                    .ThenByDescending(g => g.IsInstalled)
                    .ThenByDescending(g => g.LastLaunchTime ?? DateTime.MinValue)
                    .ThenBy(g => g.Name);

                _gameList?.Clear();
                foreach (var game in sorted)
                    _gameList?.Add(game);
            }
        }
        finally
        {
            _isLoadingGames = false;
        }
    }

    private async void OnGameDataStateChanged(GameDataState state)
    {
        if (state == GameDataState.Loaded)
            await Dispatcher.InvokeAsync(async () => await LoadGamesAsync());
    }

    private void ShowGameLoadingState()
    {
        if (GameLoadingOverlay != null)
            GameLoadingOverlay.Visibility = Visibility.Visible;
    }

    private void HideGameLoadingState()
    {
        if (GameLoadingOverlay != null)
            GameLoadingOverlay.Visibility = Visibility.Collapsed;
    }

    // ═══════════════════════════════════════════════════════════════
    // 游戏列表 — 置顶动画（FLIP 模式）
    // ═══════════════════════════════════════════════════════════════
    // 采用 FLIP（First-Last-Invert-Play）动画模式：
    //   1. First  — 记录移动前所有受影响元素的绝对 X 坐标
    //   2. Last   — 瞬间执行 ObservableCollection.Move 并强制 WPF 重排
    //   3. Invert — 计算位移差值 deltaX，设置 RenderTransform 为逆向偏移
    //   4. Play   — 播放从 deltaX→0 的补间动画，实现平滑过渡
    // 防抖保护：_isPinning 标志防止动画期间二次触发
    // 关键修复：动画 From 显式指定 deltaX，避免属性锁定；
    //           Completed 中调用 BeginAnimation(null) 彻底解锁 TranslateTransform.X

    private void PinButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isPinning || _gameList == null) return;
        
        if (sender is FrameworkElement element && element.DataContext is GameItem gameItem)
        {
            e.Handled = true;
            _isPinning = true;
            
            var currentIndex = _gameList.IndexOf(gameItem);
            
            if (gameItem.IsPinned)
            {
                gameItem.IsPinned = false;
                _gameDataService?.SaveUserData(gameItem);
                var pinnedCount = _gameList.Count(g => g.IsPinned);
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
                var currentPinned = _gameList.FirstOrDefault(g => g.IsPinned);
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
    /// 执行 FLIP 动画：在 ObservableCollection.Move 前后记录坐标，计算差值后播放位移动画
    /// 通过 RenderTransform 的 TranslateTransform 实现平滑过渡，完成后解锁动画属性
    /// </summary>
    private void AnimateMoveToPosition(int fromIndex, int toIndex)
    {
        if (_gameList == null || fromIndex == toIndex)
        {
            _isPinning = false;
            return;
        }

        // 1. 【First】记录记录移动前所有图标的绝对全局 X 坐标
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

        // 2. 【Last】瞬间执行数据移动并强制 WPF 重新排版
        var movingItem = _gameList[fromIndex];
        _gameList.Move(fromIndex, toIndex);
        GameListItemsControl.UpdateLayout();

        var storyboard = new Storyboard();
        var duration = new Duration(TimeSpan.FromMilliseconds(450));
        var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

        // 3. 【Invert & Play】计算差值并播放补间动画
        for (int i = minIndex; i <= maxIndex; i++)
        {
            var container = GameListItemsControl.ItemContainerGenerator.ContainerFromItem(_gameList[i]) as FrameworkElement;

            if (container != null && positionMap.TryGetValue(getIndexInMap(i, fromIndex, toIndex), out double oldX))
            {
                var newPos = container.TransformToAncestor(GameListItemsControl).Transform(new Point(0, 0));
                double deltaX = oldX - newPos.X;

                if (container.RenderTransform is not TranslateTransform)
                {
                    container.RenderTransform = new TranslateTransform();
                }

                Panel.SetZIndex(container, (_gameList[i] == movingItem) ? 999 : 0);

                // 【关键修复 1】：显式指定 From，无视任何之前的属性锁死，强制从 deltaX 开始动画！
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

        // 辅助闭包：映射移动前的数据索引
        int getIndexInMap(int currentIndex, int fIdx, int tIdx)
        {
            if (currentIndex == tIdx) return fIdx;
            if (fIdx > tIdx) return currentIndex - 1;
            return currentIndex + 1;
        }

        // 4. 清理现场
        storyboard.Completed += (s, e) => {
            _isPinning = false;

            foreach (var item in _gameList)
            {
                var c = GameListItemsControl.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (c != null)
                {
                    Panel.SetZIndex(c, 0);

                    // 【关键修复 2】：动画结束后，传入 null 彻底解除对 X 属性的动画锁定
                    if (c.RenderTransform is TranslateTransform t)
                    {
                        t.BeginAnimation(TranslateTransform.XProperty, null);
                        t.X = 0; // 解锁后，将属性安全地归零
                    }
                }
            }
        };

        storyboard.Begin();
    }

    // ═══════════════════════════════════════════════════════════════
    // 游戏列表 — 自定义滚动条
    // ═══════════════════════════════════════════════════════════════
    // 游戏列表使用水平 ScrollViewer，底部配有自定义拖拽滚动条
    // 滚动条 thumb 尺寸按 viewport/extent 比例缩放，通过 TranslateTransform 定位

    /// <summary>
    /// 鼠标滚轮滚动事件：将滚轮 delta 映射为水平滚动偏移
    /// </summary>
    private void GameScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            e.Handled = true;
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta / 3);
        }
    }

    private void GameScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateScrollbarThumb();
    }

    /// <summary>
    /// 根据 ScrollViewer 的视口/内容比例更新自定义滚动条 thumb 的宽度和 X 偏移位置
    /// </summary>
    private void UpdateScrollbarThumb()
    {
        if (GameScrollViewer == null || ScrollbarThumb == null || ScrollbarTrack == null) return;
        
        var viewportWidth = GameScrollViewer.ViewportWidth;
        var extentWidth = GameScrollViewer.ExtentWidth;
        var horizontalOffset = GameScrollViewer.HorizontalOffset;
        
        if (extentWidth <= viewportWidth)
        {
            ScrollbarThumb.Width = ScrollbarTrack.ActualWidth;
            ResetThumbPosition();
            return;
        }
        
        var thumbWidth = Math.Max(10, (viewportWidth / extentWidth) * ScrollbarTrack.ActualWidth);
        ScrollbarThumb.Width = thumbWidth;
        
        var maxOffset = extentWidth - viewportWidth;
        var thumbPosition = (horizontalOffset / maxOffset) * (ScrollbarTrack.ActualWidth - thumbWidth);
        
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

    private bool _isDraggingThumb = false;
    private Point _lastMousePosition;

    private void ScrollbarThumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingThumb = true;
        _lastMousePosition = e.GetPosition(ScrollbarTrack);
        ScrollbarThumb.CaptureMouse();
        e.Handled = true;
    }

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

    private void ScrollbarThumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingThumb)
        {
            _isDraggingThumb = false;
            ScrollbarThumb.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 温度仪表 — 15 色块 + 渐变色彩
    // ═══════════════════════════════════════════════════════════════
    // 温度以 15 个 Path 色块呈现，每块代表 6°C（满量程 90°C，模拟 0~120°C）
    // 颜色渐变分三段：
    //   块 0~1   → 绿色 (34,197,94)
    //   块 2~8   → 绿到橙过渡（线性插值）
    //   块 9~14  → 橙到红过渡（234,179,8 → 185,28,28）

    /// <summary>
    /// 初始化温度仪表：收集 15 个色块引用并刷新初始显示
    /// </summary>
    private void InitializeTemperatureGauge()
    {
        _temperatureBlocks = new[] { TempBlock0, TempBlock1, TempBlock2, TempBlock3, TempBlock4, 
                                      TempBlock5, TempBlock6, TempBlock7, TempBlock8, TempBlock9,
                                      TempBlock10, TempBlock11, TempBlock12, TempBlock13, TempBlock14 };
        UpdateTemperatureDisplay(_temperatureValue);
    }

    /// <summary>
    /// 启动温度模拟计时器（50ms 间隔），在 0~120°C 之间来回变化
    /// </summary>
    private void StartTemperatureSimulation()
    {
        _temperatureAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _temperatureAnimationTimer.Tick += TemperatureAnimationTimer_Tick;
        _temperatureAnimationTimer.Start();
    }

    private void TemperatureAnimationTimer_Tick(object? sender, EventArgs e)
    {
        if (_temperatureIncreasing)
        {
            _temperatureValue += 1;
            if (_temperatureValue >= 120)
            {
                _temperatureValue = 120;
                _temperatureIncreasing = false;
            }
        }
        else
        {
            _temperatureValue -= 1;
            if (_temperatureValue <= 0)
            {
                _temperatureValue = 0;
                _temperatureIncreasing = true;
            }
        }
        UpdateTemperatureDisplay(_temperatureValue);
    }

    /// <summary>
    /// 外部调用接口：设置温度值并刷新显示（值会被 Clamp 到 0~120）
    /// </summary>
    public void SetTemperature(double value)
    {
        _temperatureValue = Math.Clamp(value, 0, 120);
        UpdateTemperatureDisplay(_temperatureValue);
    }

    private void UpdateTemperatureDisplay(double temperature)
    {
        if (_temperatureBlocks == null || TemperatureText == null) return;
        
        temperature = Math.Clamp(temperature, 0, 120);
        TemperatureText.Text = ((int)temperature).ToString();
        
        var displayTemperature = Math.Min(temperature, MaxTemperature);
        var activeBlockCount = (int)Math.Ceiling(displayTemperature / TemperaturePerBlock);
        activeBlockCount = Math.Min(activeBlockCount, TemperatureBlockCount);
        
        for (int i = 0; i < TemperatureBlockCount; i++)
        {
            if (i < activeBlockCount)
            {
                _temperatureBlocks[i].Fill = GetTemperatureBlockColor(i);
            }
            else
            {
                _temperatureBlocks[i].Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937"));
            }
        }
    }

    /// <summary>
    /// 获取指定色块的渐变颜色：块 0~1 绿色，块 2~8 绿→橙，块 9~14 橙→红
    /// 使用线性插值实现三区段颜色平滑过渡
    /// </summary>
    private SolidColorBrush GetTemperatureBlockColor(int blockIndex)
    {
        Color color;
        
        if (blockIndex <= 1)
        {
            color = Color.FromRgb(34, 197, 94);
        }
        else if (blockIndex <= 8)
        {
            var t = (blockIndex - 2) / 6.0;
            var r = (byte)(34 + (234 - 34) * t);
            var g = (byte)(197 + (179 - 197) * t);
            var b = (byte)(94 + (8 - 94) * t);
            color = Color.FromRgb(r, g, b);
        }
        else
        {
            var t = (blockIndex - 8) / 6.0;
            var r = (byte)(234 + (185 - 234) * t);
            var g = (byte)(179 + (28 - 179) * t);
            var b = (byte)(8 + (28 - 8) * t);
            color = Color.FromRgb(r, g, b);
        }
        
        return new SolidColorBrush(color);
    }

    // ═══════════════════════════════════════════════════════════════
    // 力反馈弧形仪表
    // ═══════════════════════════════════════════════════════════════
    // 圆弧形仪表盘，从 135°（左下）起始，覆盖 270° 弧长
    // 坐标计算：X = centerX + radius * cos(rad)，Y = centerY + radius * sin(rad)
    // WPF 中 Y 轴向下为正，因此 startAngle=135° 指向左下，顺时针方向覆盖
    // IsLargeArc 标志：当弧角 > 180° 时设为 true

    /// <summary>
    /// 初始化力反馈仪表：绘制背景弧线并设置初始值
    /// </summary>
    private void InitializeForceFeedbackGauge()
    {
        DrawBackgroundArc();
        UpdateForceFeedbackArc(_forceFeedbackValue);
    }

    /// <summary>
    /// 绘制背景弧线（灰色底弧），计算起始/终止点的直角坐标放置 PathSegment
    /// </summary>
    private void DrawBackgroundArc()
    {
        if (BackgroundArcSegment == null || BackgroundArcFigure == null) return;

        var startAngleRad = GaugeStartAngle * Math.PI / 180;
        var endAngleRad = (GaugeStartAngle + GaugeTotalAngle) * Math.PI / 180;

        var startX = GaugeCenterX + GaugeRadius * Math.Cos(startAngleRad);
        var startY = GaugeCenterY + GaugeRadius * Math.Sin(startAngleRad);
        var endX = GaugeCenterX + GaugeRadius * Math.Cos(endAngleRad);
        var endY = GaugeCenterY + GaugeRadius * Math.Sin(endAngleRad);

        BackgroundArcFigure.StartPoint = new Point(startX, startY);
        BackgroundArcSegment.Point = new Point(endX, endY);
        BackgroundArcSegment.Size = new Size(GaugeRadius, GaugeRadius);
        BackgroundArcSegment.IsLargeArc = GaugeTotalAngle > 180;
    }

    /// <summary>
    /// 启动力反馈模拟计时器（100ms 间隔），在 0~100 范围内随机波动
    /// </summary>
    private void StartForceFeedbackSimulation()
    {
        _random = new Random();
        _forceFeedbackAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _forceFeedbackAnimationTimer.Tick += ForceFeedbackAnimationTimer_Tick;
        _forceFeedbackAnimationTimer.Start();
    }

    private void ForceFeedbackAnimationTimer_Tick(object? sender, EventArgs e)
    {
        var variation = (_random!.NextDouble() - 0.5) * 10;
        var newValue = Math.Clamp(_forceFeedbackValue + variation, 0, 100);
        _forceFeedbackValue = newValue;
        UpdateForceFeedbackArc(newValue);
    }

    /// <summary>
    /// 外部调用接口：设置力反馈值并刷新弧形显示（值会被 Clamp 到 0~100）
    /// </summary>
    public void SetForceFeedbackValue(double value)
    {
        _forceFeedbackValue = Math.Clamp(value, 0, 100);
        UpdateForceFeedbackArc(_forceFeedbackValue);
    }

    private void UpdateForceFeedbackArc(double value)
    {
        if (DynamicArcSegment == null || DynamicArcFigure == null || ForceFeedbackText == null) return;
        
        value = Math.Clamp(value, 0, 100);
        
        var angle = (value / 100.0) * GaugeTotalAngle;
        var endAngle = GaugeStartAngle + angle;
        
        var startAngleRad = GaugeStartAngle * Math.PI / 180;
        var endAngleRad = endAngle * Math.PI / 180;
        
        var startX = GaugeCenterX + GaugeRadius * Math.Cos(startAngleRad);
        var startY = GaugeCenterY + GaugeRadius * Math.Sin(startAngleRad);
        var endX = GaugeCenterX + GaugeRadius * Math.Cos(endAngleRad);
        var endY = GaugeCenterY + GaugeRadius * Math.Sin(endAngleRad);
        
        DynamicArcFigure.StartPoint = new Point(startX, startY);
        DynamicArcSegment.Point = new Point(endX, endY);
        DynamicArcSegment.Size = new Size(GaugeRadius, GaugeRadius);
        DynamicArcSegment.IsLargeArc = angle > 180;
        
        ForceFeedbackText.Text = value.ToString("F1");
    }

    // ═══════════════════════════════════════════════════════════════
    // Banner 轮播
    // ═══════════════════════════════════════════════════════════════
    // 3 张 Slide 使用 Canvas.Left 定位并排排列，通过双动画实现滑动切换效果
    // 切换逻辑：当前页向左滑出，目标页从右侧滑入（向前）或左侧滑入（向后）
    // CubicEase EaseOut 缓动函数提供自然减速效果
    // 自动播放周期 5s，手动切换后重置计时器

    private void CarouselContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSlideWidths();
        if (!_isAnimating)
        {
            PositionSlide(_currentSlide, 0);
        }
    }

    private void CarouselContainer_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_isAnimating || _slides == null) return;
        
        e.Handled = true;
        ResetAutoPlayTimer();
        
        if (e.Delta > 0)
        {
            GoToPrevSlide();
        }
        else
        {
            GoToNextSlide();
        }
    }

    private void UpdateSlideWidths()
    {
        var width = CarouselContainer.ActualWidth;
        foreach (var slide in _slides ?? Array.Empty<Border>())
        {
            slide.Width = width;
        }
    }

    private void InitializeSlidePositions()
    {
        PositionSlide(0, 0);
        PositionSlide(1, CarouselContainer.ActualWidth);
        PositionSlide(2, CarouselContainer.ActualWidth * 2);
    }

    private void PositionSlide(int slideIndex, double position)
    {
        if (_slides == null || slideIndex < 0 || slideIndex >= _slides.Length) return;
        Canvas.SetLeft(_slides[slideIndex], position);
    }

    private void StartAutoPlay()
    {
        _autoPlayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AutoPlayInterval)
        };
        _autoPlayTimer.Tick += AutoPlayTimer_Tick;
        _autoPlayTimer.Start();
    }

    private void AutoPlayTimer_Tick(object? sender, EventArgs e)
    {
        GoToNextSlide();
    }

    private void GoToNextSlide()
    {
        if (_isAnimating || _slides == null) return;
        
        var nextSlide = (_currentSlide + 1) % SlideCount;
        AnimateTransition(nextSlide, true);
    }

    private void GoToPrevSlide()
    {
        if (_isAnimating || _slides == null) return;
        
        var prevSlide = (_currentSlide - 1 + SlideCount) % SlideCount;
        AnimateTransition(prevSlide, false);
    }

    private void GoToSlide(int targetIndex)
    {
        if (_isAnimating || _slides == null || targetIndex == _currentSlide) return;
        
        var diff = targetIndex - _currentSlide;
        var goForward = diff > 0 || (diff == -2);
        
        AnimateTransition(targetIndex, goForward);
    }

    private async void AnimateTransition(int targetIndex, bool goForward)
    {
        if (_slides == null) return;
        
        _isAnimating = true;
        var previousSlide = _currentSlide;
        _currentSlide = targetIndex;
        UpdateIndicators();
        
        var slideWidth = CarouselContainer.ActualWidth;
        var multiplier = goForward ? 1 : -1;
        
        PositionSlide(targetIndex, slideWidth * multiplier);
        
        var currentAnim = new DoubleAnimation
        {
            From = 0,
            To = -slideWidth * multiplier,
            Duration = TimeSpan.FromSeconds(AnimationDuration),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        
        var targetAnim = new DoubleAnimation
        {
            From = slideWidth * multiplier,
            To = 0,
            Duration = TimeSpan.FromSeconds(AnimationDuration),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        
        _slides[previousSlide].BeginAnimation(Canvas.LeftProperty, currentAnim);
        _slides[targetIndex].BeginAnimation(Canvas.LeftProperty, targetAnim);
        
        await Task.Delay(TimeSpan.FromSeconds(AnimationDuration));
        _isAnimating = false;
    }

    private void UpdateIndicators()
    {
        if (_indicators == null) return;
        
        for (int i = 0; i < _indicators.Length; i++)
        {
            _indicators[i].Opacity = (i == _currentSlide) ? 1.0 : 0.5;
        }
    }

    private void Indicator_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag && int.TryParse(tag, out int index))
        {
            ResetAutoPlayTimer();
            GoToSlide(index);
        }
    }

    private static readonly HttpClient _bannerImageClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    private async Task LoadBannersAsync()
    {
        try
        {
            if (_gameDataService == null) return;
            _banners = await _gameDataService.GetBannersAsync();

            if (_banners.Count > 0)
                await UpdateCarouselImagesAsync();
        }
        catch
        {
            // Silently fail — banners are cosmetic
        }
    }

    private async Task UpdateCarouselImagesAsync()
    {
        if (_banners == null || _slideImages == null) return;

        for (int i = 0; i < SlideCount && i < _banners.Count; i++)
        {
            try
            {
                var bytes = await _bannerImageClient.GetByteArrayAsync(_banners[i].ImageUrl);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = new System.IO.MemoryStream(bytes);
                bitmap.EndInit();
                bitmap.Freeze();
                _slideImages[i].Source = bitmap;
            }
            catch
            {
                // Keep default image on failure
            }
        }
    }

    private void Slide_Click(object sender, MouseButtonEventArgs e)
    {
        if (_banners == null || _banners.Count == 0) return;
        if (sender is Border border && border.Tag is string tag && int.TryParse(tag, out int index))
        {
            if (index < _banners.Count && !string.IsNullOrWhiteSpace(_banners[index].LinkUrl))
            {
                try
                {
                    var url = _banners[index].LinkUrl;
                    // 仅允许 http/https 协议，防止 file:// 或命令注入
                    if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                }
                catch
                {
                    // Silently fail if browser can't open
                }
            }
        }
    }

    private void ResetAutoPlayTimer()
    {
        _autoPlayTimer?.Stop();
        _autoPlayTimer?.Start();
    }

    // ═══════════════════════════════════════════════════════════════
    // 方向盘 — 34 段圆环刻度
    // ═══════════════════════════════════════════════════════════════
    // 在 Canvas 上以圆心为中心，34 个 Rectangle 以弧度排列成圆形刻度盘
    // 角度计算：angleRad = i * 360° / 34 转为弧度，使用 cos/sin 计算 X/Y 偏移
    // 顶部标记段（i=0 和 i=17）使用更宽的 27.52px 刻度
    // 段 0~17 为红色（198,14,14），段 18~33 为暗红色（91,35,36）
    // 每个段通过 RotateTransform 绕自身中心旋转，实现切向排列
    // 方向旋转度数由 SteeringWheelRotate.Angle 控制，模拟 ±900° 转向

    /// <summary>
    /// 初始化方向盘：动态创建 34 个 Rectangle 刻度段，以圆形分布排列在 Canvas 上
    /// </summary>
    private void InitializeSteeringWheel()
    {
        if (SteeringWheelCanvas == null) return;
        
        _steeringWheelSegments = new Rectangle[SteeringWheelSegmentCount];
        var canvasCenterX = SteeringWheelCanvas.Width / 2;
        var canvasCenterY = SteeringWheelCanvas.Height / 2;
        
        for (int i = 0; i < SteeringWheelSegmentCount; i++)
        {
            var angleDeg = i * 360.0 / SteeringWheelSegmentCount;
            var angleRad = angleDeg * Math.PI / 180;
            
            Color segmentColor;
            double segmentWidth;
            
            if (i == 0 || i == 17)
            {
                segmentWidth = 27.52;
                segmentColor = Color.FromRgb(198, 14, 14);
            }
            else if (i >= 18 && i <= 33)
            {
                segmentWidth = SteeringWheelSegmentWidth;
                segmentColor = Color.FromRgb(91, 35, 36);
            }
            else
            {
                segmentWidth = SteeringWheelSegmentWidth;
                segmentColor = Color.FromRgb(198, 14, 14);
            }
            
            var segment = new Rectangle
            {
                Width = segmentWidth,
                Height = SteeringWheelSegmentHeight,
                Fill = new SolidColorBrush(segmentColor),
                RadiusX = 2,
                RadiusY = 2,
                RenderTransform = new RotateTransform
                {
                    Angle = angleDeg,
                    CenterX = segmentWidth / 2,
                    CenterY = SteeringWheelSegmentHeight / 2
                }
            };
            
            var x = canvasCenterX + SteeringWheelRadius * Math.Cos(angleRad) - segmentWidth / 2;
            var y = canvasCenterY + SteeringWheelRadius * Math.Sin(angleRad) - SteeringWheelSegmentHeight / 2;
            
            Canvas.SetLeft(segment, x);
            Canvas.SetTop(segment, y);
            
            SteeringWheelCanvas.Children.Add(segment);
            _steeringWheelSegments[i] = segment;
        }
    }

    /// <summary>
    /// 启动方向盘模拟计时器（50ms 间隔），在 ±900° 之间来回旋转
    /// </summary>
    private void StartSteeringSimulation()
    {
        _steeringAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _steeringAnimationTimer.Tick += SteeringAnimationTimer_Tick;
        _steeringAnimationTimer.Start();
    }

    private void SteeringAnimationTimer_Tick(object? sender, EventArgs e)
    {
        if (_steeringIncreasing)
        {
            _steeringAngle += 5;
            if (_steeringAngle >= MaxSteeringAngle)
            {
                _steeringAngle = MaxSteeringAngle;
                _steeringIncreasing = false;
            }
        }
        else
        {
            _steeringAngle -= 5;
            if (_steeringAngle <= -MaxSteeringAngle)
            {
                _steeringAngle = -MaxSteeringAngle;
                _steeringIncreasing = true;
            }
        }
        UpdateSteeringWheel(_steeringAngle);
    }

    /// <summary>
    /// 外部调用接口：设置方向盘角度值（限制在 ±900° 范围内）
    /// </summary>
    public void SetSteeringAngle(double angle)
    {
        _steeringAngle = Math.Clamp(angle, -MaxSteeringAngle, MaxSteeringAngle);
        UpdateSteeringWheel(_steeringAngle);
    }

    private void UpdateSteeringWheel(double angle)
    {
        if (SteeringAngleText == null || SteeringWheelRotate == null) return;
        
        SteeringAngleText.Text = $"{(int)angle}°";
        
        SteeringWheelRotate.Angle = angle;
    }

    // ═══════════════════════════════════════════════════════════════
    // 踏板位置显示 — 离合器 / 刹车 / 油门
    // ═══════════════════════════════════════════════════════════════
    // 三个 Rectangle Fill 控件，高度按 0~100% 比例映射到 PedalBottomMargin ~ PedalHeight
    // 即 Clamp(pedalBottomMargin + (pedalHeight - pedalBottomMargin) * value/100)
    // 模拟计时器以随机漫步 + 10% 跳变概率驱动三个踏板值的动画

    /// <summary>
    /// 启动踏板模拟计时器（100ms 间隔），随机模拟三踏板数值变化
    /// </summary>
    private void StartPedalSimulation()
    {
        _pedalRandom = new Random();
        _pedalAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _pedalAnimationTimer.Tick += PedalAnimationTimer_Tick;
        _pedalAnimationTimer.Start();
    }

    private void PedalAnimationTimer_Tick(object? sender, EventArgs e)
    {
        _clutchValue = SimulatePedalValue(_clutchValue);
        _brakeValue = SimulatePedalValue(_brakeValue);
        _throttleValue = SimulatePedalValue(_throttleValue);
        
        UpdatePedalDisplay(_clutchValue, _brakeValue, _throttleValue);
    }

    private double SimulatePedalValue(double currentValue)
    {
        var change = (_pedalRandom!.NextDouble() - 0.5) * 20;
        var newValue = currentValue + change;
        
        if (newValue < 0) newValue = 0;
        if (newValue > 100) newValue = 100;
        
        if (_pedalRandom.NextDouble() < 0.1)
        {
            newValue = _pedalRandom.NextDouble() * 100;
        }
        
        return newValue;
    }

    /// <summary>
    /// 外部调用接口：同时设置离合器、刹车、油门三个踏板值（均 Clamp 到 0~100）
    /// </summary>
    public void SetPedalValues(double clutch, double brake, double throttle)
    {
        _clutchValue = Math.Clamp(clutch, 0, 100);
        _brakeValue = Math.Clamp(brake, 0, 100);
        _throttleValue = Math.Clamp(throttle, 0, 100);
        
        UpdatePedalDisplay(_clutchValue, _brakeValue, _throttleValue);
    }

    private void UpdatePedalDisplay(double clutch, double brake, double throttle)
    {
        if (ClutchFill == null || BrakeFill == null || ThrottleFill == null) return;
        
        var minHeight = PedalBottomMargin;
        var maxHeight = PedalHeight;
        
        var clutchHeight = minHeight + (maxHeight - minHeight) * (clutch / 100.0);
        var brakeHeight = minHeight + (maxHeight - minHeight) * (brake / 100.0);
        var throttleHeight = minHeight + (maxHeight - minHeight) * (throttle / 100.0);
        
        ClutchFill.Height = clutchHeight;
        BrakeFill.Height = brakeHeight;
        ThrottleFill.Height = throttleHeight;
    }

    // ═══════════════════════════════════════════════════════════════
    // 游戏列表 — 启动游戏与悬浮位移
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 游戏启动按钮：根据 LaunchMode 使用 Steam 或自定义路径启动游戏
    /// 失败时弹出全局错误对话框
    /// </summary>
    private void LaunchButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (sender is FrameworkElement element && element.DataContext is GameItem gameItem)
        {
            var mode = gameItem.LaunchMode == LaunchModeUdf.CustomPath ? LaunchMode.CustomPath : LaunchMode.Steam;
            if (GameLauncher.Launch(gameItem, mode))
            {
                _gameDataService?.SaveUserData(gameItem);
                return;
            }
        }

        ShowLaunchErrorDialog();
    }

    private void ShowLaunchErrorDialog()
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            var dialog = mainWindow.GlobalDialogControl;

            dialog.Title = LocalizationService.Instance["Dialog.LaunchFailed"];
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
            }, isPrimary: true);

            dialog.AddButton(LocalizationService.Instance["Dialog.Cancel"], (s, args) =>
            {
                dialog.Hide();
            });

            dialog.Show();
        }
    }

    /// <summary>
    /// 鼠标进入游戏卡片时，将其右侧所有兄弟元素向右偏移 8.12px，形成悬浮展开效果
    /// </summary>
    private void CardRoot_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is GameItem gameItem)
        {
            int hoveredIndex = _gameList?.IndexOf(gameItem) ?? -1;
            if (hoveredIndex >= 0)
            {
                ShiftRightSiblings(hoveredIndex, 8.12);
            }
        }
    }

    /// <summary>
    /// 鼠标离开游戏卡片时，将右侧兄弟元素复位到偏移量 0
    /// </summary>
    private void CardRoot_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is GameItem gameItem)
        {
            int hoveredIndex = _gameList?.IndexOf(gameItem) ?? -1;
            if (hoveredIndex >= 0)
            {
                ShiftRightSiblings(hoveredIndex, 0);
            }
        }
    }

    private void ShiftRightSiblings(int startIndex, double offsetX)
    {
        if (GameListItemsControl == null || _gameList == null) return;

        var duration = new Duration(TimeSpan.FromSeconds(0.3));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        for (int i = startIndex + 1; i < _gameList.Count; i++)
        {
            if (GameListItemsControl.ItemContainerGenerator.ContainerFromIndex(i) is UIElement container)
            {
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

                transform.BeginAnimation(TranslateTransform.XProperty, anim);
            }
        }
    }
}
