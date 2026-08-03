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
using HITAPEX.Services;
using SharpVectors.Converters;

namespace HITAPEX.Views.DeviceParameters;

/// <summary>
/// 预设浏览/选择侧滑弹窗。
/// 提供官方预设和个人预设两个选项卡，支持按游戏类别筛选、鼠标悬停显示详情弹窗、
/// 个人预设的编辑/删除/导出操作，以及导入外部预设文件功能。
/// 弹窗从右侧滑入，点击遮罩层或应用预设后滑出关闭。
/// </summary>
public partial class PresetListPopup : UserControl
{
    // ══════════════════════════════════════════
    //  字段
    // ══════════════════════════════════════════

    /// <summary>是否已完成首次初始化</summary>
    private bool _isInitialized;

    /// <summary>官方预设列表（只读展示，不可编辑）</summary>
    private readonly List<PresetItem> _officialPresets = new();

    /// <summary>个人预设列表（可编辑、删除、导出）</summary>
    private readonly List<PresetItem> _personalPresets = new();

    /// <summary>所有游戏类别项（用于下拉框筛选）</summary>
    private readonly List<string> _allGameItems = new();

    /// <summary>当前显示的是官方预设选项卡还是个人预设选项卡</summary>
    private bool _isOfficialTab = true;

    /// <summary>当前选中的游戏类别筛选条件</summary>
    private string _currentCategory = LocalizationService.Instance["Preset.All"];

    /// <summary>当前被选中的预设项</summary>
    private PresetItem? _selectedPreset;

    /// <summary>当前被选中预设项对应的 UI 控件</summary>
    private ContentControl? _selectedControl;

    /// <summary>首次加载前暂存待定位的预设名称，Loaded 完成后执行</summary>
    private string? _pendingSelectName;

    // ---- ComboBox 搜索筛选相关 ----

    /// <summary>覆盖在 ComboBox ContentSite 上的 TextBox，实现输入即搜索</summary>
    private TextBox? _filterTextBox;

    /// <summary>ComboBox 模板中的 ContentSite，显示当前选中项文本</summary>
    private TextBlock? _contentSite;

    /// <summary>ComboBox 模板中的 Watermark 提示文本</summary>
    private TextBlock? _watermark;

    /// <summary>展开 ComboBox 前的选中项，用于取消筛选时恢复</summary>
    private object? _previousGameSelection;

    // ---- 共享详情弹窗相关（所有预设项共用同一个 Popup 实例，避免内存浪费） ----

    /// <summary>鼠标悬停预设项时显示的详情弹窗</summary>
    private Popup? _detailPopup;

    /// <summary>详情弹窗中的预设名称文本</summary>
    private TextBlock? _detailNameText;

    /// <summary>详情弹窗中的游戏标签容器</summary>
    private WrapPanel? _detailGamesPanel;

    /// <summary>详情弹窗中的内容堆叠面板</summary>
    private StackPanel? _detailContentStack;

    /// <summary>详情弹窗的根网格容器</summary>
    private Grid? _detailRootGrid;

    /// <summary>详情弹窗背景切角多边形的 Path 列表</summary>
    private List<Path>? _detailPolygonPaths;

    /// <summary>详情弹窗边框线条的 Path 列表</summary>
    private List<Path>? _detailBorderSegments;

    /// <summary>当前弹窗对应的设备类型</summary>
    public Models.Usb.DeviceType DeviceType { get; set; } = Models.Usb.DeviceType.Pedal;

    /// <summary>用户点击"应用"按钮时触发，传递选中的预设</summary>
    public event EventHandler<PresetItem>? PresetApplied;

    /// <summary>
    /// 用于控制详情弹窗延迟显示任务的取消令牌。
    /// 鼠标悬停在预设项上 500ms 后才弹出详情弹窗；
    /// 如果在 500ms 内鼠标移开，则取消弹窗。
    /// </summary>
    private CancellationTokenSource? _popupDelayCts;

    public PresetListPopup()
    {
        InitializeComponent();
        // 推迟实际初始化到 Loaded 事件，确保 XAML 模板已应用
        Loaded += OnLoaded;
    }

    /// <summary>首次加载时初始化 ComboBox、加载预设数据、创建共享详情弹窗并渲染列表</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 防止重复初始化（Loaded 可能被多次触发）
        if (_isInitialized) return;
        _isInitialized = true;

