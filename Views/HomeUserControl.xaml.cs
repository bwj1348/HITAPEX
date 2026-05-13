using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using HITAPEX.Controls;
using HITAPEX.Models;

namespace HITAPEX.Views;

public partial class HomeUserControl : UserControl
{
    private const int SlideCount = 3;
    private const int AutoPlayInterval = 5000;
    private const double AnimationDuration = 0.5;
    
    private const double GaugeCenterX = 71.5;
    private const double GaugeCenterY = 71.5;
    private const double GaugeRadius = 60;
    private const double GaugeStartAngle = 135;
    private const double GaugeTotalAngle = 270;
    
    private const int TemperatureBlockCount = 15;
    private const double TemperaturePerBlock = 6.0;
    private const double MaxTemperature = 90.0;
    
    private int _currentSlide = 0;
    private DispatcherTimer? _autoPlayTimer;
    private Border[]? _indicators;
    private Border[]? _slides;
    private bool _isAnimating = false;
    
    private ObservableCollection<GameItem>? _gameList;
    private bool _isPinning = false;
    
    private double _forceFeedbackValue = 75;
    private DispatcherTimer? _forceFeedbackAnimationTimer;
    private Random? _random;
    
    private Path[]? _temperatureBlocks;
    private double _temperatureValue = 0;
    private DispatcherTimer? _temperatureAnimationTimer;
    private bool _temperatureIncreasing = true;
    
    private const int SteeringWheelSegmentCount = 34;
    private const double SteeringWheelRadius = 60;
    private const double SteeringWheelSegmentWidth = 16.51;
    private const double SteeringWheelSegmentHeight = 5.5;
    private const double MaxSteeringAngle = 900;
    
    private Rectangle[]? _steeringWheelSegments;
    private double _steeringAngle = 0;
    private DispatcherTimer? _steeringAnimationTimer;
    private bool _steeringIncreasing = true;
    
    private const double PedalHeight = 135;
    private const double PedalTopMargin = 4;
    private const double PedalBottomMargin = 5;
    
    private double _clutchValue = 0;
    private double _brakeValue = 0;
    private double _throttleValue = 0;
    private DispatcherTimer? _pedalAnimationTimer;
    private Random? _pedalRandom;

    public HomeUserControl()
    {
        InitializeComponent();
    }

    private void HomeUserControl_Loaded(object sender, RoutedEventArgs e)
    {
        _indicators = new[] { Indicator0, Indicator1, Indicator2 };
        _slides = new[] { Slide0, Slide1, Slide2 };
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
    }

    private void InitializeGameList()
    {
        _gameList = new ObservableCollection<GameItem>();
        for (int i = 0; i < 10; i++)
        {
            _gameList.Add(new GameItem
            {
                Name = $"Game {i + 1}",
                ImagePath = "/Assets/Rectangle 24845.png",
                IsInstalled = i % 2 == 0,
                IsPinned = false
            });
        }
        GameListItemsControl.ItemsSource = _gameList;
        
        Dispatcher.BeginInvoke(() => UpdateScrollbarThumb(), DispatcherPriority.Loaded);
    }

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
                }
                
                gameItem.IsPinned = true;
                
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

    private void UpdateScrollbarThumb()
    {
        if (GameScrollViewer == null || ScrollbarThumb == null || ScrollbarTrack == null) return;
        
        var viewportWidth = GameScrollViewer.ViewportWidth;
        var extentWidth = GameScrollViewer.ExtentWidth;
        var horizontalOffset = GameScrollViewer.HorizontalOffset;
        
        if (extentWidth <= viewportWidth)
        {
            ScrollbarThumb.Width = ScrollbarTrack.ActualWidth;
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

    private void InitializeTemperatureGauge()
    {
        _temperatureBlocks = new[] { TempBlock0, TempBlock1, TempBlock2, TempBlock3, TempBlock4, 
                                      TempBlock5, TempBlock6, TempBlock7, TempBlock8, TempBlock9,
                                      TempBlock10, TempBlock11, TempBlock12, TempBlock13, TempBlock14 };
        UpdateTemperatureDisplay(_temperatureValue);
    }

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

    private void InitializeForceFeedbackGauge()
    {
        DrawBackgroundArc();
        UpdateForceFeedbackArc(_forceFeedbackValue);
    }

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

    private void ResetAutoPlayTimer()
    {
        _autoPlayTimer?.Stop();
        _autoPlayTimer?.Start();
    }

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

    private void LaunchButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            var dialog = mainWindow.GlobalDialogControl;
            
            dialog.Title = "启动失败";
            dialog.ShowIcon = true;
            dialog.ClearButtons();
            
            dialog.DialogContent = new TextBlock
            {
                Text = "游戏启动失败，请确认游戏已安装且启动路径正确，或尝试重新设置路径后再试。",
                FontSize = 22,
                Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Regular
                
            };
            
            dialog.AddButton("重 新 启 动", (s, args) =>
            {
                dialog.Hide();
            }, isPrimary: true);
            
            dialog.AddButton("取 消", (s, args) =>
            {
                dialog.Hide();
            });
            
            dialog.Show();
        }
    }
}
