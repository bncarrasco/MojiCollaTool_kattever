using System;
using System.Windows;
using System.Windows.Media;

namespace MojiCollaTool
{
    /// <summary>
    /// Deterministic page/local coordinate conversion and single-tail geometry.
    /// RootParameter starts at the top of the body and proceeds clockwise.
    /// </summary>
    public static class BalloonTailGeometry
    {
        private static readonly BalloonGeometryFactory SharedGeometryFactory = new();

        public static Geometry Create(BalloonData balloon, BalloonGeometryFactory? geometryFactory = null)
        {
            if (balloon == null) throw new ArgumentNullException(nameof(balloon));
            if (balloon.Tail == null) return Geometry.Empty;

            var placement = GetRootPlacement(balloon.ShapeKind, LocalBounds(balloon), balloon.Tail.RootParameter, geometryFactory);
            var halfWidth = Math.Max(0, balloon.Tail.Width) / 2;
            var first = placement.Point - placement.Tangent * halfWidth;
            var second = placement.Point + placement.Tangent * halfWidth;
            var tip = PageToLocal(balloon, balloon.Tail.Tip);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(first, isFilled: true, isClosed: true);
                context.LineTo(tip, isStroked: true, isSmoothJoin: false);
                context.LineTo(second, isStroked: true, isSmoothJoin: false);
            }
            geometry.Freeze();
            return geometry;
        }

        public static BalloonTailPlacement GetRootPlacement(BalloonData balloon, BalloonGeometryFactory? geometryFactory = null)
        {
            if (balloon == null) throw new ArgumentNullException(nameof(balloon));
            if (balloon.Tail == null) throw new InvalidOperationException("Balloon has no tail.");
            var local = GetRootPlacement(balloon.ShapeKind, LocalBounds(balloon), balloon.Tail.RootParameter, geometryFactory);
            return new BalloonTailPlacement(
                LocalToPage(balloon, local.Point),
                Rotate(local.Tangent, balloon.Rotation),
                Rotate(local.OutwardNormal, balloon.Rotation));
        }

        public static BalloonTailPlacement GetRootPlacement(
            BalloonShapeKind shape,
            Rect bounds,
            double rootParameter,
            BalloonGeometryFactory? geometryFactory = null)
        {
            var parameter = Math.Clamp(rootParameter, 0, 1);
            var point = RootPoint(shape, bounds, parameter, geometryFactory);
            var epsilon = 1.0 / 4096;
            var previous = RootPoint(shape, bounds, Wrap(parameter - epsilon), geometryFactory);
            var next = RootPoint(shape, bounds, Wrap(parameter + epsilon), geometryFactory);
            var tangent = Normalize(next - previous, new Vector(1, 0));
            var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            var normal = new Vector(tangent.Y, -tangent.X);
            if (Vector.Multiply(normal, point - center) < 0) normal = -normal;
            normal = Normalize(normal, Normalize(point - center, new Vector(0, 1)));
            return new BalloonTailPlacement(point, tangent, normal);
        }

        public static double FindNearestRootParameter(
            BalloonShapeKind shape,
            Rect bounds,
            Point localPoint,
            BalloonGeometryFactory? geometryFactory = null)
        {
            _ = shape;
            _ = geometryFactory;
            var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            var delta = localPoint - center;
            if (delta.LengthSquared <= 1e-18 || double.IsNaN(delta.LengthSquared)) return 0;
            var angle = Math.Atan2(delta.Y, delta.X);
            return Wrap((angle + Math.PI / 2) / (Math.PI * 2));
        }

