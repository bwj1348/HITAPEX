using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using HITAPEX.Models.Usb;
using SharpVectors.Converters;

namespace HITAPEX.Views.DeviceParameters;

public partial class PresetListPopup : UserControl
{
    private readonly List<PresetItem> _officialPresets = new();
    private readonly List<PresetItem> _personalPresets = new();
    private readonly List<string> _allGameItems = new();
    private bool _isOfficialTab = true;
    private string _currentCategory = "全部";
    private PresetItem? _selectedPreset;
    private ContentControl? _selectedControl;
    private TextBox? _filterTextBox;
    private TextBlock? _contentSite;
    private TextBlock? _watermark;
    private object? _previousGameSelection;
    // Shared detail popup — one instance for all preset items
    private Popup? _detailPopup;
    private TextBlock? _detailNameText;
    private WrapPanel? _detailGamesPanel;
    private StackPanel? _detailContentStack;
    private Grid? _detailRootGrid;
    private List<Path>? _detailPolygonPaths;
    private List<Path>? _detailBorderSegments;

    public event EventHandler<PresetItem>? PresetApplied;

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
        LoadPresets();
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
                    SavePersonalPresets();
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

    private void LoadPresets()
    {
        if (App.PresetService != null)
        {
            var official = App.PresetService.LoadOfficialPresets();
            _officialPresets.AddRange(official);

            var personal = App.PresetService.LoadPersonalPresets();
            _personalPresets.AddRange(personal);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[PresetListPopup] PresetService 不可用");
        }
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
        _allGameItems.Clear();
        _allGameItems.Add("Assetto Corsa Competizione (ACC)");
        _allGameItems.Add("Assetto Corsa Evo (AC EVO)");
        _allGameItems.Add("Forza Horizon 5 (FH5)");
        _allGameItems.Add("Forza Motorsport (FM)");
        _allGameItems.Add("Dirt Rally 2.0 (DR2.0)");
        _allGameItems.Add("EA Sports WRC (EA WRC)");
        _allGameItems.Add("GT World Challenge (GTWC)");
        _allGameItems.Add("Intercontinental GT Challenge (IGTC)");
        _allGameItems.Add("GT4 European Series (GT4)");
        _allGameItems.Add("iRacing (iR)");
        _allGameItems.Add("F1 24 (F1)");
        _allGameItems.Add("Le Mans Ultimate (LMU)");

        CategoryComboBox.DropDownOpened += CategoryComboBox_DropDownOpened;
        CategoryComboBox.DropDownClosed += CategoryComboBox_DropDownClosed;
        ResetComboBoxFilter();
        CategoryComboBox.SelectedIndex = -1;
    }

    // ══════════════════════════════════════════
    //  下拉框文本筛选
    // ══════════════════════════════════════════

    private void CategoryComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        if (_filterTextBox == null)
        {
            _filterTextBox = CategoryComboBox.Template.FindName("PART_FilterTextBox", CategoryComboBox) as TextBox;
            _contentSite = CategoryComboBox.Template.FindName("ContentSite", CategoryComboBox) as TextBlock;
            _watermark = CategoryComboBox.Template.FindName("Watermark", CategoryComboBox) as TextBlock;

            if (_filterTextBox != null)
            {
                _filterTextBox.TextChanged += FilterTextBox_TextChanged;
                _filterTextBox.PreviewKeyDown += FilterTextBox_PreviewKeyDown;
                _filterTextBox.GotFocus += FilterTextBox_GotFocus;
                _filterTextBox.LostKeyboardFocus += FilterTextBox_LostKeyboardFocus;
            }
        }

        // 记录展开前的选中项，用于筛选后未选择时恢复
        _previousGameSelection = CategoryComboBox.SelectedItem;

        // 重置筛选文本，恢复全部列表（但保留已选中的项）
        ResetComboBoxFilter();