        InitCategoryComboBox();
        LoadPresets();
        InitSharedDetailPopup();
        RenderPresetList();
        // 滚动条同步：ScrollViewer 滚动时更新自定义滚动条
        PresetScrollViewer.ScrollChanged += PresetScrollViewer_ScrollChanged;
    }

    // ══════════════════════════════════════════
    //  编辑弹窗（仅个人预设）
    // ══════════════════════════════════════════

    /// <summary>打开编辑弹窗，允许用户修改个人预设的名称和参数</summary>
    private void OpenEditPopup(PresetItem preset)
    {
        var editPopup = new EditPresetPopup();
        editPopup.Tag = preset.Name;
        editPopup.DeviceType = DeviceType;
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

        // 将编辑弹窗添加到当前页面的根面板上，实现覆盖显示
        if (Content is Panel rootPanel)
            rootPanel.Children.Add(editPopup);

        editPopup.BeginEdit(preset, _personalPresets.Select(p => p.Name));
        editPopup.Show();
    }

    /// <summary>从根面板移除编辑弹窗</summary>
    private void RemoveEditPopup(EditPresetPopup popup)
    {
        if (Content is Panel rootPanel)
            rootPanel.Children.Remove(popup);
    }

    // ══════════════════════════════════════════
    //  滚动条同步
    // ══════════════════════════════════════════

    /// <summary>ScrollViewer 滚动时同步更新自定义滚动条的位置和大小</summary>
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

    // ══════════════════════════════════════════
    //  数据加载
    // ══════════════════════════════════════════

    /// <summary>从 PresetService 加载官方预设和个人预设数据</summary>
    private void LoadPresets()
    {
        if (App.PresetService != null)
        {
            var official = App.PresetService.LoadOfficialPresets(DeviceType);
            _officialPresets.AddRange(official);

            var personal = App.PresetService.LoadPersonalPresets(DeviceType);
            _personalPresets.AddRange(personal);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[PresetListPopup] PresetService 不可用");
        }
    }

    // ══════════════════════════════════════════
    //  显示 / 隐藏
    // ══════════════════════════════════════════

    /// <summary>显示弹窗，触发从右侧滑入动画</summary>
    public void Show()
    {
        Visibility = Visibility.Visible;
        AnimateIn();
    }

    /// <summary>隐藏弹窗，触发向右侧滑出动画，动画完成后设置为 Collapsed</summary>
    public void Hide()
    {
        AnimateOut(() => Visibility = Visibility.Collapsed);
    }

    // ══════════════════════════════════════════
    //  动画：从右侧滑入/滑出
    //  使用 TranslateTransform 控制 PopupPanel 的水平位移，
    //  配合 Opacity 动画实现淡入淡出效果
    // ══════════════════════════════════════════

    /// <summary>
    /// 滑入动画：弹窗面板从右侧（X = PanelWidth）平移到 X = 0，
    /// 同时面板和遮罩层淡入显示。
    /// </summary>
    private void AnimateIn()
    {
        // 初始状态：遮罩透明、面板透明、面板位于右侧
        OverlayBackground.Opacity = 0;
        PopupPanel.Opacity = 0;
        PopupPanel.RenderTransform = new TranslateTransform(PopupPanel.Width, 0);
        PopupPanel.IsHitTestVisible = false;

        // 面板从右侧滑入到原始位置
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

        // 面板淡入完成后才允许点击交互
        panelFade.Completed += (_, _) => { PopupPanel.IsHitTestVisible = true; };

        PopupPanel.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
        PopupPanel.BeginAnimation(OpacityProperty, panelFade);
        OverlayBackground.BeginAnimation(OpacityProperty, overlayFade);
    }

    /// <summary>
    /// 滑出动画：弹窗面板从当前 X 位置平移到 X = PanelWidth（右侧之外），
    /// 同时面板和遮罩层淡出，动画完成后执行回调。
    /// </summary>
    private void AnimateOut(Action onCompleted)
    {
        if (PopupPanel.RenderTransform is not TranslateTransform translate)
            PopupPanel.RenderTransform = translate = new TranslateTransform(0, 0);

        PopupPanel.IsHitTestVisible = false;

        // 面板从当前位置滑出到右侧
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

        // 面板淡出完成后设置 Visibility = Collapsed
        panelFade.Completed += (_, _) => { onCompleted(); };

        translate.BeginAnimation(TranslateTransform.XProperty, slideOut);
        PopupPanel.BeginAnimation(OpacityProperty, panelFade);
        OverlayBackground.BeginAnimation(OpacityProperty, overlayFade);
    }

    // ══════════════════════════════════════════
    //  ComboBox 初始化：游戏类别下拉筛选
    // ══════════════════════════════════════════

    /// <summary>初始化游戏类别 ComboBox，填充所有游戏项并挂载事件</summary>
    private void InitCategoryComboBox()
    {
        _allGameItems.Clear();
        foreach (var game in Models.GameListConfig.GetGames())
        {
            if (!string.IsNullOrEmpty(game.Abbreviation))
                _allGameItems.Add($"{game.Name} ({game.Abbreviation})");
        }

        CategoryComboBox.DropDownOpened += CategoryComboBox_DropDownOpened;
        CategoryComboBox.DropDownClosed += CategoryComboBox_DropDownClosed;
        ResetComboBoxFilter();
        CategoryComboBox.SelectedIndex = -1;
    }

    // ══════════════════════════════════════════
    //  ComboBox 搜索即输入（Search-as-you-type）
    //  通过 Template 查找 ContentSite 和 Watermark，
    //  将一个透明的 TextBox 覆盖在 ContentSite 上方，
    //  用户输入时实时筛选下拉列表。
    // ══════════════════════════════════════════

    /// <summary>下拉框展开时初始化筛选 TextBox，记录展开前选中项</summary>
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

    /// <summary>用户点击筛选文本框获得焦点时，隐藏 ContentSite 和 Watermark 以显示输入光标</summary>
    private void FilterTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        // 用户点击文本框开始输入 → 隐藏 ContentSite 和 Watermark
        HideContentSiteAndWatermark();
    }

    /// <summary>
    /// 筛选文本框失去焦点时：
    /// - 下拉框仍展开 → 延迟重新获取焦点（因为鼠标悬停项会导致 TextBox 失焦）
    /// - 下拉框已关闭且文本为空 → 恢复 ContentSite/Watermark
    /// </summary>
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

    /// <summary>筛选文本变化时，隐藏 Watermark 并实时过滤下拉框项</summary>
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

    /// <summary>根据筛选文本过滤 ComboBox 中的游戏类别项</summary>
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

    /// <summary>处理筛选文本框的键盘事件：上下键导航、回车确认、Esc 取消</summary>
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

    /// <summary>隐藏 ContentSite 和 Watermark，露出下方的筛选 TextBox</summary>
    private void HideContentSiteAndWatermark()
    {
        if (_contentSite != null) _contentSite.Visibility = Visibility.Collapsed;
        if (_watermark != null) _watermark.Visibility = Visibility.Collapsed;
    }

    /// <summary>根据当前是否有选中项来显示 ContentSite 或 Watermark</summary>
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

    /// <summary>
    /// 重置 ComboBox 筛选状态：清空筛选文本、恢复完整列表，
    /// 并保持之前选中的项不变。
    /// </summary>
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

    /// <summary>切换到官方预设选项卡</summary>
    private void TabOfficial_Click(object sender, RoutedEventArgs e)
    {
        _isOfficialTab = true;
        DeselectCurrentItem();
        UpdateTabVisuals();
        UpdateBottomButtons();
        RenderPresetList();
    }

    /// <summary>切换到个人预设选项卡</summary>
    private void TabPersonal_Click(object sender, RoutedEventArgs e)
    {
        _isOfficialTab = false;
        DeselectCurrentItem();
        UpdateTabVisuals();
        UpdateBottomButtons();
        RenderPresetList();
    }

    /// <summary>更新选项卡下划线可见性，标识当前选中的选项卡</summary>
    private void UpdateTabVisuals()
    {
        TabOfficialUnderline.Visibility = _isOfficialTab ? Visibility.Visible : Visibility.Collapsed;
        TabPersonalUnderline.Visibility = _isOfficialTab ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>个人选项卡显示导入按钮，官方选项卡隐藏导入按钮</summary>
    private void UpdateBottomButtons()
    {
        ImportButton.Visibility = _isOfficialTab ? Visibility.Collapsed : Visibility.Visible;
    }

    // ══════════════════════════════════════════
    //  预设列表渲染
    // ══════════════════════════════════════════

    /// <summary>根据当前选项卡和游戏类别筛选条件重新渲染预设列表</summary>
    private void RenderPresetList()
    {
        PresetItemsControl.Items.Clear();

        var source = _isOfficialTab ? _officialPresets : _personalPresets;
        var filtered = _currentCategory == LocalizationService.Instance["Preset.All"]
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

    /// <summary>在渲染完成后同步自定义滚动条的状态</summary>
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

    /// <summary>自定义滚动条拖动时同步滚动 ScrollViewer</summary>
    private void PresetScrollBar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
    {
        PresetScrollViewer.ScrollToVerticalOffset(e.NewValue);
    }

    /// <summary>创建官方预设列表项 UI 控件（含旗帜图标和预设名称）</summary>
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

        item.Tag = preset; // 标记预设数据，供 SelectAndScrollToPreset 快速查找
        item.PreviewMouseLeftButtonDown += (_, _) => SelectPresetItem(preset, item);
        item.MouseDoubleClick += (_, _) => ApplySelectedPreset();

        if (preset.Games.Count > 0)
        {
            item.MouseEnter += (_, _) => ShowDetailPopup(preset, item);
            item.MouseLeave += (_, _) => HideDetailPopup();
        }

        return item;
    }

    /// <summary>
    /// 创建个人预设列表项 UI 控件（含旗帜图标、名称、游戏标签、编辑/删除/导出按钮）。
    /// 动态判断名称和标签是否能在一行显示，若不能才将预设名称宽度限制为 102。
    /// </summary>
    private FrameworkElement CreatePersonalPresetItem(PresetItem preset)
    {
        var item = new ContentControl
        {
            Style = (Style)FindResource("PersonalPresetItemStyle")
        };

        item.Tag = preset; // 标记预设数据，供 SelectAndScrollToPreset 快速查找
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

        // Preset name (移除硬编码的 MaxWidth = 102)
        var nameText = new TextBlock
        {
            Text = preset.Name,
            // MaxWidth = 102,  <-- 已移除，默认允许根据内容撑开
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

        // Game tags — wrap naturally
        var tagsPanel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,0,-10) };
        foreach (var game in preset.Games)
            tagsPanel.Children.Add(CreateGameTag(game));

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
        iconPanel.Children.Add(CreateIconButton(s_deleteIconGeometry, () => ShowDeleteConfirmDialog(preset)));
        iconPanel.Children.Add(CreateIconButton(s_copyIconGeometry, () => ExportPreset(preset)));
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

        // ==========================================
        // 动态宽度与标签修剪逻辑
        // ==========================================
        mainGrid.Loaded += (_, _) =>
        {
            // 1. 测量无任何空间限制下（标签不换行）的理想尺寸
            nameText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            tagsPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            
            double desiredTotalWidth = nameText.DesiredSize.Width + tagsPanel.DesiredSize.Width;

            // 2. 计算 Name (Col 1) 和 Tags (Col 2) 可用的实际总宽度
            // 公式：网格总宽 - 左侧图标所在列的实际宽度 - 右侧操作栏所在列的实际宽度
            double availableWidth = mainGrid.ActualWidth 
                                    - mainGrid.ColumnDefinitions[0].ActualWidth 
                                    - mainGrid.ColumnDefinitions[3].ActualWidth;

            // 3. 如果理想总宽超出可用空间，说明一行装不下
            if (desiredTotalWidth > availableWidth)
            {
                nameText.MaxWidth = 102;
                mainGrid.UpdateLayout(); // 强制更新布局以应用新的 MaxWidth，迫使标签面板换行或挤压
            }

            // 4. 执行原有的溢出修剪逻辑
            TrimTagOverflow(tagsPanel);
        };

        return item;
    }

    /// <summary>
    /// 游戏标签溢出修剪：最多显示 2 行，超出部分用 "..." 标签替代。
    /// 先截断第 3 行及以后的标签，再在最后一行末尾腾出空间放置 "..." 标签。
    /// </summary>
    private void TrimTagOverflow(WrapPanel tagsPanel)
    {
        const double tagRowH = 36;
        const double tagMarginR = 5;

        var children = tagsPanel.Children.Cast<FrameworkElement>().ToList();
        if (children.Count == 0) return;

        // 第一步：找到第 3 行及以后的标签，全部移除
        int overflowIdx = children.Count;
        for (int i = 0; i < children.Count; i++)
        {
            var pos = children[i].TranslatePoint(new Point(0, 0), tagsPanel);
            // Y >= 2 行高度 → 属于第 3 行及更后
            if (pos.Y >= 2 * tagRowH)
            {
                overflowIdx = i;
                break;
            }
        }

        // 如果没有溢出（所有标签都在 2 行以内），直接返回
        if (overflowIdx >= children.Count)
            return;

        // 移除第 3 行及之后的所有标签
        for (int i = children.Count - 1; i >= overflowIdx; i--)
            tagsPanel.Children.RemoveAt(i);

        // 第二步：在最后一行末尾腾出空间放置 "..." 标签
        var dotsTag = CreateGameTag("...");
        double dotsWidth = dotsTag.Width + tagMarginR;
        double available = tagsPanel.ActualWidth;

        // 从后向前移除标签，直到最后一行有足够空间放入 "..."
        while (tagsPanel.Children.Count > 0)
        {
            var last = (FrameworkElement)tagsPanel.Children[tagsPanel.Children.Count - 1];
            var lastPos = last.TranslatePoint(new Point(0, 0), tagsPanel);
            if (lastPos.Y < tagRowH) break; // 最后一项在第一行，不修剪

            double rightEdge = lastPos.X + last.ActualWidth + tagMarginR;
            if (rightEdge + dotsWidth <= available) break; // 有足够空间放 "..."

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

    /// <summary>创建图标按钮（Path + Grid），支持鼠标悬停颜色变化和点击回调</summary>
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

    /// <summary>
    /// 初始化共享详情弹窗（所有预设项共用单个 Popup 实例）。
    /// 包含预设名称、游戏标签列表，背景为切角多边形，边框为分段线条。
    /// </summary>
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

    /// <summary>
    /// 显示详情弹窗（鼠标悬停 500ms 后触发）。
    /// 使用 CancellationTokenSource 实现延迟取消：如果 500ms 内鼠标移开，
    /// MouseLeave 调用 HideDetailPopup 取消 Token，弹窗不会显示。
    /// </summary>
    private async void ShowDetailPopup(PresetItem preset, FrameworkElement target)
    {
        if (_detailPopup == null) return;

        // 取消上一次可能还未完成的延迟任务
        _popupDelayCts?.Cancel();
        _popupDelayCts = new CancellationTokenSource();
        var token = _popupDelayCts.Token;

        // 先关闭当前可能处于打开状态的弹窗
        if (_detailPopup.IsOpen)
            _detailPopup.IsOpen = false;

        try
        {
            // 延迟 500ms：鼠标悬停 500ms 后才显示详情弹窗，避免快速划过时闪烁
            await Task.Delay(500, token);
        }
        catch (TaskCanceledException)
        {
            // 如果在 500ms 内触发了 MouseLeave，任务被取消，直接退出
            return;
        }

        // 更新内容数据（放到延迟之后执行，节省性能）
        _detailNameText!.Text = preset.Name;
        _detailGamesPanel!.Children.Clear();
        foreach (var game in preset.Games)
            _detailGamesPanel.Children.Add(CreateGameTag(game));

        // 根据实际内容重新计算弹窗的切角多边形和边框几何
        UpdateDetailGeometries();

        // 更新弹窗要挂载的目标位置
        _detailPopup.PlacementTarget = target;

        // 使用 Dispatcher 将”打开”动作推迟到 Render 优先级，确保布局已完成
        _=Dispatcher.BeginInvoke(new Action(() =>
        {
            // 再次检查：确保等待期间目标没有被清空，且任务未被取消
            if (_detailPopup.PlacementTarget == target && !token.IsCancellationRequested)
            {
                _detailPopup.IsOpen = true;
            }
        }), System.Windows.Threading.DispatcherPriority.Render);
    }

    /// <summary>
    /// 隐藏详情弹窗，同时取消正在等待中的延迟显示任务。
    /// </summary>
    private void HideDetailPopup()
    {
        // 鼠标移开时，立刻取消延迟显示的计时器任务
        _popupDelayCts?.Cancel();

        if (_detailPopup != null)
        {
            _detailPopup.IsOpen = false;

            // 鼠标离开时必须清空目标，防止残留引用
            _detailPopup.PlacementTarget = null;
        }
    }

    /// <summary>
    /// 动态更新详情弹窗的切角多边形和边框几何。
    /// 通过强制 Measure 获取内容的期望尺寸，然后重新绘制背景多边形和边框线条，
    /// 使弹窗大小精确匹配内容。
    /// </summary>
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

    /// <summary>创建游戏标签 UI 控件（切角平行四边形背景 + 游戏名称文本）</summary>
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

    /// <summary>点击遮罩背景时关闭弹窗（仅当点击源为遮罩层本身，防止事件冒泡误关闭）</summary>
    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == OverlayBackground)
            Hide();
    }

    /// <summary>ComboBox 选中项变化时，从选中文本中提取游戏简称作为筛选条件</summary>
    private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = CategoryComboBox.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(selected))
        {
            _currentCategory = LocalizationService.Instance["Preset.All"];
            return;
        }
        // 从 "Assetto Corsa Competizione (ACC)" 中提取括号内的简称 "ACC"
        var match = System.Text.RegularExpressions.Regex.Match(selected, @"\(([^)]+)\)");
        _currentCategory = match.Success ? match.Groups[1].Value : selected;
    }

    /// <summary>点击筛选按钮，重新渲染预设列表</summary>
    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        RenderPresetList();
    }

    /// <summary>点击取消按钮，重置游戏筛选为"全部"</summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _currentCategory = LocalizationService.Instance["Preset.All"];
        CategoryComboBox.SelectedIndex = -1;
        ShowContentSiteOrWatermark();
        RenderPresetList();
    }

    /// <summary>点击应用按钮，触发 PresetApplied 事件并关闭弹窗</summary>
    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPreset = GetSelectedPreset();
        if (selectedPreset != null)
            PresetApplied?.Invoke(this, selectedPreset);
        Hide();
    }

    /// <summary>点击导入按钮，打开文件对话框选择 JSON 预设文件导入</summary>
    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationService.Instance["Preset.ImportPreset"],
            Filter = "预设文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dlg.ShowDialog() == true)
        {
            var imported = App.PresetService?.ImportPreset(dlg.FileName);
            if (imported != null)
            {
                imported.DeviceType = DeviceType;
                _personalPresets.Add(imported);
                SavePersonalPresets();
                RenderPresetList();
            }
            else
            {
                MessageBox.Show(LocalizationService.Instance["Preset.ImportFailedMessage"], LocalizationService.Instance["Preset.ImportFailed"],
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ══════════════════════════════════════════
    //  公开方法
    // ══════════════════════════════════════════

    /// <summary>从外部刷新个人预设列表数据</summary>
    public void RefreshPersonalPresets(List<PresetItem> presets)
    {
        _personalPresets.Clear();
        _personalPresets.AddRange(presets);
        if (!_isOfficialTab) RenderPresetList();
    }

    /// <summary>从外部设置官方预设列表数据</summary>
    public void SetOfficialPresets(IEnumerable<PresetItem> presets)
    {
        _officialPresets.Clear();
        _officialPresets.AddRange(presets);
        if (_isOfficialTab) RenderPresetList();
    }

    /// <summary>从外部设置个人预设列表数据</summary>
    public void SetPersonalPresets(IEnumerable<PresetItem> presets)
    {
        _personalPresets.Clear();
        _personalPresets.AddRange(presets);
        if (!_isOfficialTab) RenderPresetList();
    }

    /// <summary>从外部添加一条个人预设</summary>
    public void AddPersonalPreset(PresetItem preset)
    {
        _personalPresets.Add(preset);
        if (!_isOfficialTab) RenderPresetList();
    }

    /// <summary>从外部移除一条个人预设</summary>
    public void RemovePersonalPreset(PresetItem preset)
    {
        _personalPresets.Remove(preset);
        if (!_isOfficialTab) RenderPresetList();
    }

    /// <summary>
    /// 按名称查找并选中预设（供外部如 GameUserControl 调用）。
    /// 先查个人预设再查官方预设，找到后滚动到可见位置并高亮。
    /// 若控件尚未完成初始化，则延迟到 Loaded 之后再执行。
    /// </summary>
    /// <param name="presetName">预设名称</param>
    public void SelectAndScrollToPreset(string presetName)
    {
        if (!_isInitialized)
        {
            // 控件尚未加载完成，暂存预设名并等 Loaded 后再执行
            _pendingSelectName = presetName;
            Loaded += DelayedSelect;
            return;
        }

        DoSelectAndScrollToPreset(presetName);
    }

    private void DelayedSelect(object? sender, RoutedEventArgs e)
    {
        Loaded -= DelayedSelect;
        // 此时 _isInitialized 为 true，但 Loaded 里刚调完 RenderPresetList，
        // 需要再推迟一帧等模板应用完毕
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            if (_pendingSelectName != null)
            {
                DoSelectAndScrollToPreset(_pendingSelectName);
                _pendingSelectName = null;
            }
        });
    }

    private void DoSelectAndScrollToPreset(string presetName)
    {
        // 先在个人预设中查找，再查官方预设
        var preset = _personalPresets.FirstOrDefault(p =>
            string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase));
        var isPersonal = true;

        if (preset == null)
        {
            preset = _officialPresets.FirstOrDefault(p =>
                string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase));
            isPersonal = false;
        }

        if (preset == null) return;

        // 切换到对应的 Tab
        if (isPersonal && _isOfficialTab)
        {
            _isOfficialTab = false;
            UpdateTabVisuals();
            UpdateBottomButtons();
            RenderPresetList();
        }
        else if (!isPersonal && !_isOfficialTab)
        {
            _isOfficialTab = true;
            UpdateTabVisuals();
            UpdateBottomButtons();
            RenderPresetList();
        }

        // 从 Items 中直接定位匹配的 ContentControl（Tag 存了 PresetItem）
        ContentControl? targetControl = null;
        foreach (var itemObj in PresetItemsControl.Items)
        {
            if (itemObj is ContentControl cc && cc.Tag is PresetItem p &&
                string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase))
            {
                targetControl = cc;
                break;
            }
        }

        if (targetControl == null) return;

        // 推迟到布局渲染完成后选中，确保模板已应用、BgRect 可被查找
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            SelectPresetItem(preset, targetControl);
            targetControl.BringIntoView();
        });
    }

    // ══════════════════════════════════════════
    //  预设选中 / 应用 / 删除 / 导出
    // ══════════════════════════════════════════

    /// <summary>将当前个人预设列表持久化保存到文件</summary>
    private void SavePersonalPresets()
    {
        App.PresetService?.SavePersonalPresets(_personalPresets, DeviceType);
    }

    /// <summary>获取当前选中的预设项</summary>
    private PresetItem? GetSelectedPreset()
    {
        return _selectedPreset;
    }

    /// <summary>选中一个预设项，取消前一个选中并更新视觉效果</summary>
    private void SelectPresetItem(PresetItem preset, ContentControl control)
    {
        // 取消上一个选中项的高亮
        DeselectCurrentItem();

        _selectedPreset = preset;
        _selectedControl = control;

        // 应用选中视觉效果：红色描边 + 渐变填充背景
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

    /// <summary>取消当前选中项，清除视觉效果</summary>
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

    /// <summary>应用选中的预设（双击触发），触发 PresetApplied 事件并关闭弹窗</summary>
    private void ApplySelectedPreset()
    {
        if (_selectedPreset != null)
            PresetApplied?.Invoke(this, _selectedPreset);
        Hide();
    }

    /// <summary>显示删除确认对话框（仅个人预设可用）</summary>
    private void ShowDeleteConfirmDialog(PresetItem preset)
    {
        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = LocalizationService.Instance["Preset.DeletePreset"];
        dialog.ClearButtons();

        dialog.DialogContent = new TextBlock
        {
            Text = LocalizationService.Instance["Preset.DeleteConfirmMessage"],
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        dialog.AddButton(LocalizationService.Instance["Common.Delete"], (_, _) =>
        {
            dialog.Hide();
            _personalPresets.Remove(preset);
            SavePersonalPresets();
            RenderPresetList();
        }, isPrimary: true);

        dialog.AddButton(LocalizationService.Instance["Common.Cancel"], (_, _) =>
        {
            dialog.Hide();
        }, isPrimary: false);

        dialog.Show();
    }

    /// <summary>
    /// 导出预设到文件。打开保存文件对话框选择目标路径，
    /// 导出失败时会弹出重试对话框。
    /// </summary>
    private void ExportPreset(PresetItem preset)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = LocalizationService.Instance["Preset.ExportPreset"],
            Filter = "预设文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = ".json",
            FileName = preset.Name
        };

        if (dlg.ShowDialog() != true || App.PresetService == null) return;

        var fileName = dlg.FileName;
        TryExportPresetWithRetry(preset, fileName);
    }

    /// <summary>尝试导出预设，成功显示 Toast 提示，失败显示重试对话框</summary>
    private void TryExportPresetWithRetry(PresetItem preset, string fileName)
    {
        if (PerformExportPreset(preset, fileName))
        {
            ShowExportSuccessToast(LocalizationService.Instance["Preset.ExportSuccess"]);
            return;
        }

        ShowExportFailedDialog(() => TryExportPresetWithRetry(preset, fileName));
    }

    /// <summary>执行预设导出到文件的实际操作</summary>
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

    /// <summary>显示导出成功的 Toast 提示（1 秒后自动消失）</summary>
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

    /// <summary>显示导出失败对话框，提供重试和取消选项</summary>
    private void ShowExportFailedDialog(Action? onRetry)
    {
        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = LocalizationService.Instance["Preset.ExportFailed"];
        dialog.ShowIcon = true;
        dialog.ClearButtons();

        dialog.DialogContent = new TextBlock
        {
            Text = LocalizationService.Instance["Preset.ExportFailedMessage"],
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        dialog.AddButton(LocalizationService.Instance["Common.Retry"], (_, _) =>
        {
            dialog.Hide();
            onRetry?.Invoke();
        }, isPrimary: true);

        dialog.AddButton(LocalizationService.Instance["Common.Cancel"], (_, _) =>
        {
            dialog.Hide();
        }, isPrimary: false);

        dialog.Show();
    }

    /// <summary>Accent 按钮尺寸变化时重新绘制切角背景路径</summary>
    private void AccentButton_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid) return;
        var w = grid.ActualWidth;
        var h = grid.ActualHeight;
        if (w <= 0 || h <= 0) return;
        if (grid.FindName("BtnPath") is not Path path) return;
        path.Width = w;
        path.Data = Geometry.Parse($"M{w},0 H9 L0,9 V{h} H{w - 9} L{w},{h - 9} V0 Z");
    }

    /// <summary>操作按钮尺寸变化时重新绘制切角背景路径</summary>
    private void ActionButton_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid) return;
        var w = grid.ActualWidth;
        var h = grid.ActualHeight;
        if (w <= 0 || h <= 0) return;
        if (grid.FindName("BtnPath") is not Path path) return;
        path.Width = w;
        path.Data = Geometry.Parse($"M{w},0 H9 L0,9 V{h} H{w - 9} L{w},{h - 9} V0 Z");
    }

    /// <summary>ComboBox 尺寸变化时重新绘制切角背景路径</summary>
    private void ComboBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid) return;
        var w = grid.ActualWidth;
        var h = grid.ActualHeight;
        if (w <= 0 || h <= 0) return;
        if (grid.FindName("ComboBoxBg") is not Path path) return;
        path.Width = w;
        path.Height = h;
        // 切角偏移 9px（与原始形状 M325...M5... 倾斜一致）
        path.Data = Geometry.Parse($"M{w},0 H9 L0,9 V{h} H{w - 9} L{w},{h - 9} V0 Z");
    }

}

/// <summary>
/// 预设项数据模型。
/// 包含预设的名称、描述、分类、关联游戏列表、参数快照等元数据，
/// 用于在 PresetListPopup 中展示和操作预设。
/// </summary>
public class PresetItem
{
    /// <summary>预设名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>关联的游戏列表（游戏简称）</summary>
    public List<string> Games { get; set; } = new();

    /// <summary>是否为个人预设（可编辑/删除），官方预设不可编辑</summary>
    public bool IsPersonal { get; set; }

    /// <summary>设备类型（基座/踏板/面盘/排挡等）</summary>
    public Models.Usb.DeviceType DeviceType { get; set; } = Models.Usb.DeviceType.Pedal;

    /// <summary>基座预设参数快照</summary>
    public Models.Usb.BasePresetSnapshot? BaseParameters { get; set; }

    /// <summary>面盘预设参数快照</summary>
    public Models.Usb.WheelPresetSnapshot? WheelParameters { get; set; }

    /// <summary>踏板预设参数快照</summary>
    public Models.Usb.PedalPresetSnapshot? PedalParameters { get; set; }
}
