using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HITAPEX.Controls;

/// <summary>
/// 模态对话框控件，支持标题、自定义内容、图标显示、关闭按钮等配置。
/// 通过依赖属性提供声明式配置能力，支持在 XAML 中直接绑定。
/// 提供 Show/Hide 方法控制显示状态，以及动态添加按钮的功能。
/// </summary>
public partial class ModalDialog : UserControl
{
    // ═══════════════════════════════════════════════════════════════
    // 依赖属性定义
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 对话框标题的依赖属性
    /// </summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ModalDialog),
            new PropertyMetadata(string.Empty, OnTitleChanged));

    /// <summary>
    /// 对话框内容区域的依赖属性，可接受任意 UI 元素或数据对象
    /// </summary>
    public static readonly DependencyProperty DialogContentProperty =
        DependencyProperty.Register(nameof(DialogContent), typeof(object), typeof(ModalDialog),
            new PropertyMetadata(null, OnDialogContentChanged));

    /// <summary>
    /// 是否显示标题图标（感叹号图标）的依赖属性
    /// </summary>
    public static readonly DependencyProperty ShowIconProperty =
        DependencyProperty.Register(nameof(ShowIcon), typeof(bool), typeof(ModalDialog),
            new PropertyMetadata(false, OnShowIconChanged));

    /// <summary>
    /// 是否显示关闭按钮（右上角 X）的依赖属性
    /// </summary>
    public static readonly DependencyProperty ShowCloseButtonProperty =
        DependencyProperty.Register(nameof(ShowCloseButton), typeof(bool), typeof(ModalDialog),
            new PropertyMetadata(false, OnShowCloseButtonChanged));

    // ═══════════════════════════════════════════════════════════════
    // 依赖属性 CLR 包装属性
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 对话框标题文本
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// 对话框内容区域的显示对象
    /// </summary>
    public object DialogContent
    {
        get => GetValue(DialogContentProperty);
        set => SetValue(DialogContentProperty, value);
    }

    /// <summary>
    /// 是否显示标题前的图标
    /// </summary>
    public bool ShowIcon
    {
        get => (bool)GetValue(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    /// <summary>
    /// 是否在标题栏右上角显示关闭按钮
    /// </summary>
    public bool ShowCloseButton
    {
        get => (bool)GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    // ═══════════════════════════════════════════════════════════════
    // 构造函数
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 初始化模态对话框，加载 XAML 模板中定义的视觉树
    /// </summary>
    public ModalDialog()
    {
        InitializeComponent();
    }

    // ═══════════════════════════════════════════════════════════════
    // 依赖属性变更回调
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 标题变更回调。当标题为空时自动折叠标题区域，
    /// 避免残留空白占位影响布局
    /// </summary>
    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModalDialog dialog)
        {
            var newTitle = e.NewValue as string;
            dialog.TitleText.Text = newTitle ?? string.Empty;

            // 关键修复：当标题为空时，彻底折叠 TitleText 及其父级 StackPanel 的高度占用
            if (string.IsNullOrWhiteSpace(newTitle))
            {
                dialog.TitleText.Visibility = Visibility.Collapsed;
            }
            else
            {
                dialog.TitleText.Visibility = Visibility.Visible;
            }
        }
    }

    /// <summary>
    /// 对话框内容变更回调，将新内容设置到 ContentPresenter
    /// </summary>
    private static void OnDialogContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModalDialog dialog)
        {
            dialog.DialogContentPresenter.Content = e.NewValue;
        }
    }

    /// <summary>
    /// 图标显示状态变更回调，控制标题图标可见性
    /// </summary>
    private static void OnShowIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModalDialog dialog)
        {
            dialog.TitleIcon.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 关闭按钮显示状态变更回调，控制右上角 X 按钮可见性
    /// </summary>
    private static void OnShowCloseButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModalDialog dialog)
        {
            dialog.CloseButton.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 事件处理
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 关闭按钮点击事件处理，隐藏对话框
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    // ═══════════════════════════════════════════════════════════════
    // 公共方法 - 按钮管理
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 向对话框底部按钮栏动态添加一个按钮。
    /// 第一个按钮左对齐，后续按钮右对齐。
    /// 主按钮使用红色渐变填充，普通按钮使用半透明白色填充
    /// </summary>
    /// <param name="text">按钮文本</param>
    /// <param name="clickHandler">点击事件处理委托</param>
    /// <param name="isPrimary">是否为主操作按钮（决定颜色样式）</param>
    public void AddButton(string text, RoutedEventHandler clickHandler, bool isPrimary = false)
    {
        var buttonIndex = ButtonPanel.Children.Count;

        HorizontalAlignment hAlign;
        Thickness margin;

        if (buttonIndex == 0)
        {
            hAlign = HorizontalAlignment.Left;
            margin = new Thickness(70, 0, 0, 0);
        }
        else
        {
            hAlign = HorizontalAlignment.Right;
            margin = new Thickness(0, 0, 70, 0);
        }

        var button = new Button
        {
            Content = text,
            Width = 172,
            Height = 32,
            Margin = margin,
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = hAlign
        };

        Grid.SetColumn(button, buttonIndex);

        // 动态构建按钮的 ControlTemplate
        var template = new ControlTemplate(typeof(Button));

        var gridFactory = new FrameworkElementFactory(typeof(Grid));

        // 背景路径：六边形按钮形状
        var pathFactory = new FrameworkElementFactory(typeof(Path));
        pathFactory.SetValue(Path.DataProperty, Geometry.Parse("M0 6V32H166L172 26V0H6L0 6Z"));
        pathFactory.SetValue(Path.StretchProperty, Stretch.Fill);
        pathFactory.SetValue(Path.WidthProperty, 172.0);
        pathFactory.SetValue(Path.HeightProperty, 32.0);

        // 根据 isPrimary 决定按钮颜色：主按钮红色渐变，普通按钮半透明白色
        if (isPrimary)
        {
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                Opacity = 0.8
            };
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(198, 14, 14), 0));
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(96, 7, 7), 1));
            pathFactory.SetValue(Path.FillProperty, gradient);
        }
        else
        {
            var solidBrush = new SolidColorBrush(Color.FromRgb(238, 238, 238)) { Opacity = 0.2 };
            pathFactory.SetValue(Path.FillProperty, solidBrush);
        }

        // 文本内容呈现器
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        contentFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(238, 238, 238)));
        contentFactory.SetValue(TextBlock.FontSizeProperty, 18.0);
        contentFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);

        gridFactory.AppendChild(pathFactory);
        gridFactory.AppendChild(contentFactory);

        template.VisualTree = gridFactory;
        button.Template = template;

        button.Click += clickHandler;
        ButtonPanel.Children.Add(button);
    }

    /// <summary>
    /// 清除底部按钮栏中所有已添加的按钮。
    /// 通常在隐藏对话框时调用以重置状态
    /// </summary>
    public void ClearButtons()
    {
        ButtonPanel.Children.Clear();
    }

    // ═══════════════════════════════════════════════════════════════
    // 显示/隐藏控制
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 显示模态对话框（设置 Visibility 为 Visible）
    /// </summary>
    public void Show()
    {
        Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 隐藏模态对话框并重置所有属性到默认状态。
    /// 重置图标、关闭按钮、标题、内容和按钮栏，
    /// 防止上次调用的状态残留影响下次显示
    /// </summary>
    public void Hide()
    {
        Visibility = Visibility.Collapsed;

        // 弹窗关闭时，自动重置状态，防止污染下一次调用
        ShowIcon = false;
        ShowCloseButton = false;
        Title = string.Empty;
        DialogContent = null;
        ClearButtons(); // 强烈建议在这里也清理一下按钮，防止下次调用时按钮重复叠加
    }
}
