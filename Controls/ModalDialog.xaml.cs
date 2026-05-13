using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HITAPEX.Controls;

public partial class ModalDialog : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ModalDialog), 
            new PropertyMetadata(string.Empty, OnTitleChanged));

    public static readonly DependencyProperty DialogContentProperty =
        DependencyProperty.Register(nameof(DialogContent), typeof(object), typeof(ModalDialog), 
            new PropertyMetadata(null, OnDialogContentChanged));

    public static readonly DependencyProperty ShowIconProperty =
        DependencyProperty.Register(nameof(ShowIcon), typeof(bool), typeof(ModalDialog), 
            new PropertyMetadata(false, OnShowIconChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object DialogContent
    {
        get => GetValue(DialogContentProperty);
        set => SetValue(DialogContentProperty, value);
    }

    public bool ShowIcon
    {
        get => (bool)GetValue(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    public ModalDialog()
    {
        InitializeComponent();
    }

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

    private static void OnDialogContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModalDialog dialog)
        {
            dialog.DialogContentPresenter.Content = e.NewValue;
        }
    }

    private static void OnShowIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModalDialog dialog)
        {
            dialog.TitleIcon.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

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

        var template = new ControlTemplate(typeof(Button));

        var gridFactory = new FrameworkElementFactory(typeof(Grid));

        var pathFactory = new FrameworkElementFactory(typeof(Path));
        pathFactory.SetValue(Path.DataProperty, Geometry.Parse("M0 6V32H166L172 26V0H6L0 6Z"));
        pathFactory.SetValue(Path.StretchProperty, Stretch.Fill);
        pathFactory.SetValue(Path.WidthProperty, 172.0);
        pathFactory.SetValue(Path.HeightProperty, 32.0);

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

    public void ClearButtons()
    {
        ButtonPanel.Children.Clear();
    }

    public void Show()
    {
        Visibility = Visibility.Visible;
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;

        // 弹窗关闭时，自动重置状态，防止污染下一次调用
        ShowIcon = false;
        Title = string.Empty;
        DialogContent = null;
        ClearButtons(); // 强烈建议在这里也清理一下按钮，防止下次调用时按钮重复叠加
    }

    // ������������ֲ�ʱ����
    private void Overlay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // ����ԭ�е� Hide �����رյ���
        Hide();
    }

    // �����������������ʱ����
    private void DialogContent_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // ���� Handled Ϊ true����ֹ����¼����ݵ����� Overlay����ֹ���
        e.Handled = true;
    }
}
