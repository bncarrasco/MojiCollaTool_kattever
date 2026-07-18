using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MojiCollaTool
{
    /// <summary>
    /// A Unicode grapheme cluster and its location in the original UTF-16 string.
    /// </summary>
    public sealed class GraphemeCluster
    {
        internal GraphemeCluster(int index, int utf16Start, int utf16Length, string text)
        {
            Index = index;
            Utf16Start = utf16Start;
            Utf16Length = utf16Length;
            Text = text;
        }

        public int Index { get; }
        public int Utf16Start { get; }
        public int Utf16Length { get; }
        public int Start => Utf16Start;
        public int Length => Utf16Length;
        public string Text { get; }
        public override string ToString() => Text;
    }

    /// <summary>
    /// Unicode-safe text segmentation used by anchors and text editing.
    /// Indices exposed by this service are grapheme indices; ranges are UTF-16
    /// ranges because that is what WPF text APIs consume.
    /// </summary>
    public static class GraphemeService
    {
        public static IReadOnlyList<GraphemeCluster> Segment(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (text.Length == 0) return Array.Empty<GraphemeCluster>();

            var starts = StringInfo.ParseCombiningCharacters(text).ToList();
            // StringInfo is the platform source of truth for combining scripts.
            // The small merge pass covers emoji ZWJ sequences and modifiers on
            // frameworks whose Unicode data predates those sequences.
            var mergedStarts = new List<int>(starts.Count);
            for (var i = 0; i < starts.Count; i++)
            {
                if (mergedStarts.Count == 0 || !ShouldMerge(text, mergedStarts[mergedStarts.Count - 1], starts[i]))
                {
                    mergedStarts.Add(starts[i]);
                }
            }

            var result = new GraphemeCluster[mergedStarts.Count];
            for (var i = 0; i < mergedStarts.Count; i++)
            {
                var start = mergedStarts[i];
                var end = i + 1 < mergedStarts.Count ? mergedStarts[i + 1] : text.Length;
                result[i] = new GraphemeCluster(i, start, end - start, text.Substring(start, end - start));
            }
            return result;
        }

        public static int Count(string text) => Segment(text).Count;

        public static GraphemeCluster GetAt(string text, int graphemeIndex)
        {
            if (graphemeIndex < 0) throw new ArgumentOutOfRangeException(nameof(graphemeIndex));
            var clusters = Segment(text);
            if (graphemeIndex >= clusters.Count) throw new ArgumentOutOfRangeException(nameof(graphemeIndex));
            return clusters[graphemeIndex];
        }

        public static string Slice(string text, int graphemeStart, int graphemeCount)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (graphemeStart < 0) throw new ArgumentOutOfRangeException(nameof(graphemeStart));
            if (graphemeCount < 0) throw new ArgumentOutOfRangeException(nameof(graphemeCount));
            var clusters = Segment(text);
            if (graphemeStart > clusters.Count - graphemeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(graphemeStart));
            }
            if (graphemeCount == 0) return string.Empty;
            var start = clusters[graphemeStart].Utf16Start;
            var end = clusters[graphemeStart + graphemeCount - 1].Utf16Start +
                clusters[graphemeStart + graphemeCount - 1].Utf16Length;
            return text.Substring(start, end - start);
        }

        public static int FindNearest(string text, string clusterText, int preferredIndex)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (clusterText == null) throw new ArgumentNullException(nameof(clusterText));
            var clusters = Segment(text);
            if (clusters.Count == 0) return -1;
            var candidates = clusters.Where(item => item.Text == clusterText).ToArray();
            // Reconnecting to an arbitrary duplicate silently changes the
            // meaning of an attached symbol. Only a unique candidate is safe.
            if (candidates.Length != 1) return -1;
            return candidates[0].Index;
        }

        private static bool ShouldMerge(string text, int previousStart, int currentStart)
        {
            if (currentStart <= previousStart) return false;
            var previous = text.Substring(previousStart, currentStart - previousStart);
            if (previous.EndsWith("\r", StringComparison.Ordinal) && text[currentStart] == '\n') return true;
            if (previous.EndsWith("\u200D", StringComparison.Ordinal) || text[currentStart - 1] == '\u200D') return true;
            // A modifier or variation selector can be exposed as a separate
            // element by older Unicode tables.
            var codePoint = ReadCodePoint(text, currentStart, out _);
            if (codePoint == 0x200D || IsVariationSelector(codePoint) || IsEmojiModifier(codePoint) || IsTag(codePoint)) return true;

            // Regional indicator symbols form flags in pairs.
            var previousCodePoint = ReadCodePoint(text, previousStart, out _);
            if (IsRegionalIndicator(previousCodePoint) && IsRegionalIndicator(codePoint))
            {
                var previousCluster = SegmentWithoutMerge(text.Substring(previousStart, currentStart - previousStart));
                return previousCluster.Count == 1;
            }
            return false;
        }

        private static IReadOnlyList<int> SegmentWithoutMerge(string text)
        {
            return StringInfo.ParseCombiningCharacters(text);
        }

        private static int ReadCodePoint(string text, int index, out int charCount)
        {
            var first = text[index];
            if (char.IsHighSurrogate(first) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                charCount = 2;
                return char.ConvertToUtf32(first, text[index + 1]);
            }
            charCount = 1;
            return first;
        }

        private static bool IsVariationSelector(int codePoint) =>
            (codePoint >= 0xFE00 && codePoint <= 0xFE0F) || (codePoint >= 0xE0100 && codePoint <= 0xE01EF);

        private static bool IsEmojiModifier(int codePoint) => codePoint >= 0x1F3FB && codePoint <= 0x1F3FF;

        private static bool IsTag(int codePoint) => codePoint >= 0xE0020 && codePoint <= 0xE007F;

        private static bool IsRegionalIndicator(int codePoint) => codePoint >= 0x1F1E6 && codePoint <= 0x1F1FF;
    }

    public sealed class GraphemeSegmenter
    {
        public IReadOnlyList<GraphemeCluster> Segment(string text) => GraphemeService.Segment(text);
        public int Count(string text) => GraphemeService.Count(text);
    }
}
