using System.Windows;
using System.Windows.Media;
using System.Windows.Documents;

namespace HITAPEX.Controls
{
    public class SkipInkTextBlock : FrameworkElement
    {
        // ==========================================
        // 1. Basic text and color properties
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
        // 2. Typography and font properties
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
        // 3. Cache fields - avoid rebuilding FormattedText / Geometry every frame
        // ==========================================

        private FormattedText? _cachedFormattedText;
        private Geometry? _cachedTextGeometry;
        private Geometry? _cachedUnderlineGeometry;
        private double _cachedMaxWidth;

        // ==========================================
        // 4. Core layout and rendering logic
        // ==========================================

        protected override Size MeasureOverride(Size availableSize)
        {
            if (string.IsNullOrEmpty(Text)) return new Size(0, 0);

            var formattedText = GetOrCreateFormattedText(availableSize.Width);
            return new Size(formattedText.Width, formattedText.Height);
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (string.IsNullOrEmpty(Text)) return;

            // 1. Draw background rect so the whole control area can respond to mouse clicks
            dc.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));

            // Use cached geometry objects
            EnsureGeometryCache(ActualWidth);

            if (_cachedUnderlineGeometry != null)
                dc.DrawGeometry(UnderlineBrush, null, _cachedUnderlineGeometry);
            if (_cachedTextGeometry != null)
                dc.DrawGeometry(Foreground, null, _cachedTextGeometry);
        }

        private FormattedText GetOrCreateFormattedText(double maxWidth)
        {
            if (_cachedFormattedText != null && _cachedMaxWidth == maxWidth)
                return _cachedFormattedText;

            _cachedFormattedText = CreateFormattedText(maxWidth);
            _cachedMaxWidth = maxWidth;
            InvalidateGeometryCache();
            return _cachedFormattedText;
        }

        private void EnsureGeometryCache(double maxWidth)
        {
            // Ensure FormattedText is up to date
            GetOrCreateFormattedText(maxWidth);

            if (_cachedTextGeometry != null && _cachedUnderlineGeometry != null)
                return;

            var formattedText = _cachedFormattedText!;
            _cachedTextGeometry = formattedText.BuildGeometry(new Point(0, 0));
            var widenedTextGeometry = _cachedTextGeometry.GetWidenedPathGeometry(new Pen(Brushes.Black, 2));

            double lineY = formattedText.Baseline + 2;
            var underlineGeometry = new RectangleGeometry(new Rect(0, lineY, formattedText.Width, 1));

            _cachedUnderlineGeometry = Geometry.Combine(underlineGeometry, widenedTextGeometry, GeometryCombineMode.Exclude, null);
        }

        private void InvalidateGeometryCache()
        {
            _cachedTextGeometry = null;
            _cachedUnderlineGeometry = null;
        }

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
