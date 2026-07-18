using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace MojiCollaTool
{
    /// <summary>
    /// Creates the geometry used by balloon visuals. Geometry is immutable and
    /// frozen before it is returned, so a cached instance can safely be shared
    /// by all visuals.
    /// </summary>
    public sealed class BalloonGeometryFactory
    {
        public const int DefaultCacheCapacity = 256;

        private readonly BalloonGeometryCache _cache;

        public BalloonGeometryFactory(int cacheCapacity = DefaultCacheCapacity)
        {
            _cache = new BalloonGeometryCache(cacheCapacity);
        }

        public int CacheCapacity => _cache.Capacity;
        public int CacheCount => _cache.Count;
        public long CacheHits => _cache.Hits;
        public long CacheMisses => _cache.Misses;

        public Geometry Create(BalloonData balloon)
        {
            if (balloon == null) throw new ArgumentNullException(nameof(balloon));
            return _cache.GetOrCreate(balloon.ShapeKind, balloon.Bounds);
        }

        public Geometry Create(BalloonShapeKind shape, Rect bounds)
            => _cache.GetOrCreate(shape, bounds);

        public void ClearCache() => _cache.Clear();

        public static Geometry CreateGeometry(BalloonData balloon)
            => Shared.Create(balloon);

        public static Geometry CreateGeometry(BalloonShapeKind shape, Rect bounds)
            => Shared.Create(shape, bounds);

        private static BalloonGeometryFactory Shared { get; } = new BalloonGeometryFactory();

        private sealed class BalloonGeometryCache
        {
            private readonly Dictionary<GeometryKey, LinkedListNode<CacheEntry>> _entries = new();
            private readonly LinkedList<CacheEntry> _lru = new();

            public BalloonGeometryCache(int capacity)
            {
                if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
                Capacity = capacity;
            }

            public int Capacity { get; }
            public int Count => _entries.Count;
            public long Hits { get; private set; }
            public long Misses { get; private set; }

            public Geometry GetOrCreate(BalloonShapeKind shape, Rect bounds)
            {
                var key = new GeometryKey(shape, bounds);
                if (_entries.TryGetValue(key, out var existing))
                {
                    Hits++;
                    _lru.Remove(existing);
                    _lru.AddFirst(existing);
                    return existing.Value.Geometry;
                }

                Misses++;
                var geometry = BuildGeometry(shape, bounds);
                geometry.Freeze();
                var entry = new CacheEntry(key, geometry);
                var node = _lru.AddFirst(entry);
                _entries.Add(key, node);
                if (_entries.Count > Capacity)
                {
                    var last = _lru.Last!;
                    _lru.RemoveLast();
                    _entries.Remove(last.Value.Key);
                }
                return geometry;
            }

            public void Clear()
            {
                _entries.Clear();
                _lru.Clear();
                Hits = 0;
                Misses = 0;
            }
        }

        private readonly struct GeometryKey : IEquatable<GeometryKey>
        {
            private readonly BalloonShapeKind _shape;
            private readonly long _x;
            private readonly long _y;
            private readonly long _width;
            private readonly long _height;

            public GeometryKey(BalloonShapeKind shape, Rect bounds)
            {
                _shape = shape;
                _x = BitConverter.DoubleToInt64Bits(bounds.X);
                _y = BitConverter.DoubleToInt64Bits(bounds.Y);
                _width = BitConverter.DoubleToInt64Bits(bounds.Width);
                _height = BitConverter.DoubleToInt64Bits(bounds.Height);
            }

            public bool Equals(GeometryKey other)
                => _shape == other._shape && _x == other._x && _y == other._y &&
                   _width == other._width && _height == other._height;

            public override bool Equals(object? obj) => obj is GeometryKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_shape, _x, _y, _width, _height);
        }

        private readonly struct CacheEntry
        {
            public CacheEntry(GeometryKey key, Geometry geometry)
            {
                Key = key;
                Geometry = geometry;
            }

            public GeometryKey Key { get; }
            public Geometry Geometry { get; }
        }

        private static Geometry BuildGeometry(BalloonShapeKind shape, Rect bounds)
        {
            var safe = new Rect(
                bounds.X,
                bounds.Y,
                Math.Max(0, bounds.Width),
                Math.Max(0, bounds.Height));

            // Unknown values deliberately render as a plain rectangle. This
            // keeps an otherwise valid document visible while preserving the
            // original discriminator in BalloonData.
            switch (shape)
            {
                case BalloonShapeKind.Ellipse:
                    return new EllipseGeometry(safe);
                case BalloonShapeKind.RoundedRectangle:
                    return new RectangleGeometry(safe, Math.Min(safe.Width, safe.Height) * 0.14, Math.Min(safe.Width, safe.Height) * 0.14);
                case BalloonShapeKind.Rectangle:
                case BalloonShapeKind.Unknown:
                    return new RectangleGeometry(safe);
                case BalloonShapeKind.Monologue:
                    return CreateMonologueGeometry(safe);
                default:
                    return new RectangleGeometry(safe);
            }
        }

        private static Geometry CreateMonologueGeometry(Rect bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return new RectangleGeometry(bounds);

            var x = bounds.X;
            var y = bounds.Y;
            var w = bounds.Width;
            var h = bounds.Height;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(x + w * 0.08, y), isFilled: true, isClosed: true);
                context.LineTo(new Point(x + w * 0.92, y), true, false);
                context.LineTo(new Point(x + w, y + h * 0.18), true, false);
                context.LineTo(new Point(x + w * 0.92, y + h * 0.38), true, false);
                context.LineTo(new Point(x + w, y + h * 0.58), true, false);
                context.LineTo(new Point(x + w * 0.91, y + h * 0.76), true, false);
                context.LineTo(new Point(x + w * 0.58, y + h), true, false);
                context.LineTo(new Point(x + w * 0.5, y + h * 0.78), true, false);
                context.LineTo(new Point(x + w * 0.12, y + h * 0.82), true, false);
                context.LineTo(new Point(x, y + h * 0.58), true, false);
                context.LineTo(new Point(x + w * 0.06, y + h * 0.38), true, false);
                context.LineTo(new Point(x, y + h * 0.18), true, false);
            }
            return geometry;
        }
    }
}