        // 保持 ContentSite/Watermark 可见，TextBox 透明覆盖在上方
        ShowContentSiteOrWatermark();
    }

    private void CategoryComboBox_DropDownClosed(object? sender, EventArgs e)
    {
        // 如果筛选后用户没有选中新游戏，恢复展开前的选中项
        if (_filterTextBox != null && !string.IsNullOrWhiteSpace(_filterTextBox.Text))
        {
            if (CategoryComboBox.SelectedIndex == -1 && _previousGameSelection != null)
            {
                CategoryComboBox.SelectedItem = _previousGameSelection;
            }
        }

        // 清空筛选文本、恢复全部列表
        ResetComboBoxFilter();

        // 必须手动更新 ContentSite/Watermark，因为代码设置的局部 Visibility
        // 优先级高于 ControlTemplate 触发器，触发器无法覆盖局部值
        ShowContentSiteOrWatermark();
    }

    private void FilterTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        // 用户点击文本框开始输入 → 隐藏 ContentSite 和 Watermark
        HideContentSiteAndWatermark();
    }

    private void FilterTextBox_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        // 下拉框仍展开时，鼠标悬停到展开项会导致 TextBox 失焦，
        // 延迟重新获取焦点以保持 TextBox 可接收键盘输入
        if (CategoryComboBox.IsDropDownOpen)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (CategoryComboBox.IsDropDownOpen && _filterTextBox != null)
                    _filterTextBox.Focus();
            }), System.Windows.Threading.DispatcherPriority.Input);
            return;
        }

        // 下拉框已关闭时，如果文本框为空则恢复 ContentSite/Watermark
        if (_filterTextBox != null && string.IsNullOrEmpty(_filterTextBox.Text))
        {
            ShowContentSiteOrWatermark();
        }
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filterText = _filterTextBox?.Text ?? string.Empty;

        // 一旦有输入文本，隐藏 ContentSite 和 Watermark
        if (!string.IsNullOrWhiteSpace(filterText))
        {
            HideContentSiteAndWatermark();
        }

        DoFilterItems(filterText);
    }

    private void DoFilterItems(string filterText)
    {
        CategoryComboBox.SelectionChanged -= CategoryComboBox_SelectionChanged;
        CategoryComboBox.Items.Clear();

        if (string.IsNullOrWhiteSpace(filterText))
        {
            foreach (var item in _allGameItems)
                CategoryComboBox.Items.Add(item);
        }
        else
        {
            foreach (var item in _allGameItems)
            {
                if (item.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                    CategoryComboBox.Items.Add(item);
            }
        }

        CategoryComboBox.SelectedIndex = -1;
        CategoryComboBox.SelectionChanged += CategoryComboBox_SelectionChanged;
    }

    private void FilterTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case System.Windows.Input.Key.Down:
                if (CategoryComboBox.Items.Count > 0)
                {
                    var idx = CategoryComboBox.SelectedIndex;
                    if (idx < CategoryComboBox.Items.Count - 1)
                        CategoryComboBox.SelectedIndex = idx + 1;
                }
                e.Handled = true;
                break;

            case System.Windows.Input.Key.Up:
                if (CategoryComboBox.Items.Count > 0)
                {
                    var idx = CategoryComboBox.SelectedIndex;
                    if (idx > 0)
                        CategoryComboBox.SelectedIndex = idx - 1;
                }
                e.Handled = true;
                break;

            case System.Windows.Input.Key.Enter:
                CategoryComboBox.IsDropDownOpen = false;
                e.Handled = true;
                break;

            case System.Windows.Input.Key.Escape:
                // 先恢复选中项，再关闭
                if (_previousGameSelection != null)
                    CategoryComboBox.SelectedItem = _previousGameSelection;
                ResetComboBoxFilter();
                CategoryComboBox.IsDropDownOpen = false;
                e.Handled = true;
                break;
        }
    }

    private void HideContentSiteAndWatermark()
    {
        if (_contentSite != null) _contentSite.Visibility = Visibility.Collapsed;
        if (_watermark != null) _watermark.Visibility = Visibility.Collapsed;
    }

    private void ShowContentSiteOrWatermark()
    {
        if (_contentSite == null || _watermark == null) return;

        if (CategoryComboBox.SelectedIndex >= 0)
        {
            _contentSite.Visibility = Visibility.Visible;
            _watermark.Visibility = Visibility.Collapsed;
        }
        else
        {
            _contentSite.Visibility = Visibility.Collapsed;
            _watermark.Visibility = Visibility.Visible;
        }
    }

    private void ResetComboBoxFilter()
    {
        // 必须在清空 TextBox 文本之前保存选中项
        // 因为设置 TextBox.Text 会触发 TextChanged → DoFilterItems → SelectedIndex = -1
        var currentSelection = CategoryComboBox.SelectedItem;

        if (_filterTextBox != null)
        {
            _filterTextBox.TextChanged -= FilterTextBox_TextChanged;
            _filterTextBox.Text = string.Empty;
            _filterTextBox.TextChanged += FilterTextBox_TextChanged;
        }

        CategoryComboBox.SelectionChanged -= CategoryComboBox_SelectionChanged;
        CategoryComboBox.Items.Clear();
        foreach (var item in _allGameItems)
            CategoryComboBox.Items.Add(item);

        if (currentSelection != null && CategoryComboBox.Items.Contains(currentSelection))
            CategoryComboBox.SelectedItem = currentSelection;
        else
            CategoryComboBox.SelectedIndex = -1;

        CategoryComboBox.SelectionChanged += CategoryComboBox_SelectionChanged;
    }

    // ══════════════════════════════════════════
    //  选项卡切换
    // ══════════════════════════════════════════

    private void TabOfficial_Click(object sender, RoutedEventArgs e)
    {
        _isOfficialTab = true;
        DeselectCurrentItem();
        UpdateTabVisuals();
        UpdateBottomButtons();
        RenderPresetList();
    }

    private void TabPersonal_Click(object sender, RoutedEventArgs e)
    {
        _isOfficialTab = false;
        DeselectCurrentItem();
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

        item.PreviewMouseLeftButtonDown += (_, _) => SelectPresetItem(preset, item);
        item.MouseDoubleClick += (_, _) => ApplySelectedPreset();

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

        item.PreviewMouseLeftButtonDown += (_, _) => SelectPresetItem(preset, item);
        item.MouseDoubleClick += (_, _) => ApplySelectedPreset();

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
            ShowDeleteConfirmDialog(preset);
        }));
        iconPanel.Children.Add(CreateIconButton(s_copyIconGeometry, () =>
        {
            ExportPreset(preset);
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
        _currentCategory = "全部";
        CategoryComboBox.SelectedIndex = -1;
        ShowContentSiteOrWatermark();
        RenderPresetList();
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
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入预设",
            Filter = "预设文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dlg.ShowDialog() == true)
        {
            var imported = App.PresetService?.ImportPreset(dlg.FileName);
            if (imported != null)
            {
                _personalPresets.Add(imported);
                SavePersonalPresets();
                RenderPresetList();
            }
            else
            {
                MessageBox.Show("导入预设失败，文件格式不正确。", "导入失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ══════════════════════════════════════════
    //  公开方法
    // ══════════════════════════════════════════

    public void RefreshPersonalPresets(List<PresetItem> presets)
    {
        _personalPresets.Clear();
        _personalPresets.AddRange(presets);
        if (!_isOfficialTab) RenderPresetList();
    }

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

    private void SavePersonalPresets()
    {
        App.PresetService?.SavePersonalPresets(_personalPresets);
    }

    private PresetItem? GetSelectedPreset()
    {
        return _selectedPreset;
    }

    private void SelectPresetItem(PresetItem preset, ContentControl control)
    {
        // Deselect previous
        DeselectCurrentItem();

        _selectedPreset = preset;
        _selectedControl = control;

        // Apply hover-like selected visual
        var bgRect = control.Template.FindName("BgRect", control) as System.Windows.Shapes.Rectangle;
        if (bgRect != null)
        {
            bgRect.Stroke = new SolidColorBrush(Color.FromArgb(0x99, 0xC6, 0x0E, 0x0E));
            bgRect.Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0x33, 0x60, 0x07, 0x07), 0),
                    new GradientStop(Color.FromArgb(0x33, 0xC6, 0x0E, 0x0E), 0.5),
                    new GradientStop(Color.FromArgb(0x33, 0x60, 0x07, 0x07), 1)
                }
            };
        }
    }

    private void DeselectCurrentItem()
    {
        if (_selectedControl != null)
        {
            var bgRect = _selectedControl.Template.FindName("BgRect", _selectedControl) as System.Windows.Shapes.Rectangle;
            if (bgRect != null)
            {
                bgRect.ClearValue(System.Windows.Shapes.Rectangle.StrokeProperty);
                bgRect.ClearValue(System.Windows.Shapes.Rectangle.FillProperty);
            }
        }

        _selectedPreset = null;
        _selectedControl = null;
    }

    private void ApplySelectedPreset()
    {
        if (_selectedPreset != null)
            PresetApplied?.Invoke(this, _selectedPreset);
        Hide();
    }

    private void ShowDeleteConfirmDialog(PresetItem preset)
    {
        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = "删 除 预 设";
        dialog.ClearButtons();

        dialog.DialogContent = new TextBlock
        {
            Text = "该预设将被永久删除，且无法恢复。",
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        dialog.AddButton("删 除", (_, _) =>
        {
            dialog.Hide();
            _personalPresets.Remove(preset);
            SavePersonalPresets();
            RenderPresetList();
        }, isPrimary: true);

        dialog.AddButton("取 消", (_, _) =>
        {
            dialog.Hide();
        }, isPrimary: false);

        dialog.Show();
    }

    private void ExportPreset(PresetItem preset)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出预设",
            Filter = "预设文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = ".json",
            FileName = preset.Name
        };

        if (dlg.ShowDialog() != true || App.PresetService == null) return;

        var fileName = dlg.FileName;
        TryExportPresetWithRetry(preset, fileName);
    }

    private void TryExportPresetWithRetry(PresetItem preset, string fileName)
    {
        if (PerformExportPreset(preset, fileName))
        {
            ShowExportSuccessToast("导 出 成 功");
            return;
        }

        ShowExportFailedDialog(() => TryExportPresetWithRetry(preset, fileName));
    }

    private bool PerformExportPreset(PresetItem preset, string fileName)
    {
        try
        {
            App.PresetService!.ExportPreset(preset, fileName);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresetListPopup] 导出预设失败: {ex.Message}");
            return false;
        }
    }

    private void ShowExportSuccessToast(string message)
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

        toast.Children.Add(new SvgViewbox
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
            Text = message,
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

    private void ShowExportFailedDialog(Action? onRetry)
    {
        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = "导 出 失 败";
        dialog.ShowIcon = true;
        dialog.ClearButtons();

        dialog.DialogContent = new TextBlock
        {
            Text = "当前预设导出失败，请检查后重试。",
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        dialog.AddButton("重 试", (_, _) =>
        {
            dialog.Hide();
            onRetry?.Invoke();
        }, isPrimary: true);

        dialog.AddButton("取 消", (_, _) =>
        {
            dialog.Hide();
        }, isPrimary: false);

        dialog.Show();
    }
}

public class PresetItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public List<string> Games { get; set; } = new();
    public Models.Usb.PedalPresetSnapshot? Parameters { get; set; }

    /// <summary>是否为个人预设（可编辑/删除），官方预设不可编辑</summary>
    public bool IsPersonal { get; set; }
}
