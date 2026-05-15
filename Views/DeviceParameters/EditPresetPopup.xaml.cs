using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace HITAPEX.Views.DeviceParameters;

public partial class EditPresetPopup : UserControl
{
    private static readonly List<string> s_allGames =
    [
        "ACC", "AC EVO", "DR2.0", "EA WRC", "F1", "FH5", "FM",
        "GT4", "GTWC", "IGTC", "iR", "LMU"
    ];

    private static readonly Dictionary<string, string> s_gameFullNames = new()
    {
        ["ACC"] = "Assetto Corsa Competizione",
        ["AC EVO"] = "Assetto Corsa Evo",
        ["DR2.0"] = "Dirt Rally 2.0",
        ["EA WRC"] = "EA Sports WRC",
        ["F1"] = "F1 24",
        ["FH5"] = "Forza Horizon 5",
        ["FM"] = "Forza Motorsport",
        ["GT4"] = "GT4 European Series",
        ["GTWC"] = "GT World Challenge",
        ["IGTC"] = "Intercontinental GT Challenge",
        ["iR"] = "iRacing",
        ["LMU"] = "Le Mans Ultimate"
    };

    private static string GetGameDisplayName(string abbreviation)
    {
        return s_gameFullNames.TryGetValue(abbreviation, out var fullName)
            ? $"{fullName} ({abbreviation})"
            : abbreviation;
    }

    private readonly HashSet<string> _selectedGames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<char, FrameworkElement> _groupHeaders = new();
    private readonly List<Button> _letterButtons = new();
    private List<string> _existingNames = [];
    private PresetItem _originalPreset = new();
    private Button? _highlightedLetter;
    private bool _suppressScrollSync;

    public event EventHandler<PresetItem>? EditConfirmed;
    public event EventHandler? EditCancelled;

    public EditPresetPopup()
    {
        InitializeComponent();
        BuildLetterIndex();
    }

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
        UpdateCharCount();
        UpdateWatermark();
        BuildAllGamesList();
        BuildSelectedGamesList();
        UpdateSelectionSummary();
    }

    // ══════════════════════════════════════════
    //  构建"所有游戏"列表（按首字母分组）
    // ══════════════════════════════════════════

    private void BuildAllGamesList()
    {
        AllGamesPanel.Children.Clear();
        _groupHeaders.Clear();

        var grouped = s_allGames
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .GroupBy(g => char.ToUpper(g[0]))
            .ToList();

        foreach (var group in grouped)
        {
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

    private void BuildSelectedGamesList()
    {
        SelectedGamesPanel.Children.Clear();

        if (_selectedGames.Count == 0)
        {
            var emptyHint = new TextBlock
            {
                Text = "暂未选择游戏",
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

            BuildSelectedGamesList();
            UpdateSelectionSummary();
        }
    }

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

    private void UpdateSelectionSummary()
    {
        GameCountText.Text = $"游戏（{_selectedGames.Count}/{s_allGames.Count}）";

        SelectAllCheckBox.IsChecked = _selectedGames.Count == s_allGames.Count
            ? true
            : _selectedGames.Count == 0
                ? false
                : null;
    }

    // ══════════════════════════════════════════
    //  A-Z 字母索引
    // ══════════════════════════════════════════

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

    private void ScrollToLetter(char letter)
    {
        if (_groupHeaders.TryGetValue(letter, out var header))
        {
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

    private void AllGamesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_suppressScrollSync) return;

        // Find the first visible group header
        var scrollViewer = (ScrollViewer)sender;
        double viewportTop = scrollViewer.VerticalOffset;

        char? firstVisible = null;
        foreach (var kvp in _groupHeaders)
        {
            var transform = kvp.Value.TransformToVisual(AllGamesPanel);
            var pos = transform.Transform(new Point(0, 0));
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

    private void PresetNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateCharCount();
        UpdateWatermark();
        DuplicateNameText.Visibility = Visibility.Collapsed;
        ConfirmButton.IsEnabled = true;
    }

    private void PresetNameTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // 拼音输入过程中不限制字符数，待确认输入后由 TextChanged 统一校验
        if (InputMethod.Current.ImeState == InputMethodState.On)
            return;

        if (PresetNameTextBox.Text.Length >= 20)
            e.Handled = true;
    }

    private void PresetNameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 放行编辑键和导航键
        if (e.Key is Key.Back or Key.Delete or Key.Left or Key.Right or Key.Tab
            or Key.Enter or Key.Home or Key.End)
            return;

        // IME 进程中放行所有按键，确保拼音输入不被打断
        if (e.Key == Key.ImeProcessed || InputMethod.Current.ImeState == InputMethodState.On)
            return;

        if (PresetNameTextBox.Text.Length >= 20)
            e.Handled = true;
    }

    private void UpdateCharCount()
    {
        var count = PresetNameTextBox.Text.Length;
        CharCountText.Text = $"{count}/20";
    }

    private void UpdateWatermark()
    {
        WatermarkText.Visibility = string.IsNullOrEmpty(PresetNameTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ClearNameButton_Click(object sender, RoutedEventArgs e)
    {
        PresetNameTextBox.Text = string.Empty;
        PresetNameTextBox.Focus();
    }

    // ══════════════════════════════════════════
    //  操作按钮
    // ══════════════════════════════════════════

    private void ClearAllSelected_Click(object sender, RoutedEventArgs e)
    {
        _selectedGames.Clear();
        BuildAllGamesList();
        BuildSelectedGamesList();
        UpdateSelectionSummary();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var name = PresetNameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        if (_existingNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
        {
            DuplicateNameText.Visibility = Visibility.Visible;
            ConfirmButton.IsEnabled = false;
            return;
        }

        var edited = new PresetItem
        {
            Name = name,
            Description = _originalPreset.Description,
            Category = _originalPreset.Category,
            ItemCount = _originalPreset.ItemCount,
            Games = _selectedGames.OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToList()
        };

        EditConfirmed?.Invoke(this, edited);
        Hide();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        EditCancelled?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == OverlayBackground)
            CancelButton_Click(sender, e);
    }

    // ══════════════════════════════════════════
    //  显示 / 隐藏 + 动画
    // ══════════════════════════════════════════

    public void Show()
    {
        Visibility = Visibility.Visible;
        AnimateIn();
        PresetNameTextBox.Focus();
        PresetNameTextBox.CaretIndex = PresetNameTextBox.Text.Length;
    }

    public void Hide()
    {
        AnimateOut(() => Visibility = Visibility.Collapsed);
    }

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
