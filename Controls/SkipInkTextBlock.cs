using System.Windows;
using System.Windows.Media;
using System.Windows.Documents;

namespace HITAPEX.Controls
{
    /// <summary>
    /// 自定义文本渲染控件，实现下划线"跳过"文字笔画的效果。
    /// 与普通 TextBlock 不同，此控件使用 Geometry 进行精确绘制，
    /// 使下划线在文字笔画处中断（Exclude 模式），避免下划线穿过文字。
    /// 适用于需要精美文字下划线效果的标题、按钮等场景。
    /// </summary>
    public class SkipInkTextBlock : FrameworkElement
    {
        // ═══════════════════════════════════════════════════════════════
        // 1. 基础文本与颜色依赖属性
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 要显示的文本内容的依赖属性
        /// </summary>
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// 显示的文本内容
        /// </summary>
        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

        /// <summary>
        /// 文字前景色的依赖属性
        /// </summary>
        public static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register("Foreground", typeof(Brush), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// 文字前景色画刷，默认为白色
        /// </summary>
        public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

        /// <summary>
        /// 下划线颜色的依赖属性
        /// </summary>
        public static readonly DependencyProperty UnderlineBrushProperty =
            DependencyProperty.Register("UnderlineBrush", typeof(Brush), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// 下划线颜色画刷，默认为灰色
        /// </summary>
        public Brush UnderlineBrush { get => (Brush)GetValue(UnderlineBrushProperty); set => SetValue(UnderlineBrushProperty, value); }

        /// <summary>
        /// 背景色的依赖属性
        /// </summary>
        public static readonly DependencyProperty BackgroundProperty =
            DependencyProperty.Register("Background", typeof(Brush), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// 背景色画刷，默认为透明。用于使整个控件区域响应鼠标点击
        /// </summary>
        public Brush Background { get => (Brush)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

        // ═══════════════════════════════════════════════════════════════
        // 2. 排版与字体依赖属性
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 字体大小的依赖属性
        /// </summary>
        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register("FontSize", typeof(double), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// 字体大小，默认为 12
        /// </summary>
        public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

        /// <summary>
        /// 字体粗细的依赖属性
        /// </summary>
        public static readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register("FontWeight", typeof(FontWeight), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// 字体粗细，默认为 Normal
        /// </summary>
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

        /// <summary>
        /// 字体族的依赖属性
        /// </summary>
        public static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register("FontFamily", typeof(FontFamily), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// 字体族，默认为系统消息字体
        /// </summary>
        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

        /// <summary>
        /// 字体样式的依赖属性
        /// </summary>
        public static readonly DependencyProperty FontStyleProperty =
            DependencyProperty.Register("FontStyle", typeof(FontStyle), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(FontStyles.Normal, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// 字体样式（Normal、Italic 等），默认为 Normal
        /// </summary>
        public FontStyle FontStyle { get => (FontStyle)GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }

        /// <summary>
        /// 文本裁剪模式的依赖属性
        /// </summary>
        public static readonly DependencyProperty TextTrimmingProperty =
            DependencyProperty.Register("TextTrimming", typeof(TextTrimming), typeof(SkipInkTextBlock),
                new FrameworkPropertyMetadata(TextTrimming.CharacterEllipsis, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// 文本溢出时的裁剪方式，默认为字符省略号（CharacterEllipsis）
        /// </summary>
        public TextTrimming TextTrimming { get => (TextTrimming)GetValue(TextTrimmingProperty); set => SetValue(TextTrimmingProperty, value); }

        // ═══════════════════════════════════════════════════════════════
        // 3. 缓存字段 - 避免每次渲染时重建 FormattedText / Geometry
        // ═══════════════════════════════════════════════════════════════

        /// <summary>缓存的格式化文本对象</summary>
        private FormattedText? _cachedFormattedText;

        /// <summary>缓存的文字几何图形</summary>
        private Geometry? _cachedTextGeometry;

        /// <summary>缓存的下划线几何图形（文字笔画区域已排除）</summary>
        private Geometry? _cachedUnderlineGeometry;

        /// <summary>缓存的最大宽度，用于检测是否需要重建</summary>
        private double _cachedMaxWidth;

        // ═══════════════════════════════════════════════════════════════
        // 4. 核心布局与渲染逻辑
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 测量阶段：计算文本所需的空间大小。
        /// 考虑文本裁剪和最大宽度限制
        /// </summary>
        /// <param name="availableSize">可用空间</param>
        /// <returns>文本所需的实际大小</returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            if (string.IsNullOrEmpty(Text)) return new Size(0, 0);

            var formattedText = GetOrCreateFormattedText(availableSize.Width);
            return new Size(formattedText.Width, formattedText.Height);
        }

        /// <summary>
        /// 渲染阶段：绘制背景、下划线和文字。
        /// 使用缓存的 Geometry 对象提高性能，
        /// 下划线通过 Geometry.Combine Exclude 模式在文字笔画处断开
        /// </summary>
        /// <param name="dc">绘图上下文</param>
        protected override void OnRender(DrawingContext dc)
        {
            if (string.IsNullOrEmpty(Text)) return;

            // 1. 绘制背景矩形，使整个控件区域可响应鼠标点击
            dc.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));

            // 使用缓存的几何图形对象
            EnsureGeometryCache(ActualWidth);

            if (_cachedUnderlineGeometry != null)
                dc.DrawGeometry(UnderlineBrush, null, _cachedUnderlineGeometry);
            if (_cachedTextGeometry != null)
                dc.DrawGeometry(Foreground, null, _cachedTextGeometry);
        }

        // ═══════════════════════════════════════════════════════════════
        // 5. 几何图形缓存管理
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 获取或创建格式化文本对象。宽度变化时自动重建，
        /// 宽度不变时返回缓存实例
        /// </summary>
        /// <param name="maxWidth">最大可用宽度</param>
        /// <returns>格式化文本对象</returns>
        private FormattedText GetOrCreateFormattedText(double maxWidth)
        {
            if (_cachedFormattedText != null && _cachedMaxWidth == maxWidth)
                return _cachedFormattedText;

            _cachedFormattedText = CreateFormattedText(maxWidth);
            _cachedMaxWidth = maxWidth;
            InvalidateGeometryCache();
            return _cachedFormattedText;
        }

        /// <summary>
        /// 确保文字和下划线几何图形缓存有效。
        /// 通过 Geometry.Combine 的 Exclude 模式，
        /// 将下划线中与文字笔画重叠的部分排除，
        /// 实现下划线不穿过文字的效果
        /// </summary>
        /// <param name="maxWidth">最大可用宽度</param>
        private void EnsureGeometryCache(double maxWidth)
        {
            // 先确保 FormattedText 是最新的
            GetOrCreateFormattedText(maxWidth);

            if (_cachedTextGeometry != null && _cachedUnderlineGeometry != null)
                return;

            var formattedText = _cachedFormattedText!;
            _cachedTextGeometry = formattedText.BuildGeometry(new Point(0, 0));

            // 对文字几何图形扩边（加粗 2 像素），确保下划线在文字边缘也有足够空隙
            var widenedTextGeometry = _cachedTextGeometry.GetWidenedPathGeometry(new Pen(Brushes.Black, 2));

            // 计算下划线位置：基线下方 2 像素处
            double lineY = formattedText.Baseline + 2;
            var underlineGeometry = new RectangleGeometry(new Rect(0, lineY, formattedText.Width, 1));

            // 从下划线矩形中排除文字笔画区域，实现"跳过墨水"效果
            _cachedUnderlineGeometry = Geometry.Combine(underlineGeometry, widenedTextGeometry, GeometryCombineMode.Exclude, null);
        }

        /// <summary>
        /// 清除缓存的几何图形，强制下次渲染时重新构建
        /// </summary>
        private void InvalidateGeometryCache()
        {
            _cachedTextGeometry = null;
            _cachedUnderlineGeometry = null;
        }

        // ═══════════════════════════════════════════════════════════════
        // 6. FormattedText 创建
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 根据当前属性创建 FormattedText 对象。
        /// 包含所有字体、颜色和裁剪设置
        /// </summary>
        /// <param name="maxWidth">最大宽度限制</param>
        /// <returns>配置完成的 FormattedText</returns>
        private FormattedText CreateFormattedText(double maxWidth)
        {
            var formattedText = new FormattedText(
                Text ?? string.Empty,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretches.Normal),
                FontSize,
                Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // 如果启用了文本裁剪且最大宽度有效，则配置裁切参数
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
