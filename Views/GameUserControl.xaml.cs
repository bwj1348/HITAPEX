using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using HITAPEX.Controls;
using HITAPEX.Models;
using Microsoft.Win32;
using System.IO;

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
    private GameFilterType _currentFilter = GameFilterType.All;
    private bool _isPinning = false;
    private bool _isLoading = false;
    private GameItem? _selectedGame;
    
    private DispatcherTimer? _telemetryAnimationTimer;
    private Random? _random;
    private int _packetCount = 0;
    
    private bool _isDraggingThumb = false;
    private Point _lastMousePosition;

    public GameUserControl()
    {
        InitializeComponent();
    }

    private void GameUserControl_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeGameList();
        StartTelemetrySimulation();
        UpdateScrollbarThumb();
    }

    private void InitializeGameList()
    {
        _allGameList = new ObservableCollection<GameItem>();
        _filteredGameList = new ObservableCollection<GameItem>();
        
        var games = new[]
        {
            new GameItem
            {
                Name = "FORZA 5",
                ImagePath = "/Assets/Rectangle 24845.png",
                IsInstalled = true,
                LaunchPath = @"C:\Games\Forza5\Forza5.exe",
                Description = "《极限竞速：地平线5》（Forza Horizon 5）是一款由Playground Games开发、以墨西哥为背景的开放世界赛车游戏。该游戏全面支持简体/繁体中文界面以及中文普通话语音，玩家可在Steam、Microsoft Store、Xbox及PS5平台上享受完全本地化的中文竞速体验。",
                Version = "v1.0",
                LastPlayed = "2024-01-20",
                IsPinned = false
            },
            new GameItem
            {
                Name = "iRacing",
                ImagePath = "/Assets/Rectangle 24845.png",
                IsInstalled = true,
                LaunchPath = @"C:\Games\iRacing\iRacing.exe",
                Description = "iRacing是一款专业级在线赛车模拟平台，提供最真实的赛车物理和竞技体验。支持多种赛车类型和全球知名赛道。",
                Version = "2024.1",
                LastPlayed = "2024-01-10",
                IsPinned = false
            },
            new GameItem
            {
                Name = "rFactor 2",
                ImagePath = "/Assets/Rectangle 24845.png",
                IsInstalled = false,
                LaunchPath = "",
                Description = "rFactor 2是一款高度可定制的赛车模拟器，支持丰富的Mod内容和真实的物理引擎。",
                Version = "未安装",
                LastPlayed = "-",
                IsPinned = false
            },
            new GameItem
            {
                Name = "Project CARS 2",
                ImagePath = "/Assets/Rectangle 24845.png",
                IsInstalled = true,
                LaunchPath = @"C:\Games\PCARS2\PCARS2.exe",
                Description = "Project CARS 2提供超过180种车辆和60个赛道，支持多种赛车类型和动态天气系统。",
                Version = "v7.0",
                LastPlayed = "2024-01-08",
                IsPinned = false
            },
            new GameItem
            {
                Name = "F1 2023",
                ImagePath = "/Assets/Rectangle 24845.png",
                IsInstalled = true,
                LaunchPath = @"C:\Games\F123\F1_23.exe",
                Description = "F1 2023是官方授权的一级方程式赛车游戏，包含所有官方车队、车手和赛道。",
                Version = "v1.15",
                LastPlayed = "2024-01-12",
                IsPinned = false
            },
            new GameItem
            {
                Name = "Automobilista 2",
                ImagePath = "/Assets/Rectangle 24845.png",
                IsInstalled = false,
                LaunchPath = "",
                Description = "Automobilista 2是巴西赛车模拟游戏，提供丰富的巴西赛道和车辆内容。",
                Version = "未安装",
                LastPlayed = "-",
                IsPinned = false
            },
            new GameItem
            {
                Name = "RaceRoom",
                ImagePath = "/Assets/Rectangle 24845.png",
                IsInstalled = true,
                LaunchPath = @"C:\Games\RaceRoom\RaceRoom.exe",
                Description = "RaceRoom是一款免费赛车模拟游戏，提供多种赛车和赛道内容。",
                Version = "v1.20",
                LastPlayed = "2024-01-05",
                IsPinned = false
            },
            new GameItem
            {
                Name = "Gran Turismo 7",
                ImagePath = "/Assets/Rectangle 24845.png",
                IsInstalled = false,
                LaunchPath = "",
                Description = "Gran Turismo 7是PlayStation平台的旗舰赛车游戏，提供超过400种车辆。",
                Version = "未安装",
                LastPlayed = "-",
                IsPinned = false
            },
            new GameItem
            {
                Name = "Forza Motorsport",
                ImagePath = "/Assets/Rectangle 24845.png",
                IsInstalled = true,
                LaunchPath = @"C:\Games\Forza\ForzaMotorsport.exe",
                Description = "Forza Motorsport是Xbox Game Studios开发的赛车模拟游戏，提供超过500种车辆。",
                Version = "v1.0",
                LastPlayed = "2024-01-14",
                IsPinned = false
            },
            new GameItem
            {
                Name = "Richard Burns Rally",
                ImagePath = "/Assets/Rectangle 24845.png",
                IsInstalled = true,
                LaunchPath = @"C:\Games\RBR\RBR.exe",
                Description = "Richard Burns Rally是一款经典的拉力赛模拟游戏，以真实的物理引擎著称。",
                Version = "v1.02",
                LastPlayed = "2024-01-11",
                IsPinned = false
            }
        };

        foreach (var game in games)
        {
            _allGameList.Add(game);
        }

        ApplyFilter(_currentFilter);
        SelectGame(_allGameList[0]);
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

        var sortedFiltered = filtered.OrderByDescending(g => g.IsPinned).ThenBy(g => _allGameList.IndexOf(g));

        foreach (var game in sortedFiltered)
        {
            _filteredGameList.Add(game);
        }

        GameListItemsControl.ItemsSource = _filteredGameList;
        _currentFilter = filter;

        Dispatcher.BeginInvoke(() => UpdateScrollbarThumb(), DispatcherPriority.Loaded);
    }

    private void SelectGame(GameItem game)
    {
        _selectedGame = game;
        GameTitleText.Text = game.Name;
        GameTitleText2.Text = game.Name;
        GameDescriptionText.Text = game.Description;

        if (!string.IsNullOrEmpty(game.LaunchPath))
        {
            CustomPathText.Text = game.LaunchPath;
        }
        else
        {
            CustomPathText.Text = "点击选择游戏路径...";
        }

        if (game.IsInstalled)
        {
            LaunchButtonPath.Visibility = Visibility.Visible;
            LaunchButtonPathNotInstalled.Visibility = Visibility.Collapsed;
            LaunchButtonText.Text = "启 动 游 戏";
        }
        else
        {
            LaunchButtonPath.Visibility = Visibility.Collapsed;
            LaunchButtonPathNotInstalled.Visibility = Visibility.Visible;
            LaunchButtonText.Text = "未 安 装";
        }
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
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _isLoading = true;
        LoadingOverlay.Visibility = Visibility.Visible;

        await Task.Delay(1500);

        InitializeGameList();

        _isLoading = false;
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private void LaunchGameButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        
        if (_selectedGame == null) return;

        if (_selectedGame.IsInstalled && !string.IsNullOrEmpty(_selectedGame.LaunchPath))
        {
            if (!File.Exists(_selectedGame.LaunchPath))
            {
                ShowLaunchErrorDialog();
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _selectedGame.LaunchPath,
                    UseShellExecute = true
                });
            }
            catch
            {
                ShowLaunchErrorDialog();
            }
        }
        else
        {
            ShowLaunchErrorDialog();
        }
    }

    private void LaunchModeRadio_Changed(object sender, RoutedEventArgs e)
    {
        if (CustomPathPanel == null) return;
        
        if (CustomLaunchRadio.IsChecked == true)
        {
            CustomPathPanel.Visibility = Visibility.Visible;
            if (_selectedGame != null && !string.IsNullOrEmpty(_selectedGame.LaunchPath))
            {
                CustomPathText.Text = _selectedGame.LaunchPath;
            }
            else
            {
                CustomPathText.Text = "点击选择游戏路径...";
            }
        }
        else
        {
            CustomPathPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void BrowseCustomPath_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        
        var dialog = new OpenFileDialog
        {
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            Title = "选择游戏启动文件"
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

            dialog.Title = "提示";
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
                LaunchGameButton_Click(null, null);
            }, isPrimary: true);

            dialog.AddButton("取 消", (s, args) =>
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

            if (gameItem.IsInstalled && !string.IsNullOrEmpty(gameItem.LaunchPath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = gameItem.LaunchPath,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    ShowLaunchErrorDialog();
                }
            }
            else
            {
                ShowLaunchErrorDialog();
            }
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
        _random = new Random();
        _telemetryAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _telemetryAnimationTimer.Tick += TelemetryAnimationTimer_Tick;
        _telemetryAnimationTimer.Start();
    }

    private void TelemetryAnimationTimer_Tick(object? sender, EventArgs e)
    {
        _packetCount += _random!.Next(1, 5);
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
}
