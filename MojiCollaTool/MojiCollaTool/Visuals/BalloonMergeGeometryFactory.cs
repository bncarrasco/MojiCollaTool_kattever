using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace MojiCollaTool
{
    /// <summary>Creates and bounds immutable page-space geometry for balloon merge visuals.</summary>
    public sealed class BalloonMergeGeometryFactory
    {
        public const int DefaultCacheCapacity = 128;

        private readonly BalloonGeometryFactory _balloonFactory;
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> _entries = new();
        private readonly LinkedList<CacheEntry> _lru = new();

        public BalloonMergeGeometryFactory(int cacheCapacity = DefaultCacheCapacity,
            BalloonGeometryFactory? balloonFactory = null)
        {
            if (cacheCapacity < 1) throw new ArgumentOutOfRangeException(nameof(cacheCapacity));
            CacheCapacity = cacheCapacity;
            _balloonFactory = balloonFactory ?? new BalloonGeometryFactory();
        }

        public int CacheCapacity { get; }
        public int CacheCount => _entries.Count;
        public long CacheHits { get; private set; }
        public long CacheMisses { get; private set; }

        public BalloonMergeGeometry Create(BalloonMergeData merge, IEnumerable<BalloonData> members)
        {
            if (merge == null) throw new ArgumentNullException(nameof(merge));
            if (members == null) throw new ArgumentNullException(nameof(members));
            var byId = members.ToDictionary(item => item.ObjectId);
            var ordered = merge.MemberIds.Select(id => byId.TryGetValue(id, out var value)
                ? value
                : throw new InvalidOperationException("Balloon merge geometry member was not found.")).ToArray();
            var key = CreateKey(merge, ordered);
            if (_entries.TryGetValue(key, out var existing))
            {
                CacheHits++;
                _lru.Remove(existing);
                _lru.AddFirst(existing);
                return existing.Value.Geometry;
            }

            CacheMisses++;
            var geometry = Build(merge.MergeId, ordered);
            var node = _lru.AddFirst(new CacheEntry(key, geometry));
            _entries.Add(key, node);
            if (_entries.Count > CacheCapacity)
            {
                var last = _lru.Last!;
                _lru.RemoveLast();
                _entries.Remove(last.Value.Key);
            }
            return geometry;
        }

        public void ClearCache()
        {
            _entries.Clear();
            _lru.Clear();
            CacheHits = 0;
            CacheMisses = 0;
        }

        private BalloonMergeGeometry Build(Guid mergeId, IReadOnlyList<BalloonData> members)
        {
            Geometry? union = null;
            var tails = new List<Geometry>();
            foreach (var member in members)
            {
                var body = TransformToPage(_balloonFactory.Create(member.ShapeKind,
                    new Rect(0, 0, Math.Max(0, member.Bounds.Width), Math.Max(0, member.Bounds.Height))), member);
                union = union == null ? body : Geometry.Combine(union, body, GeometryCombineMode.Union, null);
                if (member.Tail != null)
                    tails.Add(TransformToPage(BalloonTailGeometry.Create(member, _balloonFactory), member));
            }
            union ??= Geometry.Empty;
            if (union.CanFreeze) union.Freeze();
            foreach (var tail in tails) if (tail.CanFreeze) tail.Freeze();
            return new BalloonMergeGeometry(mergeId, union, tails);
        }

        private static Geometry TransformToPage(Geometry source, BalloonData balloon)
        {
            var clone = source.CloneCurrentValue();
            var transforms = new TransformGroup();
            transforms.Children.Add(new RotateTransform(balloon.Rotation,
                balloon.Bounds.Width / 2, balloon.Bounds.Height / 2));
            transforms.Children.Add(new TranslateTransform(balloon.X, balloon.Y));
            clone.Transform = transforms;
            var result = clone.GetFlattenedPathGeometry();
            if (result.CanFreeze) result.Freeze();
            return result;
        }

        private static string CreateKey(BalloonMergeData merge, IReadOnlyList<BalloonData> members)
        {
            var builder = new StringBuilder(256).Append(merge.MergeId.ToString("D"));
            foreach (var item in members)
            {
                builder.Append('|').Append(item.ObjectId.ToString("D"))
                    .Append(':').Append((int)item.ShapeKind)
                    .Append(':').Append(Number(item.X)).Append(':').Append(Number(item.Y))
                    .Append(':').Append(Number(item.Bounds.X)).Append(':').Append(Number(item.Bounds.Y))
                    .Append(':').Append(Number(item.Bounds.Width)).Append(':').Append(Number(item.Bounds.Height))
                    .Append(':').Append(Number(item.Rotation));
                if (item.Tail != null)
                    builder.Append(":T:").Append(item.Tail.TailId.ToString("D"))
                        .Append(':').Append(Number(item.Tail.TipX)).Append(':').Append(Number(item.Tail.TipY))
                        .Append(':').Append(Number(item.Tail.RootParameter)).Append(':').Append(Number(item.Tail.Width));
            }
            return builder.ToString();
        }

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private readonly record struct CacheEntry(string Key, BalloonMergeGeometry Geometry);
    }

    public sealed class BalloonMergeGeometry
    {
        internal BalloonMergeGeometry(Guid mergeId, Geometry body, IReadOnlyList<Geometry> tails)
        {
            MergeId = mergeId;
            Body = body;
            Tails = tails;
            var bounds = body.Bounds;
            foreach (var tail in tails) bounds.Union(tail.Bounds);
            Bounds = IsFinite(bounds) ? bounds : Rect.Empty;
        }

        public Guid MergeId { get; }
        public Geometry Body { get; }
        public IReadOnlyList<Geometry> Tails { get; }
        public Rect Bounds { get; }

        public bool Contains(Point pagePoint, double strokeThickness)
        {
            var pen = new Pen(Brushes.Black, Math.Max(1, strokeThickness));
            return Body.FillContains(pagePoint) || Body.StrokeContains(pen, pagePoint) ||
                Tails.Any(tail => tail.FillContains(pagePoint) || tail.StrokeContains(pen, pagePoint));
        }

        private static bool IsFinite(Rect value)
            => !value.IsEmpty && !(double.IsNaN(value.X) || double.IsInfinity(value.X) ||
                double.IsNaN(value.Y) || double.IsInfinity(value.Y) ||
                double.IsNaN(value.Width) || double.IsInfinity(value.Width) ||
                double.IsNaN(value.Height) || double.IsInfinity(value.Height));
    }
}
