using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace HITAPEX.Views.DeviceParameters;

public partial class PresetListPopup : UserControl
{
    private readonly List<PresetItem> _officialPresets = new();
    private readonly List<PresetItem> _personalPresets = new();
    private bool _isOfficialTab = true;
    private string _currentCategory = "全部";
    // Shared detail popup — one instance for all preset items
    private Popup? _detailPopup;
    private TextBlock? _detailNameText;
    private WrapPanel? _detailGamesPanel;
    private StackPanel? _detailContentStack;
    private Grid? _detailRootGrid;
    private List<Path>? _detailPolygonPaths;
    private List<Path>? _detailBorderSegments;

    public event EventHandler<PresetItem>? PresetApplied;
    public event EventHandler? PresetImported;

    //用于控制弹窗延迟任务的取消标志
    private CancellationTokenSource? _popupDelayCts;

    public PresetListPopup()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitCategoryComboBox();
        LoadSamplePresets();
        InitSharedDetailPopup();
        RenderPresetList();
        PresetScrollViewer.ScrollChanged += PresetScrollViewer_ScrollChanged;
    }

    private void OpenEditPopup(PresetItem preset)
    {
        var editPopup = new EditPresetPopup();
        editPopup.Tag = preset.Name;
        editPopup.EditConfirmed += (_, edited) =>
        {
            var originalName = editPopup.Tag?.ToString();
            if (!string.IsNullOrEmpty(originalName))
            {
                var idx = _personalPresets.FindIndex(p =>
                    string.Equals(p.Name, originalName, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    _personalPresets[idx] = edited;
                    if (!_isOfficialTab) RenderPresetList();
                }
            }
            RemoveEditPopup(editPopup);
        };
        editPopup.EditCancelled += (_, _) => RemoveEditPopup(editPopup);

        if (Content is Panel rootPanel)
            rootPanel.Children.Add(editPopup);

        editPopup.BeginEdit(preset, _personalPresets.Select(p => p.Name));
        editPopup.Show();
    }

    private void RemoveEditPopup(EditPresetPopup popup)
    {
        if (Content is Panel rootPanel)
            rootPanel.Children.Remove(popup);
    }

    private void PresetScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (PresetScrollViewer.ScrollableHeight <= 0)
        {
            PresetScrollBar.Visibility = Visibility.Collapsed;
            return;
        }
        PresetScrollBar.Visibility = Visibility.Visible;
        PresetScrollBar.Maximum = PresetScrollViewer.ScrollableHeight;
        PresetScrollBar.ViewportSize = PresetScrollViewer.ViewportHeight;
        PresetScrollBar.Value = PresetScrollViewer.VerticalOffset;
    }

    private void LoadSamplePresets()
    {
        _officialPresets.AddRange(new[]
        {
            new PresetItem { Name = "GT3 Sprint V1.2", Description = "Sprint race setup", Category = "Assetto Corsa Competizione (ACC)", ItemCount = 7, Games = new List<string> { "ACC", "GTWC", "IGTC", "GT4" } },
            new PresetItem { Name = "GT3 Endurance Pro", Description = "Endurance race setup", Category = "Assetto Corsa Competizione (ACC)", ItemCount = 9, Games = new List<string> { "ACC", "GTWC", "IGTC", "F1", "LMU" } },
            new PresetItem { Name = "GT4 Monza Quali", Category = "Assetto Corsa Competizione (ACC)", ItemCount = 6, Games = new List<string> { "ACC", "GT4", "iR" } },
            new PresetItem { Name = "Drift King Tune", Description = "Drift competition tune", Category = "Forza Horizon 5 (FH5)", ItemCount = 5, Games = new List<string> { "FH5", "FM", "AC EVO" } },
            new PresetItem { Name = "Dakar Stage 3 Rally", Description = "Rally stage setup", Category = "Dirt Rally 2.0 (DR2.0)", ItemCount = 4, Games = new List<string> { "DR2.0", "EA WRC", "FH5" } },
            new PresetItem { Name = "AC EVO Time Attack", Description = "Time attack setup", Category = "Assetto Corsa Evo (AC EVO)", ItemCount = 6, Games = new List<string> { "AC EVO", "ACC", "FM", "F1" } },
            new PresetItem { Name = "Nurburgring Setup", Description = "Nordschleife setup", Category = "Assetto Corsa Competizione (ACC)", ItemCount = 8, Games = new List<string> { "ACC", "GTWC", "IGTC", "GT4", "LMU" } },
            new PresetItem { Name = "Spa 24H Wet", Category = "Assetto Corsa Competizione (ACC)", ItemCount = 11, Games = new List<string> { "ACC", "GTWC", "IGTC", "iR" } },
            new PresetItem { Name = "Forza 5 Drag Strip", Category = "Forza Horizon 5 (FH5)", ItemCount = 3, Games = new List<string> { "FH5", "FM", "AC EVO" } },
            new PresetItem { Name = "Dakar Prologue Tune", Category = "Dirt Rally 2.0 (DR2.0)", ItemCount = 5, Games = new List<string> { "DR2.0", "EA WRC" } },
            new PresetItem { Name = "AC EVO Nordschleife", Category = "Assetto Corsa Evo (AC EVO)", ItemCount = 7, Games = new List<string> { "AC EVO", "FH5", "FM", "LMU", "F1" } },
            new PresetItem { Name = "Bathurst 12H Setup", Category = "Assetto Corsa Competizione (ACC)", ItemCount = 8, Games = new List<string> { "ACC", "IGTC", "GTWC", "GT4" } },
            new PresetItem { Name = "Forza 5 Circuit GP", Category = "Forza Motorsport (FM)", ItemCount = 6, Games = new List<string> { "FM", "FH5", "ACC", "iR" } },
            new PresetItem { Name = "Dakar Marathon E2", Category = "Dirt Rally 2.0 (DR2.0)", ItemCount = 4, Games = new List<string> { "DR2.0", "EA WRC", "FH5" } },
        });

        _personalPresets.AddRange(new[]
        {
            new PresetItem { Name = "My GT3 Setup", Category = "Assetto Corsa Competizione (ACC)", ItemCount = 3, Games = new List<string> { "ACC", "GTWC" } },
            new PresetItem { Name = "Rally Custom", Category = "Dirt Rally 2.0 (DR2.0)", ItemCount = 2, Games = new List<string> { "DR2.0", "EA WRC", "FH5" } },
            new PresetItem { Name = "Forza Drift V2", Category = "Forza Horizon 5 (FH5)", ItemCount = 4, Games = new List<string> { "FH5", "FM" } },
            new PresetItem { Name = "Endurance Pro", Category = "Assetto Corsa Competizione (ACC)", ItemCount = 5, Games = new List<string> { "ACC", "GTWC", "IGTC", "LMU", "F1", "LMU" } },
            new PresetItem { Name = "Nurb Custom", Category = "Assetto Corsa Competizione (ACC)", ItemCount = 2, Games = new List<string> { "ACC" } },
        });
    }

    public void Show()
    {
        Visibility = Visibility.Visible;
        AnimateIn();
    }

    public void Hide()
    {
        AnimateOut(() => Visibility = Visibility.Collapsed);
    }

    // ══════════════════════════════════════════
    //  动画
    // ══════════════════════════════════════════

    private void AnimateIn()
    {
        OverlayBackground.Opacity = 0;
        PopupPanel.Opacity = 0;
        PopupPanel.RenderTransform = new TranslateTransform(PopupPanel.Width, 0);
        PopupPanel.IsHitTestVisible = false;

        DoubleAnimation slideIn = new(PopupPanel.Width, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        DoubleAnimation panelFade = new(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        DoubleAnimation overlayFade = new(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        panelFade.Completed += (_, _) => { PopupPanel.IsHitTestVisible = true; };

        PopupPanel.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
        PopupPanel.BeginAnimation(OpacityProperty, panelFade);
        OverlayBackground.BeginAnimation(OpacityProperty, overlayFade);
    }

    private void AnimateOut(Action onCompleted)
    {
        if (PopupPanel.RenderTransform is not TranslateTransform translate)
            PopupPanel.RenderTransform = translate = new TranslateTransform(0, 0);

        PopupPanel.IsHitTestVisible = false;

        DoubleAnimation slideOut = new(translate.X, PopupPanel.Width, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        DoubleAnimation panelFade = new(1, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        DoubleAnimation overlayFade = new(1, 0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        panelFade.Completed += (_, _) => { onCompleted(); };

        translate.BeginAnimation(TranslateTransform.XProperty, slideOut);
        PopupPanel.BeginAnimation(OpacityProperty, panelFade);
        OverlayBackground.BeginAnimation(OpacityProperty, overlayFade);
    }

    // ══════════════════════════════════════════
    //  下拉框初始化
    // ══════════════════════════════════════════

    private void InitCategoryComboBox()
    {
        CategoryComboBox.Items.Clear();
        CategoryComboBox.Items.Add("Assetto Corsa Competizione (ACC)");
        CategoryComboBox.Items.Add("Assetto Corsa Evo (AC EVO)");
        CategoryComboBox.Items.Add("Forza Horizon 5 (FH5)");
        CategoryComboBox.Items.Add("Forza Motorsport (FM)");
        CategoryComboBox.Items.Add("Dirt Rally 2.0 (DR2.0)");
        CategoryComboBox.Items.Add("EA Sports WRC (EA WRC)");
        CategoryComboBox.Items.Add("GT World Challenge (GTWC)");
        CategoryComboBox.Items.Add("Intercontinental GT Challenge (IGTC)");
        CategoryComboBox.Items.Add("GT4 European Series (GT4)");
        CategoryComboBox.Items.Add("iRacing (iR)");
        CategoryComboBox.Items.Add("F1 24 (F1)");
        CategoryComboBox.Items.Add("Le Mans Ultimate (LMU)");
        CategoryComboBox.SelectedIndex = -1;
    }

    // ══════════════════════════════════════════
    //  选项卡切换
    // ══════════════════════════════════════════

    private void TabOfficial_Click(object sender, RoutedEventArgs e)
    {
        _isOfficialTab = true;
        UpdateTabVisuals();
        UpdateBottomButtons();
        RenderPresetList();
    }

    private void TabPersonal_Click(object sender, RoutedEventArgs e)
    {
        _isOfficialTab = false;
        UpdateTabVisuals();
        UpdateBottomButtons();
        RenderPresetList();
    }

    private void UpdateTabVisuals()
    {
        TabOfficialUnderline.Visibility = _isOfficialTab ? Visibility.Visible : Visibility.Collapsed;
        TabPersonalUnderline.Visibility = _isOfficialTab ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateBottomButtons()
    {
        ImportButton.Visibility = _isOfficialTab ? Visibility.Collapsed : Visibility.Visible;
    }

    // ══════════════════════════════════════════
    //  预设列表渲染
    // ══════════════════════════════════════════

    private void RenderPresetList()
    {
        PresetItemsControl.Items.Clear();

        var source = _isOfficialTab ? _officialPresets : _personalPresets;
        var filtered = _currentCategory == "全部"
            ? source
            : source.Where(p => p.Games.Contains(_currentCategory)).ToList();

        foreach (var preset in filtered)
        {
            PresetItemsControl.Items.Add(_isOfficialTab
                ? CreatePresetItem(preset)
                : CreatePersonalPresetItem(preset));
        }

        Dispatcher.BeginInvoke(new Action(SyncScrollBar), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void SyncScrollBar()
    {
        if (PresetScrollViewer.ScrollableHeight <= 0)
        {
            PresetScrollBar.Visibility = Visibility.Collapsed;
            return;
        }

        PresetScrollBar.Visibility = Visibility.Visible;
        PresetScrollBar.Maximum = PresetScrollViewer.ScrollableHeight;
        PresetScrollBar.ViewportSize = PresetScrollViewer.ViewportHeight;
        PresetScrollBar.Value = PresetScrollViewer.VerticalOffset;
    }

    private void PresetScrollBar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
    {
        PresetScrollViewer.ScrollToVerticalOffset(e.NewValue);
    }

    private FrameworkElement CreatePresetItem(PresetItem preset)
    {
        var item = new ContentControl
        {
            Style = (Style)FindResource("OfficialPresetItemStyle")
        };

        var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

        var iconGrid = new Grid
        {
            Width = 8,
            Height = 26,
            Effect = (Effect)FindResource("TitleIconShadow"),
            Opacity = 0.9,
            Margin = new Thickness(20, 0, 0, 0)
        };

        var path1 = new Path
        {
            Data = Geometry.Parse("M0 2L2 0H4L0 4V2Z"),
            Fill = (Brush)FindResource("AccentGradient")
        };
        var path2 = new Path
        {
            Data = Geometry.Parse("M8 0H6L0 6V26H5L8 23V0Z"),
            Fill = (Brush)FindResource("AccentGradient")
        };

        iconGrid.Children.Add(path1);
        iconGrid.Children.Add(path2);

        stackPanel.Children.Add(iconGrid);

        var nameText = new TextBlock
        {
            Text = preset.Name,
            Foreground = new SolidColorBrush(Color.FromArgb(0xE6, 0xEE, 0xEE, 0xEE)),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            FontFamily = (FontFamily)this.FindResource("OrbitronFont"),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        stackPanel.Children.Add(nameText);

        item.Content = stackPanel;

        if (preset.Games.Count > 0)
        {
            item.MouseEnter += (_, _) => ShowDetailPopup(preset, item);
            item.MouseLeave += (_, _) => HideDetailPopup();
        }

        return item;
    }

    private FrameworkElement CreatePersonalPresetItem(PresetItem preset)
    {
        var item = new ContentControl
        {
            Style = (Style)FindResource("PersonalPresetItemStyle")
        };

        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition());
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var accentBrush = (Brush)FindResource("AccentGradient");
        var iconEffect = (Effect)FindResource("TitleIconShadow");
        var font = (FontFamily)FindResource("OrbitronFont");

        // Flag icon (same as official preset)
        var iconGrid = new Grid
        {
            Width = 8,
            Height = 26,
            Effect = iconEffect,
            Opacity = 0.9,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 0, 0)
        };
        var ip = new Path { Data = Geometry.Parse("M0 2L2 0H4L0 4V2Z M8 0H6L0 6V26H5L8 23V0Z"), Fill = accentBrush };
        iconGrid.Children.Add(ip);
        Grid.SetColumn(iconGrid, 0);
        mainGrid.Children.Add(iconGrid);

        // Preset name
        var nameText = new TextBlock
        {
            Text = preset.Name,
            MaxWidth = 102,
            Foreground = new SolidColorBrush(Color.FromArgb(0xE6, 0xEE, 0xEE, 0xEE)),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            FontFamily = font,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(20, 0, 10, 0)
        };
        Grid.SetColumn(nameText, 1);
        mainGrid.Children.Add(nameText);

        // Game tags — wrap naturally, trim overflow synchronously in Loaded
        var tagsPanel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,0,-10) };
        foreach (var game in preset.Games)
            tagsPanel.Children.Add(CreateGameTag(game));

        tagsPanel.Loaded += (_, _) => TrimTagOverflow(tagsPanel);

        Grid.SetColumn(tagsPanel, 2);
        mainGrid.Children.Add(tagsPanel);

        // Right section: divider (stretched) + icon buttons
        var rightSection = new Grid();
        rightSection.HorizontalAlignment = HorizontalAlignment.Right;
        rightSection.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rightSection.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dividerColor = new SolidColorBrush(Color.FromArgb(0x4D, 0xEE, 0xEE, 0xEE));
        var divider = new Rectangle
        {
            Width = 1,
            Fill = dividerColor,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(divider, 0);
        rightSection.Children.Add(divider);

        var iconPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 0)
        };
        iconPanel.Children.Add(CreateIconButton(s_deleteIconGeometry, () =>
        {
            _personalPresets.Remove(preset);
            RenderPresetList();
        }));
        iconPanel.Children.Add(CreateIconButton(s_copyIconGeometry, () =>
        {
            var copy = new PresetItem
            {
                Name = preset.Name + "_copy",
                Description = preset.Description,
                Category = preset.Category,
                ItemCount = preset.ItemCount,
                Games = [..preset.Games]
            };
            _personalPresets.Add(copy);
            RenderPresetList();
        }));
        iconPanel.Children.Add(CreateIconButton(s_editIconGeometry, () => OpenEditPopup(preset)));
        Grid.SetColumn(iconPanel, 1);
        rightSection.Children.Add(iconPanel);

        Grid.SetColumn(rightSection, 3);
        mainGrid.Children.Add(rightSection);

        item.Content = mainGrid;

        if (preset.Games.Count > 0)
        {
            item.MouseEnter += (_, _) => ShowDetailPopup(preset, item);
            item.MouseLeave += (_, _) => HideDetailPopup();
        }

        return item;
    }

    private void TrimTagOverflow(WrapPanel tagsPanel)
    {
        const double tagRowH = 36;
        const double tagMarginR = 5;

        var children = tagsPanel.Children.Cast<FrameworkElement>().ToList();
        if (children.Count == 0) return;

        int overflowIdx = children.Count;
        for (int i = 0; i < children.Count; i++)
        {
            var pos = children[i].TranslatePoint(new Point(0, 0), tagsPanel);
            if (pos.Y >= 2 * tagRowH)
            {
                overflowIdx = i;
                break;
            }
        }

        if (overflowIdx >= children.Count)
            return;

        for (int i = children.Count - 1; i >= overflowIdx; i--)
            tagsPanel.Children.RemoveAt(i);

        var dotsTag = CreateGameTag("...");
        double dotsWidth = dotsTag.Width + tagMarginR;
        double available = tagsPanel.ActualWidth;

        while (tagsPanel.Children.Count > 0)
        {
            var last = (FrameworkElement)tagsPanel.Children[tagsPanel.Children.Count - 1];
            var lastPos = last.TranslatePoint(new Point(0, 0), tagsPanel);
            if (lastPos.Y < tagRowH) break;

            double rightEdge = lastPos.X + last.ActualWidth + tagMarginR;
            if (rightEdge + dotsWidth <= available) break;

            tagsPanel.Children.RemoveAt(tagsPanel.Children.Count - 1);
        }

        tagsPanel.Children.Add(dotsTag);
    }

    private static readonly string s_deleteIconGeometry =
        "M0.75 4.21094H15.75 M2.625 4.21094H13.875V14.5956C13.875 14.9016 13.7433 15.1951 13.5089 15.4114C13.2745 15.6278 12.9565 15.7494 12.625 15.7494H3.875C3.54348 15.7494 3.22554 15.6278 2.99112 15.4114C2.7567 15.1951 2.625 14.9016 2.625 14.5956V4.21094Z M5.125 4.21154V3.63462C5.125 2.86957 5.45424 2.13585 6.04029 1.59488C6.62634 1.05391 7.4212 0.75 8.25 0.75C9.0788 0.75 9.87366 1.05391 10.4597 1.59488C11.0458 2.13585 11.375 2.86957 11.375 3.63462V4.21154 M6.375 6.51953V12.8657 M10.125 6.51953V12.8657";

    private static readonly string s_copyIconGeometry =
        "M5.36447 4.21154V1.90385C5.36447 1.59783 5.48603 1.30434 5.70242 1.08795C5.91881 0.871566 6.2123 0.75 6.51832 0.75H14.5952C14.9013 0.75 15.1947 0.871566 15.4111 1.08795C15.6275 1.30434 15.7491 1.59783 15.7491 1.90385V14.5962C15.7491 14.9022 15.6275 15.1957 15.4111 15.412C15.1947 15.6284 14.9013 15.75 14.5952 15.75H6.51832C6.2123 15.75 5.91881 15.6284 5.70242 15.412C5.48603 15.1957 5.36447 14.9022 5.36447 14.5962V12.2885 M8.82721 8.25H0.750286 M3.05768 10.5586L0.749986 8.2509L3.05768 5.94321";

    private static readonly string s_editIconGeometry =
        "M0.75 15.75H13.4366 M7.67093 11.7133L4.21094 12.3361L4.7876 8.83001L12.5495 1.09115C12.6567 0.983054 12.7843 0.897252 12.9248 0.838699C13.0654 0.780146 13.2161 0.75 13.3684 0.75C13.5206 0.75 13.6714 0.780146 13.8119 0.838699C13.9525 0.897252 14.08 0.983054 14.1873 1.09115L15.4098 2.31369C15.5179 2.4209 15.6037 2.54846 15.6622 2.68901C15.7208 2.82955 15.7509 2.9803 15.7509 3.13255C15.7509 3.2848 15.7208 3.43555 15.6622 3.5761C15.6037 3.71664 15.5179 3.8442 15.4098 3.95142L7.67093 11.7133Z";

    private static FrameworkElement CreateIconButton(string geometry, Action? onClick = null)
    {
        var normalBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE));

        var path = new Path
        {
            Data = Geometry.Parse(geometry),
            Stroke = normalBrush,
            StrokeThickness = 1.2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Width = 15,
            Height = 15,
            Stretch = Stretch.Uniform
        };

        var grid = new Grid
        {
            Width = 15,
            Height = 15,
            Background = Brushes.Transparent,
            Margin = new Thickness(10, 0, 0, 0)
        };
        grid.Children.Add(path);

        var accentBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xC6, 0x0E, 0x0E));

        grid.MouseEnter += (_, _) => path.Stroke = accentBrush;
        grid.MouseLeave += (_, _) => path.Stroke = normalBrush;

        if (onClick != null)
            grid.PreviewMouseLeftButtonDown += (_, e) => { onClick(); e.Handled = true; };

        return grid;
    }

    private void InitSharedDetailPopup()
    {
        _detailPopup = new Popup
        {
            Placement = PlacementMode.Left,
            HorizontalOffset = -10,
            VerticalOffset = 0,
            AllowsTransparency = true,
            StaysOpen = true
        };

        var accentBrush = (Brush)FindResource("AccentGradient");
        var iconEffect = (Effect)FindResource("TitleIconShadow");
        var orbitronFont = (FontFamily)FindResource("OrbitronFont");

        _detailContentStack = new StackPanel { Margin = new Thickness(20, 20, 20, 20) };

        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var detailIconGrid = new Grid { Width = 8, Height = 26, Effect = iconEffect, Opacity = 0.9, VerticalAlignment = VerticalAlignment.Top };
        var dp = new Path { Data = Geometry.Parse("M0 2L2 0H4L0 4V2Z M8 0H6L0 6V26H5L8 23V0Z"), Fill = accentBrush };
        detailIconGrid.Children.Add(dp);
        Grid.SetColumn(detailIconGrid, 0);
        headerRow.Children.Add(detailIconGrid);

        _detailNameText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromArgb(0xE6, 0xEE, 0xEE, 0xEE)),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            FontFamily = orbitronFont,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(_detailNameText, 1);
        headerRow.Children.Add(_detailNameText);
        _detailContentStack.Children.Add(headerRow);

        _detailGamesPanel = new WrapPanel { Margin = new Thickness(0, 20, 0, 0) };
        _detailContentStack.Children.Add(_detailGamesPanel);

        // Background — simple solid color
        var bgBase = new Path { Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x1B, 0x1B, 0x1B)) };
        _detailPolygonPaths = [bgBase];

        // Border segments — simple solid strokes
        var borderStroke = new SolidColorBrush(Color.FromArgb(0x33, 0xEE, 0xEE, 0xEE));
        var borderTopLeft = new Path { Stroke = borderStroke, StrokeThickness = 1, StrokeStartLineCap = PenLineCap.Flat, StrokeEndLineCap = PenLineCap.Flat };
        var borderTop = new Path { Stroke = borderStroke, StrokeThickness = 1, StrokeStartLineCap = PenLineCap.Flat, StrokeEndLineCap = PenLineCap.Flat };
        var borderRight = new Path { Stroke = borderStroke, StrokeThickness = 1, StrokeStartLineCap = PenLineCap.Flat, StrokeEndLineCap = PenLineCap.Flat };
        var borderBottomRight = new Path { Stroke = borderStroke, StrokeThickness = 1, StrokeStartLineCap = PenLineCap.Flat, StrokeEndLineCap = PenLineCap.Flat };
        var borderBottom = new Path { Stroke = borderStroke, StrokeThickness = 1, StrokeStartLineCap = PenLineCap.Flat, StrokeEndLineCap = PenLineCap.Flat };
        var borderLeft = new Path { Stroke = borderStroke, StrokeThickness = 1, StrokeStartLineCap = PenLineCap.Flat, StrokeEndLineCap = PenLineCap.Flat };
        _detailBorderSegments = [borderTopLeft, borderTop, borderRight, borderBottomRight, borderBottom, borderLeft];

        _detailRootGrid = new Grid { Width = 358 };
        _detailRootGrid.Children.Add(bgBase);
        foreach (var bs in _detailBorderSegments)
            _detailRootGrid.Children.Add(bs);
        _detailRootGrid.Children.Add(_detailContentStack);

        _detailPopup.Opened += (_, _) =>
        {
            _detailContentStack!.UpdateLayout();
            UpdateDetailGeometries();
        };
        _detailPopup.Child = _detailRootGrid;
    }

    private async void ShowDetailPopup(PresetItem preset, FrameworkElement target)
    {
        if (_detailPopup == null) return;

        // 1. 取消上一次可能还未完成的延迟任务
        _popupDelayCts?.Cancel();
        _popupDelayCts = new CancellationTokenSource();
        var token = _popupDelayCts.Token;

        // 2. 先关闭当前可能处于打开状态的弹窗
        if (_detailPopup.IsOpen)
            _detailPopup.IsOpen = false;

        try
        {
            // 3. 核心修改：等待1秒（1000毫秒）
            await Task.Delay(500, token);
        }
        catch (TaskCanceledException)
        {
            // 如果在1秒内触发了 MouseLeave，任务被取消，直接退出
            return;
        }

        // 更新内容数据（放到延迟之后执行，节省性能）
        _detailNameText!.Text = preset.Name;
        _detailGamesPanel!.Children.Clear();
        foreach (var game in preset.Games)
            _detailGamesPanel.Children.Add(CreateGameTag(game));

        // 调用之前修复的尺寸计算方法
        UpdateDetailGeometries();

        // 更新弹窗要挂载的目标位置
        _detailPopup.PlacementTarget = target;

        // 使用 Dispatcher 将“打开”动作推迟到 Render 优先级
        _=Dispatcher.BeginInvoke(new Action(() =>
        {
            // 再次检查：确保等待期间目标没有被清空，且任务未被取消
            if (_detailPopup.PlacementTarget == target && !token.IsCancellationRequested)
            {
                _detailPopup.IsOpen = true;
            }
        }), System.Windows.Threading.DispatcherPriority.Render);
    }

    private void HideDetailPopup()
    {
        // 核心修改：鼠标移开时，立刻取消延迟显示的计时器任务
        _popupDelayCts?.Cancel();

        if (_detailPopup != null)
        {
            _detailPopup.IsOpen = false;

            // 鼠标离开时必须清空目标
            _detailPopup.PlacementTarget = null;
        }
    }

    private void UpdateDetailGeometries()
    {
        if (_detailContentStack == null || _detailRootGrid == null ||
            _detailPolygonPaths == null || _detailBorderSegments == null) return;

        // 1. 核心修复：解除上一次动态计算并写死的高度限制，让容器能够自由撑开以适应新内容
        _detailRootGrid.Height = double.NaN;

        // 2. 核心修复：Popup 在关闭状态下 ActualHeight 不会刷新，必须强制 Measure 来获取所需的确切高度
        // 358 是你设定的 _detailRootGrid 的宽度
        _detailContentStack.Measure(new Size(358, double.PositiveInfinity));

        // 3. 计算新高度：
        // DesiredSize.Height 已经包含了 _detailContentStack 上下各 20 的 Margin（共 40）。
        // 你原来的公式是：ActualHeight（不含 Margin）+ 20 + 12 = 净内容高 + 32。
        // 为了完全维持你原有的 UI 比例和内间距，这里直接用 DesiredSize.Height - 8 即可。
        var totalH = _detailContentStack.DesiredSize.Height - 8;

        if (totalH <= 32) return;

        // 重新绘制背景多边形
        var geo = Geometry.Parse($"M358 0H9L0 9V{totalH}H349L358 {totalH - 9}V0Z");
        foreach (var p in _detailPolygonPaths)
            p.Data = geo;

        // 重新绘制发光边框线条
        _detailBorderSegments[0].Data = new LineGeometry(new Point(9, 0), new Point(0, 9));
        _detailBorderSegments[1].Data = new LineGeometry(new Point(9, 0), new Point(358, 0));
        _detailBorderSegments[2].Data = new LineGeometry(new Point(358, 0), new Point(358, totalH - 9));
        _detailBorderSegments[3].Data = new LineGeometry(new Point(358, totalH - 9), new Point(349, totalH));
        _detailBorderSegments[4].Data = new LineGeometry(new Point(349, totalH), new Point(0, totalH));
        _detailBorderSegments[5].Data = new LineGeometry(new Point(0, totalH), new Point(0, 9));

        // 4. 将计算好匹配倒角的总高度重新赋给 RootGrid
        _detailRootGrid.Height = totalH;
    }

    private FrameworkElement CreateGameTag(string gameName)
    {
        var text = new TextBlock
        {
            Text = gameName,
            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE)),
            FontSize = 16,
            FontWeight = FontWeights.Regular,
            FontFamily = (FontFamily)FindResource("OrbitronFont"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(20, 0, 20, 0)
        };
        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var textWidth = text.DesiredSize.Width;
        var totalW = textWidth;
        //var totalW = textWidth + 36;
        //if (totalW < 60) totalW = 60;

        var paraPath = new Path
        {
            Data = Geometry.Parse($"M10 0H{totalW}L{totalW - 10} 26H0L10 0Z"),
            Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xEE, 0xEE, 0xEE)),
            StrokeStartLineCap = PenLineCap.Flat,
            StrokeEndLineCap = PenLineCap.Flat,
            StrokeLineJoin = PenLineJoin.Miter
        };

        var grid = new Grid
        {
            Width = totalW,
            Height = 26,
            Margin = new Thickness(0, 0, 5, 10)
        };
        grid.Children.Add(paraPath);
        grid.Children.Add(text);
        return grid;
    }

    // ══════════════════════════════════════════
    //  事件处理
    // ══════════════════════════════════════════

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == OverlayBackground)
            Hide();
    }

    private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = CategoryComboBox.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(selected))
        {
            _currentCategory = "全部";
            return;
        }
        var match = System.Text.RegularExpressions.Regex.Match(selected, @"\(([^)]+)\)");
        _currentCategory = match.Success ? match.Groups[1].Value : selected;
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        RenderPresetList();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPreset = GetSelectedPreset();
        if (selectedPreset != null)
            PresetApplied?.Invoke(this, selectedPreset);
        Hide();
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        PresetImported?.Invoke(this, EventArgs.Empty);
    }

    // ══════════════════════════════════════════
    //  公开方法
    // ══════════════════════════════════════════

    public void SetOfficialPresets(IEnumerable<PresetItem> presets)
    {
        _officialPresets.Clear();
        _officialPresets.AddRange(presets);
        if (_isOfficialTab) RenderPresetList();
    }

    public void SetPersonalPresets(IEnumerable<PresetItem> presets)
    {
        _personalPresets.Clear();
        _personalPresets.AddRange(presets);
        if (!_isOfficialTab) RenderPresetList();
    }

    public void AddPersonalPreset(PresetItem preset)
    {
        _personalPresets.Add(preset);
        if (!_isOfficialTab) RenderPresetList();
    }

    public void RemovePersonalPreset(PresetItem preset)
    {
        _personalPresets.Remove(preset);
        if (!_isOfficialTab) RenderPresetList();
    }

    private PresetItem? GetSelectedPreset()
    {
        // 返回列表中第一个预设作为当前选中项（可扩展为支持点击选中）
        var source = _isOfficialTab ? _officialPresets : _personalPresets;
        return source.FirstOrDefault();
    }
}

public class PresetItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public List<string> Games { get; set; } = new();
}
