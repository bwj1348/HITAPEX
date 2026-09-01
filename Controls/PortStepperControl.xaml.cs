using System.Windows;
using System.Windows.Controls;

namespace HITAPEX.Controls;

/// <summary>
/// 端口号调节控件：左侧可直接编辑输入框，右侧带 - / + 两个按钮增减数值。
/// 数值范围限制在有效 UDP 端口区间内。
/// </summary>
public partial class PortStepperControl : UserControl
{
    public const int MinPort = 0;
    public const int MaxPort = 65535;

    public static readonly DependencyProperty PortProperty =
        DependencyProperty.Register(nameof(Port), typeof(string), typeof(PortStepperControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>当前端口号（字符串形式，允许用户自由编辑）。</summary>
    public string Port
    {
        get => (string)GetValue(PortProperty);
        set => SetValue(PortProperty, value);
    }

    public PortStepperControl()
    {
        InitializeComponent();
    }

    private void MinusButton_Click(object sender, RoutedEventArgs e) => ChangePort(-1);

    private void PlusButton_Click(object sender, RoutedEventArgs e) => ChangePort(1);

    private void ChangePort(int delta)
    {
        var current = int.TryParse(Port, out var v) ? v : 0;
        var next = current + delta;
        if (next < MinPort) next = MinPort;
        if (next > MaxPort) next = MaxPort;
        Port = next.ToString();
    }
}