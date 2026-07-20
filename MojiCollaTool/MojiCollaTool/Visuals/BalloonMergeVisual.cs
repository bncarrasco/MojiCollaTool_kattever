using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MojiCollaTool
{
    /// <summary>Single body/tail visual and hit target for one merge group.</summary>
    public sealed class BalloonMergeVisual : FrameworkElement
    {
        private static readonly Pen SelectionPen = CreateSelectionPen();
        private readonly BalloonMergeGeometryFactory _factory;
        private BalloonMergeGeometry _geometry = null!;

        public BalloonMergeVisual(BalloonMergeData merge, IEnumerable<BalloonData> members,
            BalloonMergeGeometryFactory? factory = null)
        {
            _factory = factory ?? new BalloonMergeGeometryFactory();
            Focusable = false;
            SnapsToDevicePixels = true;
            MouseLeftButtonDown += (_, e) => MergeMouseLeftButtonDown?.Invoke(this, e);
            MouseLeftButtonUp += (_, e) => MergeMouseLeftButtonUp?.Invoke(this, e);
            MouseMove += (_, e) => MergeMouseMove?.Invoke(this, e);
            LostMouseCapture += (_, e) => MergeLostMouseCapture?.Invoke(this, e);
            ApplyData(merge, members);
        }

        public BalloonMergeData MergeData { get; private set; } = null!;
        public IReadOnlyList<BalloonData> Members { get; private set; } = Array.Empty<BalloonData>();
        public Guid MergeId => MergeData.MergeId;
        public Guid PrimaryBalloonId => MergeData.PrimaryBalloonId;
        public bool IsSelected { get; set; }
        internal BalloonMergeGeometry Geometry => _geometry;

        public event MouseButtonEventHandler? MergeMouseLeftButtonDown;
        public event MouseButtonEventHandler? MergeMouseLeftButtonUp;
        public event MouseEventHandler? MergeMouseMove;
        public event MouseEventHandler? MergeLostMouseCapture;

        public void ApplyData(BalloonMergeData merge, IEnumerable<BalloonData> members)
        {
            MergeData = merge?.Clone() ?? throw new ArgumentNullException(nameof(merge));
            Members = (members ?? throw new ArgumentNullException(nameof(members)))
                .Select(PageDocument.CloneBalloonData).ToArray();
            _geometry = _factory.Create(MergeData, Members);
            var bounds = _geometry.Bounds.IsEmpty ? new Rect(0, 0, 1, 1) : _geometry.Bounds;
            Width = Math.Max(1, bounds.Width);
            Height = Math.Max(1, bounds.Height);
            Canvas.SetLeft(this, bounds.X);
            Canvas.SetTop(this, bounds.Y);
            IsHitTestVisible = Members.Any(item => item.IsVisible);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var primary = Members.Single(item => item.ObjectId == PrimaryBalloonId);
            if (!primary.IsVisible) return;
            var offset = _geometry.Bounds.IsEmpty ? new Vector() : new Vector(-_geometry.Bounds.X, -_geometry.Bounds.Y);
            drawingContext.PushTransform(new TranslateTransform(offset.X, offset.Y));
            var fill = new SolidColorBrush(primary.Fill);
            var stroke = new SolidColorBrush(primary.Stroke);
            var thickness = Math.Max(0, primary.StrokeThickness);
            var pen = thickness > 0 ? new Pen(stroke, thickness) : null;
            foreach (var tail in _geometry.Tails) drawingContext.DrawGeometry(fill, pen, tail);
            drawingContext.DrawGeometry(fill, pen, _geometry.Body);
            drawingContext.Pop();
            if (IsSelected) drawingContext.DrawRectangle(null, SelectionPen, new Rect(0, 0, Width, Height));
        }

        protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
        {
            var primary = Members.Single(item => item.ObjectId == PrimaryBalloonId);
            var pagePoint = _geometry.Bounds.IsEmpty
                ? hitTestParameters.HitPoint
                : new Point(hitTestParameters.HitPoint.X + _geometry.Bounds.X, hitTestParameters.HitPoint.Y + _geometry.Bounds.Y);
            return _geometry.Contains(pagePoint, primary.StrokeThickness)
                ? new PointHitTestResult(this, hitTestParameters.HitPoint)
                : null;
        }

        internal bool ContainsLocalPoint(Point point)
            => HitTestCore(new PointHitTestParameters(point)) != null;

        private static Pen CreateSelectionPen()
        {
            var pen = new Pen(Brushes.DodgerBlue, 1) { DashStyle = DashStyles.Dash };
            pen.Freeze();
            return pen;
        }
    }
}
