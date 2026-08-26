using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using HITAPEX.Models;
using HITAPEX.Services;

namespace HITAPEX.Views.DeviceParameters;

/// <summary>
/// 预设编辑弹出控件 —— 用于编辑预设名称、选择关联游戏（支持 A-Z 字母索引快速导航），
/// 并可确认保存或取消编辑。
/// </summary>
public partial class EditPresetPopup : UserControl
{
    // ══════════════════════════════════════════
    //  静态游戏数据（游戏缩写列表 + 全称映射 + 显示名生成）
    // ══════════════════════════════════════════

    /// <summary>所有游戏的缩写列表，从 GameListConfig 动态生成</summary>
    private static readonly List<string> s_allGames = Models.GameListConfig.GetGames()
        .Select(g => g.Abbreviation)
        .Where(a => !string.IsNullOrEmpty(a))
        .ToList();

    /// <summary>游戏缩写 -> 完整名称的映射表，从 GameListConfig 动态生成</summary>
    private static readonly Dictionary<string, string> s_gameFullNames = Models.GameListConfig.GetGames()
        .Where(g => !string.IsNullOrEmpty(g.Abbreviation))
        .ToDictionary(g => g.Abbreviation, g => g.Name);

    /// <summary>获取游戏的显示名称：全称 (缩写) 格式；若无全称则回退到缩写</summary>
    private static string GetGameDisplayName(string abbreviation)
    {
        return s_gameFullNames.TryGetValue(abbreviation, out var fullName)
            ? $"{fullName} ({abbreviation})"
            : abbreviation;
    }

    // ══════════════════════════════════════════
    //  实例字段
    // ══════════════════════════════════════════

    /// <summary>当前已选中的游戏集合（忽略大小写）</summary>
    private readonly HashSet<string> _selectedGames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>首字母分组标题元素映射（letter -> 标题控件），用于滚动同步与快速跳转</summary>
    private readonly Dictionary<char, FrameworkElement> _groupHeaders = new();

    /// <summary>右侧 A-Z 字母索引按钮列表</summary>
    private readonly List<Button> _letterButtons = new();

    /// <summary>已有的预设名称列表，用于重名检测</summary>
    private List<string> _existingNames = [];

    /// <summary>正在编辑的原始预设数据</summary>
    private PresetItem _originalPreset = new();

    /// <summary>当前高亮的字母索引按钮</summary>
    private Button? _highlightedLetter;

    /// <summary>抑制滚动同步的标志：当字母索引点击触发 ScrollToLetter 时设为 true，避免循环触发</summary>
    private bool _suppressScrollSync;

    /// <summary>当前编辑弹窗对应的设备类型，决定保存预设时的 DeviceType 字段</summary>
    public Models.Usb.DeviceType DeviceType { get; set; } = Models.Usb.DeviceType.Pedal;

    /// <summary>编辑确认事件 —— 用户点击确认按钮且校验通过后触发</summary>
    public event EventHandler<PresetItem>? EditConfirmed;

    /// <summary>编辑取消事件 —— 用户点击取消按钮或弹窗关闭时触发</summary>
    public event EventHandler? EditCancelled;

    /// <summary>构造函数：初始化组件并构建右侧 A-Z 字母索引栏</summary>
    public EditPresetPopup()
    {
        InitializeComponent();
        BuildLetterIndex();
    }

    /// <summary>设置弹窗标题文本</summary>
    /// <param name="title">标题字符串</param>
    public void SetTitle(string title)
    {
        TitleText.Text = title;
    }

