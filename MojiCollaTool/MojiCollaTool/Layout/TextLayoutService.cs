using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MojiCollaTool
{
    /// <summary>
    /// A visual-only line/column plan.  It deliberately contains the original
    /// grapheme instances and never writes back to MojiData.FullText.
    /// </summary>
    public sealed class TextLayoutLine
    {
        internal TextLayoutLine(IReadOnlyList<GraphemeCluster> clusters, bool explicitBreak, double advance, double crossSize)
        {
            Clusters = clusters;
            IsExplicitBreak = explicitBreak;
            Advance = advance;
            CrossSize = crossSize;
        }

        public IReadOnlyList<GraphemeCluster> Clusters { get; }
        public bool IsExplicitBreak { get; }
        public double Advance { get; }
        public double CrossSize { get; }
        public string Text => string.Concat(Clusters.Select(cluster => cluster.Text));
    }

    public sealed class TextLayoutRequest
    {
        public string FullText { get; set; } = string.Empty;
        public TextDirection Direction { get; set; } = TextDirection.Yokogaki;
        public double FrameWidth { get; set; } = double.PositiveInfinity;
        public double FrameHeight { get; set; } = double.PositiveInfinity;
        public double Padding { get; set; }
        public double FontSize { get; set; } = 50;
        public double MinimumFontSize { get; set; } = 8;
        public BalloonTextAlignment Alignment { get; set; } = BalloonTextAlignment.Center;
        public string FontFamilyName { get; set; } = "ＭＳ ゴシック";
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public double CharacterMargin { get; set; }
        public double LineMargin { get; set; }

        public TextLayoutRequest Clone() => (TextLayoutRequest)MemberwiseClone();

        public static TextLayoutRequest From(MojiData text, TextLinkData link, BalloonData balloon)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (link == null) throw new ArgumentNullException(nameof(link));
            if (balloon == null) throw new ArgumentNullException(nameof(balloon));
            return new TextLayoutRequest
            {
                FullText = text.FullText,
                Direction = text.TextDirection,
                FrameWidth = balloon.Bounds.Width,
                FrameHeight = balloon.Bounds.Height,
                Padding = link.Padding,
                FontSize = text.FontSize,
                MinimumFontSize = link.MinimumFontSize,
                Alignment = link.Alignment,
                FontFamilyName = text.FontFamilyName,
                IsBold = text.IsBold,
                IsItalic = text.IsItalic,
                CharacterMargin = text.CharacterMargin,
                LineMargin = text.LineMargin,
            };
        }
    }

    public sealed class TextLayoutResult
    {
        internal TextLayoutResult(TextLayoutRequest request, double effectiveFontSize,
            IReadOnlyList<TextLayoutLine> lines, double contentWidth, double contentHeight,
            bool overflow, bool usedFallbackFont)
        {
            Request = request.Clone();
            EffectiveFontSize = effectiveFontSize;
            Lines = lines;
            ContentWidth = contentWidth;
            ContentHeight = contentHeight;
            Overflow = overflow;
            UsedFallbackFont = usedFallbackFont;
        }

        public TextLayoutRequest Request { get; }
        public TextDirection Direction => Request.Direction;
        public BalloonTextAlignment Alignment => Request.Alignment;
        public double EffectiveFontSize { get; }
        public IReadOnlyList<TextLayoutLine> Lines { get; }
        public IReadOnlyList<TextLayoutLine> Plan => Lines;
        public double ContentWidth { get; }
        public double ContentHeight { get; }
        public double TargetWidth => ContentWidth + 2 * SafePadding(Request.Padding);
        public double TargetHeight => ContentHeight + 2 * SafePadding(Request.Padding);
        public Rect TargetBounds => new Rect(0, 0, TargetWidth, TargetHeight);
        public bool Overflow { get; }
        public bool HasOverflow => Overflow;
        public bool UsedFallbackFont { get; }
        public string? Warning => Overflow ? "文字が枠内に収まりません。最小文字サイズで表示しています。" : null;

        private static double SafePadding(double value) => IsFinite(value) && value >= 0 ? value : 0;
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// Basic, deterministic grapheme-aware layout.  It handles explicit line
    /// breaks, surrogate/combining/ZWJ clusters, and horizontal/vertical wrap.
    /// Language-specific prohibition rules and hyphenation are intentionally not
    /// part of this service.
    /// </summary>
    public sealed class TextLayoutService
    {
        private readonly object _cacheGate = new object();
        private readonly Dictionary<MeasureKey, Size> _measureCache = new Dictionary<MeasureKey, Size>();
        private readonly Queue<MeasureKey> _measureOrder = new Queue<MeasureKey>();
        private readonly int _cacheCapacity;

        public TextLayoutService(int cacheCapacity = 2048)
        {
            if (cacheCapacity < 1) throw new ArgumentOutOfRangeException(nameof(cacheCapacity));
            _cacheCapacity = cacheCapacity;
        }

        public int MeasureCacheCount
        {
            get { lock (_cacheGate) return _measureCache.Count; }
        }

        public int CacheCount => MeasureCacheCount;

        public void ClearCache()
        {
            lock (_cacheGate)
            {
                _measureCache.Clear();
                _measureOrder.Clear();
            }
        }

        public TextLayoutResult FitTextToBalloon(TextLayoutRequest request)
        {
            ValidateRequest(request);
            var upper = NormalizeFontSize(request.FontSize);
            var minimum = Math.Min(upper, NormalizeMinimumFontSize(request.MinimumFontSize));
            var first = LayoutAt(request, upper);
            if (!first.Overflow) return first;

            var lowerResult = LayoutAt(request, minimum);
            if (lowerResult.Overflow || upper - minimum < 0.01) return lowerResult;

            // Overflow is monotonic for this basic wrap strategy.  Keep the
            // largest fitting value while retaining fractional font sizes.
            var low = minimum;
            var high = upper;
            var best = lowerResult;
            for (var i = 0; i < 24; i++)
            {
                var candidate = (low + high) / 2;
                var result = LayoutAt(request, candidate);
                if (result.Overflow) high = candidate;
                else { low = candidate; best = result; }
            }
            return best;
        }

        public TextLayoutResult FitBalloonToText(TextLayoutRequest request)
        {
            ValidateRequest(request);
            return LayoutAt(request, NormalizeFontSize(request.FontSize), ignoreFrame: true);
        }

        public TextLayoutResult Measure(TextLayoutRequest request)
        {
            ValidateRequest(request);
            return LayoutAt(request, NormalizeFontSize(request.FontSize), ignoreFrame: true);
        }

        private TextLayoutResult LayoutAt(TextLayoutRequest source, double fontSize, bool ignoreFrame = false)
        {
            var request = source.Clone();
            request.FontSize = fontSize;
            var padding = SafeNonNegative(request.Padding);
            var maxAdvance = request.Direction == TextDirection.Yokogaki
                ? SafeCapacity(request.FrameWidth, padding)
                : SafeCapacity(request.FrameHeight, padding);
            if (ignoreFrame) maxAdvance = double.PositiveInfinity;

            var clusters = GraphemeService.Segment(request.FullText ?? string.Empty);
            var lines = new List<TextLayoutLine>();
            var current = new List<GraphemeCluster>();
            var currentAdvance = 0d;
            var usedFallback = false;
            var overflow = false;

            void Finish(bool explicitBreak)
            {
                var metrics = MeasureLine(current, request, fontSize, out var fallback);
                usedFallback |= fallback;
                lines.Add(new TextLayoutLine(current.ToArray(), explicitBreak, metrics.advance, metrics.crossSize));
                current = new List<GraphemeCluster>();
                currentAdvance = 0;
            }

            foreach (var cluster in clusters)
            {
                if (IsLineBreak(cluster.Text))
                {
                    Finish(true);
                    continue;
                }

                var metric = MeasureCluster(cluster.Text, request, fontSize);
                usedFallback |= metric.usedFallback;
                var next = current.Count == 0 ? metric.advance : currentAdvance + SafeNonNegative(request.CharacterMargin) + metric.advance;
                if (current.Count > 0 && next > maxAdvance)
                {
                    Finish(false);
                    next = metric.advance;
                }
                current.Add(cluster);
                currentAdvance = next;
                if (metric.advance > maxAdvance) overflow = true;
            }
            Finish(false);
            if (lines.Count == 0) Finish(false);

            var lineMargin = SafeNonNegative(request.LineMargin);
            var contentWidth = 0d;
            var contentHeight = 0d;
            if (request.Direction == TextDirection.Yokogaki)
            {
                contentWidth = lines.Count == 0 ? 0 : lines.Max(line => line.Advance);
                contentHeight = lines.Sum(line => line.CrossSize) + Math.Max(0, lines.Count - 1) * lineMargin;
                if (!ignoreFrame && contentHeight > SafeCapacity(request.FrameHeight, padding)) overflow = true;
            }
            else
            {
                contentWidth = lines.Sum(line => line.CrossSize) + Math.Max(0, lines.Count - 1) * lineMargin;
                contentHeight = lines.Count == 0 ? 0 : lines.Max(line => line.Advance);
                if (!ignoreFrame && contentWidth > SafeCapacity(request.FrameWidth, padding)) overflow = true;
            }

            return new TextLayoutResult(request, fontSize, lines, contentWidth, contentHeight, overflow, usedFallback);
        }

        private (double advance, double crossSize) MeasureLine(IReadOnlyList<GraphemeCluster> line,
            TextLayoutRequest request, double fontSize, out bool usedFallback)
        {
            usedFallback = false;
            if (line.Count == 0)
            {
                var empty = Math.Max(1, fontSize);
                return (empty, empty);
            }

            var advance = 0d;
            var cross = 0d;
            foreach (var cluster in line)
            {
                var metric = MeasureCluster(cluster.Text, request, fontSize);
                usedFallback |= metric.usedFallback;
                advance += metric.advance;
                cross = Math.Max(cross, metric.crossSize);
            }
            advance += Math.Max(0, line.Count - 1) * SafeNonNegative(request.CharacterMargin);
            return (advance, Math.Max(1, cross));
        }

        private (double advance, double crossSize, bool usedFallback) MeasureCluster(string text,
            TextLayoutRequest request, double fontSize)
        {
            var key = new MeasureKey(text, request.FontFamilyName, fontSize, request.IsBold, request.IsItalic,
                request.Direction);
            lock (_cacheGate)
            {
                if (_measureCache.TryGetValue(key, out var cached))
                    return (AxisAdvance(cached, request.Direction), CrossSize(cached, request.Direction), false);
            }

            Size measured;
            var fallback = false;
            try
            {
                var family = string.IsNullOrWhiteSpace(request.FontFamilyName) ? "Segoe UI" : request.FontFamilyName;
                var typeface = new Typeface(new FontFamily(family),
                    request.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                    request.IsBold ? FontWeights.Bold : FontWeights.Normal,
                    FontStretches.Normal);
                var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, fontSize, Brushes.Black, 1.0);
                measured = new Size(Math.Max(1, formatted.WidthIncludingTrailingWhitespace), Math.Max(1, formatted.Height));
            }
            catch
            {
                fallback = true;
                measured = new Size(ApproximateAdvance(text, fontSize), Math.Max(1, fontSize * 1.2));
            }

            lock (_cacheGate)
            {
                if (!_measureCache.ContainsKey(key))
                {
                    _measureCache[key] = measured;
                    _measureOrder.Enqueue(key);
                    while (_measureCache.Count > _cacheCapacity && _measureOrder.Count > 0)
                        _measureCache.Remove(_measureOrder.Dequeue());
                }
            }
            return (AxisAdvance(measured, request.Direction), CrossSize(measured, request.Direction), fallback);
        }

        private static double ApproximateAdvance(string text, double fontSize)
        {
            if (string.IsNullOrEmpty(text)) return Math.Max(1, fontSize);
            var wide = text.Any(character => character > 0xFF || char.IsSurrogate(character));
            return Math.Max(1, fontSize * (wide ? 1 : 0.6));
        }

        private static double AxisAdvance(Size size, TextDirection direction) =>
            direction == TextDirection.Yokogaki ? size.Width : size.Height;

        private static double CrossSize(Size size, TextDirection direction) =>
            direction == TextDirection.Yokogaki ? size.Height : size.Width;

        private static bool IsLineBreak(string text) => text == "\r\n" || text == "\r" || text == "\n";

        private static double SafeCapacity(double frame, double padding)
        {
            if (double.IsPositiveInfinity(frame)) return double.PositiveInfinity;
            if (double.IsNaN(frame) || double.IsNegativeInfinity(frame)) return 0;
            return Math.Max(0, frame - 2 * padding);
        }

        private static double SafeNonNegative(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(0, value);

        private static double NormalizeFontSize(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) || value <= 0 ? 1 : value;

        private static double NormalizeMinimumFontSize(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) || value <= 0 ? 1 : value;

        private static void ValidateRequest(TextLayoutRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.FullText == null) request.FullText = string.Empty;
        }

        private readonly struct MeasureKey : IEquatable<MeasureKey>
        {
            private readonly string _text;
            private readonly string _font;
            private readonly double _size;
            private readonly bool _bold;
            private readonly bool _italic;
            private readonly TextDirection _direction;

            public MeasureKey(string text, string font, double size, bool bold, bool italic, TextDirection direction)
            {
                _text = text;
                _font = font ?? string.Empty;
                _size = size;
                _bold = bold;
                _italic = italic;
                _direction = direction;
            }

            public bool Equals(MeasureKey other) => _text == other._text && _font == other._font &&
                _size.Equals(other._size) && _bold == other._bold && _italic == other._italic && _direction == other._direction;

            public override bool Equals(object? obj) => obj is MeasureKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_text, _font, _size, _bold, _italic, _direction);
        }
    }
}
