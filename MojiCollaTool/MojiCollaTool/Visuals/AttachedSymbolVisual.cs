using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MojiCollaTool
{
    /// <summary>
    /// Renders one attached symbol in the coordinate space of its parent text
    /// panel. The parent panel owns placement so a symbol follows parent
    /// movement, rotation, and vertical/horizontal layout automatically.
    /// </summary>
    public sealed class AttachedSymbolVisual : FrameworkElement
    {
        private static readonly Pen SelectionPen = CreateSelectionPen();
        private readonly VisualCollection _children;
        private MojiData _parentText;
        private Rect _anchorBounds;
        private Geometry? _geometry;
        private bool _fontFallback;

        public AttachedSymbolVisual(AttachedSymbolData symbol, MojiData parentText)
        {
            SymbolData = symbol ?? throw new ArgumentNullException(nameof(symbol));
            _parentText = parentText ?? throw new ArgumentNullException(nameof(parentText));
            _children = new VisualCollection(this);
            Focusable = false;
            SnapsToDevicePixels = true;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseMove += OnMouseMove;
            LostMouseCapture += OnLostMouseCapture;
            Refresh(_anchorBounds);
        }

        public AttachedSymbolData SymbolData { get; private set; }
        public Guid ObjectId => SymbolData.ObjectId;
        public MojiData ParentTextData => _parentText;
        public bool IsSelected { get; set; }
        public bool IsFontFallback => _fontFallback;
        public string FontStatus => _fontFallback ? "フォント未検出（代替フォント表示）" : "フォント適用済み";
        public Rect AnchorBounds => _anchorBounds;

        public event MouseButtonEventHandler? SymbolMouseLeftButtonDown;
        public event MouseButtonEventHandler? SymbolMouseLeftButtonUp;
        public event MouseEventHandler? SymbolMouseMove;
        public event MouseEventHandler? SymbolLostMouseCapture;

        public void ApplyData(AttachedSymbolData symbol, MojiData parentText, Rect anchorBounds)
        {
            SymbolData = symbol ?? throw new ArgumentNullException(nameof(symbol));
            _parentText = parentText ?? throw new ArgumentNullException(nameof(parentText));
            Refresh(anchorBounds);
        }

        public void Refresh(Rect anchorBounds)
        {
            _anchorBounds = anchorBounds;
            var fontSize = ResolveFontSize();
            var fontFamily = ResolveFontFamily(out _fontFallback);
            var inheritDecoration = SymbolData.Inherit.HasFlag(AttachedSymbolInheritance.Decoration);
            var typeface = new Typeface(
                fontFamily,
                inheritDecoration && _parentText.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                inheritDecoration && _parentText.IsBold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);
            var formattedText = new FormattedText(
                SymbolData.Text,
                CultureInfo.GetCultureInfo("ja-JP"),
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Transparent,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            Width = Math.Max(1, formattedText.WidthIncludingTrailingWhitespace);
            Height = Math.Max(1, formattedText.Height);
            Canvas.SetLeft(this, anchorBounds.Left + anchorBounds.Width / 2 + SymbolData.OffsetX * _parentText.FontSize - Width / 2);
            Canvas.SetTop(this, anchorBounds.Top + anchorBounds.Height / 2 + SymbolData.OffsetY * _parentText.FontSize - Height / 2);
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = new RotateTransform(SymbolData.Rotation);
            _geometry = formattedText.BuildGeometry(new Point(0, 0));
            RebuildChildren(_geometry);
            IsHitTestVisible = SymbolData.IsVisible && !SymbolData.IsDetached;
            Visibility = SymbolData.IsVisible && !SymbolData.IsDetached
                ? Visibility.Visible
                : Visibility.Hidden;
            InvalidateVisual();
        }

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _children.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _children[index];
        }

        protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
        {
            if (!IsHitTestVisible) return null;
            var point = hitTestParameters.HitPoint;
            return point.X >= 0 && point.Y >= 0 && point.X <= ActualWidth && point.Y <= ActualHeight
                ? new PointHitTestResult(this, point)
                : null;
        }

        private void RebuildChildren(Geometry geometry)
        {
            _children.Clear();
            if (!SymbolData.IsVisible || SymbolData.IsDetached) return;
            var color = ResolveColor();
            if (_parentText.IsSecondBorderExists && SymbolData.Inherit.HasFlag(AttachedSymbolInheritance.Border))
                _children.Add(CreateBorderVisual(geometry, _parentText.SecondBorderThickness, _parentText.SecondBorderColor, _parentText.SecondBorderBlurrRadius));
            if (_parentText.IsBorderExists && SymbolData.Inherit.HasFlag(AttachedSymbolInheritance.Border))
                _children.Add(CreateBorderVisual(geometry, _parentText.BorderThickness, _parentText.BorderColor, _parentText.BorderBlurrRadius));

            var textVisual = new DrawingVisual();
            using (var context = textVisual.RenderOpen())
            {
                context.DrawGeometry(new SolidColorBrush(color), null, geometry);
                if (IsSelected) context.DrawRectangle(null, SelectionPen, new Rect(0, 0, Width, Height));
            }
            _children.Add(textVisual);
        }

        private DrawingVisual CreateBorderVisual(Geometry geometry, double thickness, Color color, double blurRadius)
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var pen = new Pen(new SolidColorBrush(color), Math.Max(0, thickness)) { LineJoin = PenLineJoin.Round };
                context.DrawGeometry(Brushes.Transparent, pen, geometry);
            }
            if (blurRadius > 0) visual.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = blurRadius };
            return visual;
        }

        private int ResolveFontSize()
        {
            var inherited = SymbolData.Inherit.HasFlag(AttachedSymbolInheritance.Font);
            var size = inherited || SymbolData.FontSize <= 0 ? _parentText.FontSize : SymbolData.FontSize;
            return Math.Max(1, (int)Math.Round(size * Math.Max(0.01, SymbolData.Scale)));
        }

        private FontFamily ResolveFontFamily(out bool fallback)
        {
            var requested = SymbolData.Inherit.HasFlag(AttachedSymbolInheritance.Font)
                ? _parentText.FontFamilyName
                : SymbolData.FontFamilyName;
            var families = FontUtil.GetFontFamilies();
            if (!string.IsNullOrWhiteSpace(requested) && families.TryGetValue(requested, out var family))
            {
                fallback = false;
                return family;
            }

            fallback = true;
            return SystemFonts.MessageFontFamily;
        }

        private Color ResolveColor()
        {
            if (SymbolData.Inherit.HasFlag(AttachedSymbolInheritance.ForeColor)) return _parentText.ForeColor;
            return Color.FromArgb((byte)(SymbolData.ForeColorArgb >> 24), (byte)(SymbolData.ForeColorArgb >> 16),
                (byte)(SymbolData.ForeColorArgb >> 8), (byte)SymbolData.ForeColorArgb);
        }

        private static Pen CreateSelectionPen()
        {
            var pen = new Pen(Brushes.DodgerBlue, 1);
            pen.DashStyle = DashStyles.Dash;
            pen.Freeze();
            return pen;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SymbolMouseLeftButtonDown?.Invoke(this, e);
        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => SymbolMouseLeftButtonUp?.Invoke(this, e);
        private void OnMouseMove(object sender, MouseEventArgs e) => SymbolMouseMove?.Invoke(this, e);
        private void OnLostMouseCapture(object sender, MouseEventArgs e) => SymbolLostMouseCapture?.Invoke(this, e);
    }
}
