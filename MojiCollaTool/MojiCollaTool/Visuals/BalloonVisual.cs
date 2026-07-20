using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MojiCollaTool
{
    /// <summary>
    /// Lightweight page-level balloon visual. The visual owns no document
    /// history; PageEditorControl commits its final data on pointer-up.
    /// </summary>
    public sealed class BalloonVisual : FrameworkElement
    {
        private static readonly Pen SelectionPen = CreateSelectionPen();
        private readonly BalloonGeometryFactory _geometryFactory;

        public BalloonVisual(BalloonData data, BalloonGeometryFactory? geometryFactory = null)
        {
            BalloonData = data ?? throw new ArgumentNullException(nameof(data));
            _geometryFactory = geometryFactory ?? new BalloonGeometryFactory();
            Focusable = false;
            SnapsToDevicePixels = true;
            IsHitTestVisible = data.IsVisible && !IsMergeRenderingSuppressed;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseMove += OnMouseMove;
            LostMouseCapture += OnLostMouseCapture;
            Refresh();
        }

        public BalloonData BalloonData { get; private set; }
        public Guid ObjectId => BalloonData.ObjectId;
        public bool IsSelected { get; set; }
        public bool IsMergeRenderingSuppressed { get; private set; }

        internal Geometry BodyGeometry => _geometryFactory.Create(BalloonData.ShapeKind, LocalBounds);
        internal Geometry TailGeometry => BalloonTailGeometry.Create(BalloonData, _geometryFactory);

        public event MouseButtonEventHandler? BalloonMouseLeftButtonDown;
        public event MouseButtonEventHandler? BalloonMouseLeftButtonUp;
        public event MouseEventHandler? BalloonMouseMove;
        public event MouseEventHandler? BalloonLostMouseCapture;

        public void ApplyData(BalloonData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            BalloonData = data;
            IsHitTestVisible = data.IsVisible;
            Refresh();
        }

        public void Refresh()
        {
            var width = Math.Max(1, BalloonData.Bounds.Width);
            var height = Math.Max(1, BalloonData.Bounds.Height);
            Width = width;
            Height = height;
            Canvas.SetLeft(this, BalloonData.X);
            Canvas.SetTop(this, BalloonData.Y);
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = new RotateTransform(BalloonData.Rotation);
            InvalidateVisual();
        }

        public void SetMergeRenderingSuppressed(bool suppressed)
        {
            IsMergeRenderingSuppressed = suppressed;
            IsHitTestVisible = BalloonData.IsVisible && !suppressed;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (!BalloonData.IsVisible || IsMergeRenderingSuppressed) return;

            var geometry = BodyGeometry;
            var fill = new SolidColorBrush(BalloonData.Fill);
            var stroke = new SolidColorBrush(BalloonData.Stroke);
            var thickness = Math.Max(0, BalloonData.StrokeThickness);
            var pen = thickness > 0 ? new Pen(stroke, thickness) : null;
            if (BalloonData.Tail != null)
            {
                drawingContext.DrawGeometry(fill, pen, TailGeometry);
            }
            drawingContext.DrawGeometry(fill, pen, geometry);

            if (IsSelected)
            {
                var selectionBounds = new Rect(0, 0, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
                drawingContext.DrawRectangle(null, SelectionPen, selectionBounds);
            }
        }

        protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
        {
            if (!BalloonData.IsVisible || IsMergeRenderingSuppressed) return null;
            var point = hitTestParameters.HitPoint;
            var thickness = Math.Max(1, BalloonData.StrokeThickness);
            var pen = new Pen(Brushes.Black, thickness);
            var body = BodyGeometry;
            var tail = BalloonData.Tail == null ? Geometry.Empty : TailGeometry;
            return body.FillContains(point) || body.StrokeContains(pen, point) ||
                   tail.FillContains(point) || tail.StrokeContains(pen, point)
                ? new PointHitTestResult(this, point)
                : null;
        }

        internal bool ContainsLocalPoint(Point point)
            => HitTestCore(new PointHitTestParameters(point)) != null;

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => BalloonMouseLeftButtonDown?.Invoke(this, e);
        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => BalloonMouseLeftButtonUp?.Invoke(this, e);
        private void OnMouseMove(object sender, MouseEventArgs e) => BalloonMouseMove?.Invoke(this, e);
        private void OnLostMouseCapture(object sender, MouseEventArgs e) => BalloonLostMouseCapture?.Invoke(this, e);

        private static Pen CreateSelectionPen()
        {
            var pen = new Pen(Brushes.DodgerBlue, 1);
            pen.DashStyle = DashStyles.Dash;
            pen.Freeze();
            return pen;
        }

        private Rect LocalBounds => new(0, 0, Math.Max(0, BalloonData.Bounds.Width), Math.Max(0, BalloonData.Bounds.Height));
    }
}