    /// <summary>进入编辑模式：加载已有预设数据（名称、游戏列表），构建两侧游戏面板</summary>
    /// <param name="preset">待编辑的预设对象</param>
    /// <param name="existingNames">除自身外已有的预设名称列表，用于重名校验</param>
    public void BeginEdit(PresetItem preset, IEnumerable<string> existingNames)
    {
        _originalPreset = preset;
        _existingNames = existingNames
            .Where(n => !string.Equals(n, preset.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _selectedGames.Clear();
        foreach (var g in preset.Games)
            _selectedGames.Add(g);

        PresetNameTextBox.Text = preset.Name;
        SyncToCloudCheckBox.IsChecked = preset.SyncToCloud;
        UpdateCharCount();
        UpdateWatermark();
        BuildAllGamesList();
        BuildSelectedGamesList();
        UpdateSelectionSummary();
        SetTitle(LocalizationService.Instance["Preset.Edit"]);
    }

    /// <summary>进入“另存为”模式：空白名称，给定已有名称列表用于重名校验</summary>
    /// <param name="existingNames">已有的预设名称列表</param>
    public void BeginSaveAs(IEnumerable<string> existingNames)
    {
        _originalPreset = new PresetItem();
        _existingNames = existingNames.ToList();

        _selectedGames.Clear();
        PresetNameTextBox.Text = string.Empty;
        SyncToCloudCheckBox.IsChecked = false;
        UpdateCharCount();
        UpdateWatermark();
        BuildAllGamesList();
        BuildSelectedGamesList();
        UpdateSelectionSummary();
        SetTitle(LocalizationService.Instance["Preset.SaveAsTitle"]);
    }

    // ══════════════════════════════════════════
    //  构建"所有游戏"列表（按首字母分组）
    // ══════════════════════════════════════════

    private void BuildAllGamesList()
    {
        AllGamesPanel.Children.Clear();
        _groupHeaders.Clear();

        // 按游戏名称首字母分组（忽略大小写），每组内部按字母序排列
        var grouped = s_allGames
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .GroupBy(g => char.ToUpper(g[0]))
            .ToList();

        foreach (var group in grouped)
        {
            // 创建该首字母的分组标题并记录到字典中，供字母索引跳转使用
            var header = CreateGroupHeader(group.Key);
            _groupHeaders[group.Key] = header;
            AllGamesPanel.Children.Add(header);

            foreach (var game in group)
            {
                var isSelected = _selectedGames.Contains(game);
                AllGamesPanel.Children.Add(CreateAllGameItem(game, isSelected));
            }
        }
    }

    /// <summary>创建首字母分组标题控件（A-Z 字母标签）</summary>
    private FrameworkElement CreateGroupHeader(char letter)
    {
        var border = new Border
        {
            Height = 23,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(0, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromArgb(0x00, 0xEE, 0xEE, 0xEE)),
            Child = new TextBlock
            {
                Text = letter.ToString(),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE)),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                FontFamily = (FontFamily)FindResource("OrbitronFont"),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        return border;
    }

    /// <summary>创建“所有游戏”列表中的单个游戏复选框项</summary>
    private FrameworkElement CreateAllGameItem(string gameName, bool isSelected)
    {
        var checkBox = new CheckBox
        {
            Content = new TextBlock
            {
                Text = GetGameDisplayName(gameName),
                MaxWidth = 277,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = (FontFamily)FindResource("OrbitronFont")
            },
            Style = (Style)FindResource("SelectCheckBoxStyle"),
            IsChecked = isSelected,
            Tag = gameName,
            MinHeight = 20,
            Margin = new Thickness(0, 0, 0, 10)
        };
        checkBox.Checked += GameCheckBox_Changed;
        checkBox.Unchecked += GameCheckBox_Changed;
        return checkBox;
    }

    // ══════════════════════════════════════════
    //  构建"已选游戏"列表
    // ══════════════════════════════════════════

    /// <summary>构建“已选游戏”列表：若无选中则显示空提示，否则按字母序排列已选游戏</summary>
    private void BuildSelectedGamesList()
    {
        SelectedGamesPanel.Children.Clear();

        if (_selectedGames.Count == 0)
        {
            var emptyHint = new TextBlock
            {
                Text = LocalizationService.Instance["Preset.NoGameSelected"],
                Foreground = new SolidColorBrush(Color.FromArgb(0x44, 0xEE, 0xEE, 0xEE)),
                FontSize = 14,
                FontFamily = (FontFamily)FindResource("OrbitronFont"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 0)
            };
            SelectedGamesPanel.Children.Add(emptyHint);
            return;
        }

        foreach (var game in _selectedGames.OrderBy(g => g, StringComparer.OrdinalIgnoreCase))
        {
            SelectedGamesPanel.Children.Add(CreateSelectedGameItem(game));
        }
    }

    /// <summary>创建“已选游戏”列表中的单项：显示游戏全称、装饰图标和移除按钮</summary>
    private FrameworkElement CreateSelectedGameItem(string gameName)
    {
        var accentGradient = (Brush)FindResource("AccentGradient");
        var shadow = (Effect)FindResource("TitleIconShadow");

        var row = new Grid
        {
            MinHeight = 20,
            Margin = new Thickness(0, 0, 0, 10)
        };

        // Left accent icon
        var accentIcon = new Grid
        {
            Width = 6,
            Effect = shadow,
            Opacity = 0.9,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        accentIcon.Children.Add(new Path
        {
            Data = Geometry.Parse("M0 2L2 0H4L0 4V2Z M8 0H6L0 6V26H5L8 23V0Z"),
            Fill = accentGradient,
            Stretch = Stretch.Fill,
            Width = 6,
            Height = 20
        });
        row.Children.Add(accentIcon);

        var nameText = new TextBlock
        {
            Text = GetGameDisplayName(gameName),
            MaxWidth = 286,
            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE)),
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            FontFamily = (FontFamily)FindResource("OrbitronFont"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment= HorizontalAlignment.Left,
            Margin = new Thickness(11, 0, 0, 0)
        };
        row.Children.Add(nameText);

        // Remove button with X-circle icon
        var removeBtn = new Button
        {
            Style = (Style)FindResource("TextButtonStyle"),
            FontSize = 16,
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(312, 0, 0, 0),
            Tag = gameName
        };
        removeBtn.Click += RemoveGame_Click;

        var removeIcon = new Viewbox { Width = 16, Height = 16 };
        var iconGrid = new Grid { Width = 16, Height = 16 };
        var strokeColor = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE));
        iconGrid.Children.Add(new Path
        {
            Data = Geometry.Parse("M10.031 5.4668L5.46484 10.033"),
            Stroke = strokeColor, StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        });
        iconGrid.Children.Add(new Path
        {
            Data = Geometry.Parse("M5.46484 5.4668L10.031 10.033"),
            Stroke = strokeColor, StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        });
        iconGrid.Children.Add(new Path
        {
            Data = Geometry.Parse("M7.75 14.75C11.616 14.75 14.75 11.616 14.75 7.75C14.75 3.88401 11.616 0.75 7.75 0.75C3.88401 0.75 0.75 3.88401 0.75 7.75C0.75 11.616 3.88401 14.75 7.75 14.75Z"),
            Stroke = strokeColor, StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        });
        removeIcon.Child = iconGrid;
        removeBtn.Content = removeIcon;
        row.Children.Add(removeBtn);

        return row;
    }