        public static BalloonTailData CreateDefault(BalloonData balloon)
        {
            if (balloon == null) throw new ArgumentNullException(nameof(balloon));
            var rootParameter = 0.5;
            var localPlacement = GetRootPlacement(balloon.ShapeKind, LocalBounds(balloon), rootParameter);
            var distance = Math.Max(40, Math.Min(100, Math.Max(balloon.Bounds.Width, balloon.Bounds.Height) * 0.35));
            var localTip = localPlacement.Point + localPlacement.OutwardNormal * distance;
            var pageTip = LocalToPage(balloon, localTip);
            var tail = new BalloonTailData
            {
                RootParameter = rootParameter,
                Width = Math.Max(12, Math.Min(32, Math.Min(balloon.Bounds.Width, balloon.Bounds.Height) * 0.18)),
                Tip = pageTip,
            };
            tail.Validate();
            return tail;
        }

        public static Point GetWidthHandlePoint(BalloonData balloon, BalloonGeometryFactory? geometryFactory = null)
        {
            var placement = GetRootPlacement(balloon, geometryFactory);
            return placement.Point + placement.Tangent * (balloon.Tail!.Width / 2);
        }

        public static Point PageToLocal(BalloonData balloon, Point pagePoint)
        {
            var center = new Point(balloon.X + balloon.Bounds.Width / 2, balloon.Y + balloon.Bounds.Height / 2);
            var unrotated = RotateAround(pagePoint, center, -balloon.Rotation);
            return new Point(unrotated.X - balloon.X, unrotated.Y - balloon.Y);
        }

        public static Point LocalToPage(BalloonData balloon, Point localPoint)
        {
            var pagePoint = new Point(balloon.X + localPoint.X, balloon.Y + localPoint.Y);
            var center = new Point(balloon.X + balloon.Bounds.Width / 2, balloon.Y + balloon.Bounds.Height / 2);
            return RotateAround(pagePoint, center, balloon.Rotation);
        }

        private static Point RootPoint(BalloonShapeKind shape, Rect bounds, double parameter, BalloonGeometryFactory? geometryFactory)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);

            var angle = -Math.PI / 2 + Math.PI * 2 * Wrap(parameter);
            var direction = new Vector(Math.Cos(angle), Math.Sin(angle));
            var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            var body = (geometryFactory ?? SharedGeometryFactory).Create(shape, bounds);
            if (!body.FillContains(center)) return center;

            var low = 0.0;
            var high = Math.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height) + 2;
            for (var index = 0; index < 48; index++)
            {
                var middle = (low + high) / 2;
                var candidate = center + direction * middle;
                if (body.FillContains(candidate)) low = middle;
                else high = middle;
            }
            return center + direction * low;
        }

        private static Rect LocalBounds(BalloonData balloon)
            => new(0, 0, Math.Max(0, balloon.Bounds.Width), Math.Max(0, balloon.Bounds.Height));

        private static Point RotateAround(Point point, Point center, double angle)
        {
            if (angle == 0) return point;
            var radians = angle * Math.PI / 180;
            var cosine = Math.Cos(radians);
            var sine = Math.Sin(radians);
            var x = point.X - center.X;
            var y = point.Y - center.Y;
            return new Point(center.X + x * cosine - y * sine, center.Y + x * sine + y * cosine);
        }

        private static Vector Rotate(Vector vector, double angle)
        {
            if (angle == 0) return vector;
            var radians = angle * Math.PI / 180;
            var cosine = Math.Cos(radians);
            var sine = Math.Sin(radians);
            return new Vector(vector.X * cosine - vector.Y * sine, vector.X * sine + vector.Y * cosine);
        }

        private static Vector Normalize(Vector vector, Vector fallback)
        {
            if (vector.LengthSquared <= 1e-18 || double.IsNaN(vector.LengthSquared)) return fallback;
            vector.Normalize();
            return vector;
        }

        private static double Wrap(double parameter)
        {
            parameter %= 1;
            return parameter < 0 ? parameter + 1 : parameter;
        }
    }

    public readonly struct BalloonTailPlacement
    {
        public BalloonTailPlacement(Point point, Vector tangent, Vector outwardNormal)
        {
            Point = point;
            Tangent = tangent;
            OutwardNormal = outwardNormal;
        }

        public Point Point { get; }
        public Vector Tangent { get; }
        public Vector OutwardNormal { get; }
    }
}
