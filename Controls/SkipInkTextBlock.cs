using System.Windows;
using System.Windows.Media;
using System.Windows.Documents;

namespace HITAPEX.Controls
{
    public class SkipInkTextBlock : FrameworkElement
    {
        // ==========================================
        // 1. 基础文本与颜色属性
        // ==========================================
        
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

        public static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register("Foreground", typeof(Brush), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

        public static readonly DependencyProperty UnderlineBrushProperty =
            DependencyProperty.Register("UnderlineBrush", typeof(Brush), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush UnderlineBrush { get => (Brush)GetValue(UnderlineBrushProperty); set => SetValue(UnderlineBrushProperty, value); }

        public static readonly DependencyProperty BackgroundProperty =
            DependencyProperty.Register("Background", typeof(Brush), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush Background { get => (Brush)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

        // ==========================================
        // 2. 排版与字体属性 (方法二的核心增加部分)
        // ==========================================

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register("FontSize", typeof(double), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

        public static readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register("FontWeight", typeof(FontWeight), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

        public static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register("FontFamily", typeof(FontFamily), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

        public static readonly DependencyProperty FontStyleProperty =
            DependencyProperty.Register("FontStyle", typeof(FontStyle), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(FontStyles.Normal, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public FontStyle FontStyle { get => (FontStyle)GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }

        public static readonly DependencyProperty TextTrimmingProperty =
            DependencyProperty.Register("TextTrimming", typeof(TextTrimming), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(TextTrimming.CharacterEllipsis, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public TextTrimming TextTrimming { get => (TextTrimming)GetValue(TextTrimmingProperty); set => SetValue(TextTrimmingProperty, value); }

        // ==========================================
        // 3. 核心布局与渲染逻辑
        // ==========================================

        protected override Size MeasureOverride(Size availableSize)
        {
            if (string.IsNullOrEmpty(Text)) return new Size(0, 0);

            var formattedText = CreateFormattedText(availableSize.Width);
            return new Size(formattedText.Width, formattedText.Height);
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (string.IsNullOrEmpty(Text)) return;

            // 1. 绘制背景膜，确保整个控件区域可以响应鼠标点击 (Cursor="Hand")
            dc.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));

            var formattedText = CreateFormattedText(ActualWidth);

            // 2. 获取文字的几何轮廓，并向外扩张2像素作为“保护罩”
            Geometry textGeometry = formattedText.BuildGeometry(new Point(0, 0));
            Geometry widenedTextGeometry = textGeometry.GetWidenedPathGeometry(new Pen(Brushes.Black, 2));

            // 3. 创建下划线 (向下偏移2像素)
            double lineY = formattedText.Baseline + 2;
            Geometry underlineGeometry = new RectangleGeometry(new Rect(0, lineY, formattedText.Width, 1));

            // 4. 布尔运算：用“保护罩”裁剪下划线，实现 Skip-Ink 效果
            Geometry skipInkUnderline = Geometry.Combine(underlineGeometry, widenedTextGeometry, GeometryCombineMode.Exclude, null);

            // 5. 绘制断开的下划线和纯净的文字
            dc.DrawGeometry(UnderlineBrush, null, skipInkUnderline);
            dc.DrawGeometry(Foreground, null, textGeometry);
        }

        private FormattedText CreateFormattedText(double maxWidth)
        {
            var formattedText = new FormattedText(
                Text ?? string.Empty,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                // 直接使用我们注册的 FontFamily, FontStyle, FontWeight 依赖属性
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretches.Normal),
                FontSize,
                Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // 裁剪逻辑：限制最大宽度和高度，迫使系统使用省略号而不是换行
            if (TextTrimming != TextTrimming.None && !double.IsInfinity(maxWidth) && maxWidth > 0)
            {
                formattedText.MaxTextWidth = maxWidth;
                formattedText.MaxTextHeight = FontSize * 2; 
                formattedText.Trimming = TextTrimming;
            }

            return formattedText;
        }
    }
}