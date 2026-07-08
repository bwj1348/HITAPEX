using System.Diagnostics;
using System.IO;
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
using HITAPEX.Controls;
using HITAPEX.Models;
using HITAPEX.Services;
using HITAPEX.Services.Data;
using HITAPEX.Services.Data.Api;
using Microsoft.Win32;

namespace HITAPEX.Views;

public enum GameFilterType
{
    All,
    Installed,
    NotInstalled
}

public partial class GameUserControl : UserControl
{
    private ObservableCollection<GameItem>? _allGameList;
    private ObservableCollection<GameItem>? _filteredGameList;
    private bool _isInitialized;
    private GameFilterType _currentFilter = GameFilterType.All;
    private bool _isPinning = false;
    private bool _isLoading = false;
    private GameItem? _selectedGame;
    private GameDataService? _gameDataService;
    private CancellationTokenSource? _loadGamesCts;
    
    private DispatcherTimer? _telemetryAnimationTimer;
    private int _packetCount = 0;
    
    private bool _isDraggingThumb = false;
    private Point _lastMousePosition;

    public GameUserControl()
    {
        InitializeComponent();
    }

    private void GameUserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        // 订阅语言切换事件，以便动态更新游戏介绍文本
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;

        InitializeGameList();
        StartTelemetrySimulation();
        UpdateScrollbarThumb();
    }

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

    private async void OnGameDataStateChanged(GameDataState state)
    {
        if (state == GameDataState.Loaded)
            await Dispatcher.InvokeAsync(async () => await LoadGamesAsync());
    }

    private void ShowLoadingState()
    {
        if (LoadingOverlay != null)
            LoadingOverlay.Visibility = Visibility.Visible;
    }

    private void HideLoadingState()
    {
        if (LoadingOverlay != null)
            LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private void ApplyFilter(GameFilterType filter)
    {
        if (_allGameList == null || _filteredGameList == null) return;

        _filteredGameList.Clear();

        var filtered = filter switch
        {
            GameFilterType.Installed => _allGameList.Where(g => g.IsInstalled),
            GameFilterType.NotInstalled => _allGameList.Where(g => !g.IsInstalled),
            _ => _allGameList
        };

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

        GameScrollViewer.ScrollToHorizontalOffset(0);
        Dispatcher.BeginInvoke(() => UpdateScrollbarThumb(), DispatcherPriority.Loaded);
    }

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
            CustomPathText.Text = !string.IsNullOrEmpty(game.LaunchPath)
                ? game.LaunchPath
                : LocalizationService.Instance["Game.SelectGamePath"];
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

    private void ShowTelemetryConfigSuccessToast()
    {
        var rootPanel = (Window.GetWindow(this)?.Content as Panel);
        if (rootPanel == null) return;

        var toast = new Grid
        {
            Width = 360,
            Height = 100,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Panel.SetZIndex(toast, 2000);

        toast.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M360 0H9L0 9V100H351L360 91V0Z"),
            Fill = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)),
            Stretch = Stretch.Fill
        });

        toast.Children.Add(new SharpVectors.Converters.SvgViewbox
        {
            Source = new Uri("/Assets/Group126548867.svg", UriKind.Relative),
            Stretch = Stretch.Fill
        });

        toast.Children.Add(new System.Windows.Shapes.Path
        {
            Width = 340,
            Height = 80,
            Data = Geometry.Parse("M339.5 0.5V73.793L333.793 79.5H0.5V6.20703L6.20703 0.5H339.5Z"),
            Stroke = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
            StrokeThickness = 1,
            Stretch = Stretch.Fill
        });

        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 绿色勾号图标
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

    private void ShowTelemetryConfigFailToast()
    {
        var rootPanel = (Window.GetWindow(this)?.Content as Panel);
        if (rootPanel == null) return;

        var toast = new Grid
        {
            Width = 360,
            Height = 100,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Panel.SetZIndex(toast, 2000);

        toast.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M360 0H9L0 9V100H351L360 91V0Z"),
            Fill = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)),
            Stretch = Stretch.Fill
        });

        toast.Children.Add(new SharpVectors.Converters.SvgViewbox
        {
            Source = new Uri("/Assets/Group126548867.svg", UriKind.Relative),
            Stretch = Stretch.Fill
        });

        toast.Children.Add(new System.Windows.Shapes.Path
        {
            Width = 340,
            Height = 80,
            Data = Geometry.Parse("M339.5 0.5V73.793L333.793 79.5H0.5V6.20703L6.20703 0.5H339.5Z"),
            Stroke = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
            StrokeThickness = 1,
            Stretch = Stretch.Fill
        });

        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 红色警告图标
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

    private void LaunchGameButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (_selectedGame == null) return;

        var mode = _selectedGame?.LaunchMode == LaunchModeUdf.CustomPath ? LaunchMode.CustomPath : LaunchMode.Steam;
        if (GameLauncher.Launch(_selectedGame, mode))
        {
            _gameDataService?.SaveUserData(_selectedGame);
            return;
        }

        ShowLaunchErrorDialog();
    }

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
                CustomPathText.Text = LocalizationService.Instance["Game.SelectGamePath"];
            }
        }
        else
        {
            _selectedGame.LaunchMode = LaunchModeUdf.Steam;
            CustomPathPanel.Visibility = Visibility.Collapsed;
        }

        _gameDataService?.SaveUserData(_selectedGame);
    }

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

    private void AutoApplyPreset_Changed(object sender, RoutedEventArgs e)
    {
        if (AutoApplyPresetCheckBox == null) return;
        
        var isAutoApply = AutoApplyPresetCheckBox.IsChecked == true;
    }

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

    private void AnimateMoveToPosition(int fromIndex, int toIndex)
    {
        if (_filteredGameList == null || fromIndex == toIndex)
        {
            _isPinning = false;
            return;
        }

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

        var movingItem = _filteredGameList[fromIndex];
        _filteredGameList.Move(fromIndex, toIndex);
        GameListItemsControl.UpdateLayout();

        var storyboard = new Storyboard();
        var duration = new Duration(TimeSpan.FromMilliseconds(450));
        var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

        for (int i = minIndex; i <= maxIndex; i++)
        {
            var container = GameListItemsControl.ItemContainerGenerator.ContainerFromItem(_filteredGameList[i]) as FrameworkElement;

            if (container != null && positionMap.TryGetValue(getIndexInMap(i, fromIndex, toIndex), out double oldX))
            {
                var newPos = container.TransformToAncestor(GameListItemsControl).Transform(new Point(0, 0));
                double deltaX = oldX - newPos.X;

                if (container.RenderTransform is not TranslateTransform)
                {
                    container.RenderTransform = new TranslateTransform();
                }

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

    private void LaunchButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (sender is FrameworkElement element && element.DataContext is GameItem gameItem)
        {
            SelectGame(gameItem);

            var mode = _selectedGame?.LaunchMode == LaunchModeUdf.CustomPath ? LaunchMode.CustomPath : LaunchMode.Steam;
            if (GameLauncher.Launch(gameItem, mode))
            {
                _gameDataService?.SaveUserData(gameItem);
                return;
            }

            ShowLaunchErrorDialog();
        }
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

    private void StartTelemetrySimulation()
    {
        var telemetryService = App.TelemetryService;
        if (telemetryService == null) return;

        // 监听遥测事件
        telemetryService.OnStarted += gameId =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                Debug.WriteLine($"[GameUI] 遥测已启动, GameId={gameId}");
            });
        };

        telemetryService.OnStopped += () =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _packetCount = 0;
                Debug.WriteLine("[GameUI] 遥测已停止");
            });
        };

        telemetryService.OnPacketsDispatched += _ =>
        {
            // 在后台线程中递增发包计数，需要调度到 UI 线程更新显示
            Dispatcher.BeginInvoke(() =>
            {
                _packetCount++;
            });
        };

        // 启动遥测状态刷新定时器
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

    private void TelemetryAnimationTimer_Tick(object? sender, EventArgs e)
    {
        // 此方法不再使用，遥测数据由 TelemetryService 后台线程驱动
    }

    private void CardRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is GameItem gameItem)
        {
            SelectGame(gameItem);
        }
    }

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

    private void ShiftRightSiblings(int startIndex, double offsetX)
    {
        if (GameListItemsControl == null || _filteredGameList == null) return;

        var duration = new Duration(TimeSpan.FromSeconds(0.3));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        // 从当前卡片的下一个索引开始遍历
        for (int i = startIndex + 1; i < _filteredGameList.Count; i++)
        {
            // 获取生成的实际 UI 容器
            if (GameListItemsControl.ItemContainerGenerator.ContainerFromIndex(i) is UIElement container)
            {
                // 确保容器拥有 TranslateTransform
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

                // 执行动画（基于 GPU，零重排）
                transform.BeginAnimation(TranslateTransform.XProperty, anim);
            }
        }
    }

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
}