    private void RemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string gameName)
        {
            _selectedGames.Remove(gameName);
            // 刷新两侧列表和统计信息
            BuildAllGamesList();
            BuildSelectedGamesList();
            UpdateSelectionSummary();
        }
    }

    private void GameCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is string gameName)
        {
            if (cb.IsChecked == true)
                _selectedGames.Add(gameName);
            else
                _selectedGames.Remove(gameName);

            // 复选框状态变更时即时刷新已选列表
            BuildSelectedGamesList();
            UpdateSelectionSummary();
        }
    }

    /// <summary>全选 / 取消全选：如果当前未全选则全选，否则清空</summary>
    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        var selectAll = _selectedGames.Count < s_allGames.Count;

        _selectedGames.Clear();
        if (selectAll)
        {
            foreach (var g in s_allGames)
                _selectedGames.Add(g);
        }

        BuildAllGamesList();
        BuildSelectedGamesList();
        UpdateSelectionSummary();
    }

    /// <summary>更新已选游戏计数文本和全选复选框的三态状态</summary>
    private void UpdateSelectionSummary()
    {
        GameCountText.Text = LocalizationService.Instance.Format("Preset.GameCount", _selectedGames.Count, s_allGames.Count);

        // 三态复选框：全选 -> true，全不选 -> false，部分选 -> null（中间态）
        SelectAllCheckBox.IsChecked = _selectedGames.Count == s_allGames.Count
            ? true
            : _selectedGames.Count == 0
                ? false
                : null;
    }

    // ══════════════════════════════════════════
    //  A-Z 字母索引
    // ══════════════════════════════════════════

    /// <summary>构建右侧 A-Z 字母索引按钮面板，每个字母一个按钮</summary>
    private void BuildLetterIndex()
    {
        LetterIndexPanel.Children.Clear();
        _letterButtons.Clear();

        for (char c = 'A'; c <= 'Z'; c++)
        {
            var btn = new Button
            {
                Content = c.ToString(),
                Style = (Style)FindResource("LetterIndexButtonStyle"),
                Tag = c
            };
            btn.Click += LetterIndex_Click;
            LetterIndexPanel.Children.Add(btn);
            _letterButtons.Add(btn);
        }
    }

    private void LetterIndex_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is char letter)
            ScrollToLetter(letter);
    }

    /// <summary>滚动到指定首字母的分组标题位置，并高亮对应字母按钮</summary>
    /// <param name="letter">目标首字母</param>
    private void ScrollToLetter(char letter)
    {
        if (_groupHeaders.TryGetValue(letter, out var header))
        {
            // 设置标志抑制滚动事件处理器中的高亮同步，避免循环触发
            _suppressScrollSync = true;

            // 计算 header 在面板中的位置并滚动到列表顶部
            var transform = header.TransformToVisual(AllGamesPanel);
            var position = transform.Transform(new Point(0, 0));
            AllGamesScrollViewer.ScrollToVerticalOffset(position.Y);

            HighlightLetterButton(letter);

            // Delay resetting the flag to allow scroll events to fire
            Dispatcher.BeginInvoke(new Action(() => _suppressScrollSync = false),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    /// <summary>高亮指定字母的索引按钮（红色），同时取消上一个高亮</summary>
    private void HighlightLetterButton(char letter)
    {
        if (_highlightedLetter != null)
            _highlightedLetter.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE));

        var btn = _letterButtons.FirstOrDefault(b => b.Tag is char c && c == letter);
        if (btn != null)
        {
            btn.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xC6, 0x0E, 0x0E));
            _highlightedLetter = btn;
        }
    }

    /// <summary>
    /// 滚动事件处理器：当用户手动滚动游戏列表时，自动同步高亮当前可视区域
    /// 中第一个出现的首字母分组所对应的字母索引按钮。
    /// 通过查找第一个 Y 坐标 >= 当前滚动偏移量（留 4px 容差）的分组标题来实现。
    /// </summary>
    private void AllGamesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // 若是字母索引点击触发的滚动（ScrollToLetter），则跳过本次同步避免循环
        if (_suppressScrollSync) return;

        var scrollViewer = (ScrollViewer)sender;
        double viewportTop = scrollViewer.VerticalOffset;

        // 查找当前可视区域中第一个分组标题字母
        char? firstVisible = null;
        foreach (var kvp in _groupHeaders)
        {
            var transform = kvp.Value.TransformToVisual(AllGamesPanel);
            var pos = transform.Transform(new Point(0, 0));
            // 4px 容差确保滚动到接近该标题时即触发高亮
            if (pos.Y >= viewportTop - 4)
            {
                firstVisible = kvp.Key;
                break;
            }
        }

        if (firstVisible.HasValue)
            HighlightLetterButton(firstVisible.Value);
    }

    // ══════════════════════════════════════════
    //  预设名称输入处理
    // ══════════════════════════════════════════

    /// <summary>名称输入框文本变更事件：更新字符计数、水印可见性和校验状态</summary>
    private void PresetNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateCharCount();
        UpdateWatermark();
        // 每次文本变更时清除之前的重名提示，允许重新提交
        DuplicateNameText.Visibility = Visibility.Collapsed;
        ConfirmButton.IsEnabled = true;
    }

    /// <summary>
    /// 文本输入预览事件：限制名称最大 20 字符。
    /// 拼音输入过程中放行所有字符（由 TextChanged 最终校验），避免打断中文输入。
    /// </summary>
    private void PresetNameTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // 拼音输入过程中不限制字符数，待确认输入后由 TextChanged 统一校验
        if (InputMethod.Current.ImeState == InputMethodState.On)
            return;

        // 已达上限 20 字符时阻止进一步输入
        if (PresetNameTextBox.Text.Length >= 20)
            e.Handled = true;
    }

    /// <summary>
    /// 按键预览事件：限制名称最大 20 字符。
    /// 放行 Back/Delete/方向键/Tab/Enter/Home/End 等编辑键和导航键，
    /// 放行 IME 输入过程中的所有按键，其余按键在达到上限时拦截。
    /// </summary>
    private void PresetNameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 放行编辑键和导航键，确保用户可正常删除、移动光标
        if (e.Key is Key.Back or Key.Delete or Key.Left or Key.Right or Key.Tab
            or Key.Enter or Key.Home or Key.End)
            return;

        // IME 进程中放行所有按键，确保拼音输入不被打断
        if (e.Key == Key.ImeProcessed || InputMethod.Current.ImeState == InputMethodState.On)
            return;

        // 已达上限 20 字符时阻止进一步按键输入
        if (PresetNameTextBox.Text.Length >= 20)
            e.Handled = true;
    }

    /// <summary>更新字符计数显示，格式为 {当前}/{上限}（上限 20）</summary>
    private void UpdateCharCount()
    {
        var count = PresetNameTextBox.Text.Length;
        CharCountText.Text = $"{count}/20";
    }

    /// <summary>更新输入框中水印文本的可见性：有内容时隐藏，空时显示</summary>
    private void UpdateWatermark()
    {
        WatermarkText.Visibility = string.IsNullOrEmpty(PresetNameTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>清空名称输入框并聚焦，方便用户重新输入</summary>
    private void ClearNameButton_Click(object sender, RoutedEventArgs e)
    {
        PresetNameTextBox.Text = string.Empty;
        PresetNameTextBox.Focus();
    }

    // ══════════════════════════════════════════
    //  操作按钮（清空已选、确认、取消）
    // ══════════════════════════════════════════

    /// <summary>一键清空所有已选游戏</summary>
    private void ClearAllSelected_Click(object sender, RoutedEventArgs e)
    {
        _selectedGames.Clear();
        BuildAllGamesList();
        BuildSelectedGamesList();
        UpdateSelectionSummary();
    }

    /// <summary>
    /// 确认按钮点击：校验名称非空、无重名后，构造 PresetItem 并触发 EditConfirmed 事件。
    /// 若存在重名则显示提示文案并禁用确认按钮。
    /// </summary>
    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var name = PresetNameTextBox.Text.Trim();
        // 空白名称不允许提交
        if (string.IsNullOrEmpty(name)) return;

        // 重名校验（忽略大小写）
        if (_existingNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
        {
            DuplicateNameText.Visibility = Visibility.Visible;
            ConfirmButton.IsEnabled = false;
            return;
        }

        var edited = new PresetItem
        {
            Name = name,
            Games = _selectedGames.OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToList(),
            IsPersonal = _originalPreset.IsPersonal,
            PedalParameters = _originalPreset.PedalParameters,
            WheelParameters = _originalPreset.WheelParameters,
            BaseParameters = _originalPreset.BaseParameters,
            DeviceType = DeviceType,
            // 同步到云端：勾选则同步；CloudDocumentId 沿用原预设（编辑已同步预设时走更新，否则首次为新增）
            SyncToCloud = SyncToCloudCheckBox.IsChecked == true,
            CloudDocumentId = _originalPreset.CloudDocumentId
        };

        // 触发确认事件并关闭弹窗
        EditConfirmed?.Invoke(this, edited);
        Hide();
    }

    /// <summary>取消按钮点击：触发取消事件并关闭弹窗</summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        EditCancelled?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    // ══════════════════════════════════════════
    //  显示 / 隐藏 + 动画
    // ══════════════════════════════════════════

    /// <summary>显示弹窗：播放淡入+缩放动画，聚焦名称输入框并将光标移至末尾</summary>
    public void Show()
    {
        Visibility = Visibility.Visible;
        AnimateIn();
        // 聚焦输入框并移动光标到文本末尾，方便用户直接编辑
        PresetNameTextBox.Focus();
        PresetNameTextBox.CaretIndex = PresetNameTextBox.Text.Length;
    }

    /// <summary>隐藏弹窗：播放淡出+缩放动画，动画完成后设为 Collapsed</summary>
    public void Hide()
    {
        AnimateOut(() => Visibility = Visibility.Collapsed);
    }

    /// <summary>
    /// 弹窗进入动画：遮罩层淡入 + 面板淡入 + 从 0.94x 缩放到 1x。
    /// 动画期间禁用面板命中测试，动画结束后恢复正常。
    /// </summary>
    private void AnimateIn()
    {
        OverlayBackground.Opacity = 0;
        PopupPanel.Opacity = 0;
        PopupPanel.RenderTransform = new ScaleTransform(0.94, 0.94,
            PopupPanel.Width / 2, PopupPanel.Height / 2);
        PopupPanel.CacheMode = new BitmapCache();
        PopupPanel.IsHitTestVisible = false;

        DoubleAnimation overlayFade = new(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        DoubleAnimation panelFade = new(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        DoubleAnimation scaleX = new(0.94, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        DoubleAnimation scaleY = new(0.94, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // 动画完成后清除缓存模式，恢复命中测试
        scaleX.Completed += (_, _) =>
        {
            PopupPanel.CacheMode = null;
            PopupPanel.IsHitTestVisible = true;
        };

        OverlayBackground.BeginAnimation(OpacityProperty, overlayFade);
        PopupPanel.BeginAnimation(OpacityProperty, panelFade);
        PopupPanel.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        PopupPanel.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
    }

    /// <summary>
    /// 弹窗退出动画：遮罩层淡出 + 面板淡出 + 从 1x 缩小到 0.94x。
    /// 动画期间启用 BitmapCache 提升性能，完成后执行回调（设为 Collapsed）。
    /// </summary>
    /// <param name="onCompleted">动画完成后的回调，通常用于设置 Visibility = Collapsed</param>
    private void AnimateOut(Action onCompleted)
    {
        if (PopupPanel.RenderTransform is not ScaleTransform st)
            PopupPanel.RenderTransform = st = new ScaleTransform(1, 1,
                PopupPanel.Width / 2, PopupPanel.Height / 2);

        PopupPanel.CacheMode = new BitmapCache();
        PopupPanel.IsHitTestVisible = false;

        DoubleAnimation overlayFade = new(1, 0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        DoubleAnimation panelFade = new(1, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        DoubleAnimation scaleX = new(1, 0.94, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        DoubleAnimation scaleY = new(1, 0.94, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        panelFade.Completed += (_, _) =>
        {
            PopupPanel.CacheMode = null;
            onCompleted();
        };

        OverlayBackground.BeginAnimation(OpacityProperty, overlayFade);
        PopupPanel.BeginAnimation(OpacityProperty, panelFade);
        st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
    }
}
