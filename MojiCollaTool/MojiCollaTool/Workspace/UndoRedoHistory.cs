using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MojiCollaTool
{
    /// <summary>
    /// ProjectSession内だけで有効な、保存対象外の履歴エントリです。
    /// </summary>
    public sealed class HistoryEntry
    {
        internal HistoryEntry(string description, long revision, ProjectHistoryState before, ProjectHistoryState after,
            IEnumerable<Guid> affectedPageIds, long estimatedBytes,
            IReadOnlyList<ProjectAssetRestore>? beforeAssets = null,
            IReadOnlyList<ProjectAssetRestore>? afterAssets = null)
        {
            Description = description;
            Revision = revision;
            Before = before;
            After = after;
            AffectedPageIds = new ReadOnlyCollection<Guid>(affectedPageIds.Distinct().ToArray());
            EstimatedBytes = estimatedBytes;
            BeforeAssets = beforeAssets ?? Array.Empty<ProjectAssetRestore>();
            AfterAssets = afterAssets ?? Array.Empty<ProjectAssetRestore>();
            CreatedAtUtc = DateTime.UtcNow;
        }

        public string Description { get; }
        public string OperationName => Description;
        public long Revision { get; }
        public IReadOnlyList<Guid> AffectedPageIds { get; }
        public long EstimatedBytes { get; }
        internal IReadOnlyList<ProjectAssetRestore> BeforeAssets { get; }
        internal IReadOnlyList<ProjectAssetRestore> AfterAssets { get; }
        internal DateTime CreatedAtUtc { get; }

        internal ProjectHistoryState Before { get; }
        internal ProjectHistoryState After { get; }
    }

    /// <summary>
    /// 1つのProjectDocument専用のbounded undo/redo履歴です。
    /// redo branchを新しい編集時に切り離し、件数と推定byteの両方でtrimします。
    /// </summary>
    public sealed class UndoRedoHistory
    {
        private sealed class HistoryNode
        {
            public HistoryNode(HistoryNode? parent, long revision, IEnumerable<Guid> affectedPageIds)
            {
                Parent = parent;
                Revision = revision;
                AffectedPageIds = new HashSet<Guid>(affectedPageIds);
            }

            public HistoryNode? Parent { get; }
            public long Revision { get; }
            public HashSet<Guid> AffectedPageIds { get; }
        }

        private readonly List<HistoryEntry> _undoStack = new List<HistoryEntry>();
        private readonly List<HistoryEntry> _redoStack = new List<HistoryEntry>();
        private readonly Guid _projectId;
        private HistoryNode _current;
        private long _nextRevision = 1;

        public UndoRedoHistory(ProjectDocument initialDocument, int maxEntries = 100, long maxBytes = 64L * 1024 * 1024)
        {
            if (initialDocument == null) throw new ArgumentNullException(nameof(initialDocument));
            if (maxEntries < 1) throw new ArgumentOutOfRangeException(nameof(maxEntries));
            if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            _projectId = initialDocument.ProjectId;
            MaxEntries = maxEntries;
            MaxBytes = maxBytes;
            _current = new HistoryNode(null, 0, Array.Empty<Guid>());
        }

        public int MaxEntries { get; }
        public long MaxBytes { get; }
        public bool CanUndo => _undoStack.Count != 0;
        public bool CanRedo => _redoStack.Count != 0;
        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;
        public int Count => _undoStack.Count + _redoStack.Count;
        public long EstimatedBytes => _undoStack.Concat(_redoStack).Sum(entry => entry.EstimatedBytes);
        public IReadOnlyList<HistoryEntry> UndoEntries => new ReadOnlyCollection<HistoryEntry>(_undoStack);
        public IReadOnlyList<HistoryEntry> RedoEntries => new ReadOnlyCollection<HistoryEntry>(_redoStack);
        public long CurrentRevision => _current.Revision;
        internal object CurrentNode => _current;

        public HistoryEntry Execute(ProjectDocument document, Action<ProjectDocument> mutation,
            string description = "編集", IEnumerable<Guid>? affectedPageIds = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            EnsureProject(document);
            var initialAffected = affectedPageIds?.ToArray() ?? document.Pages.Select(page => page.PageId).ToArray();
            var before = document.CreateHistoryState(initialAffected);
            mutation(document);
            var affected = initialAffected.Union(document.Pages.Select(page => page.PageId).Where(pageId => !initialAffected.Contains(pageId))).ToArray();
            var after = document.CreateHistoryState(affected);
            return Record(before, after, description, affected, _nextRevision++);
        }

        internal HistoryEntry Record(ProjectDocument before, ProjectDocument after,
            string description, IEnumerable<Guid> affectedPageIds, long revision,
            IReadOnlyList<ProjectAssetRestore>? beforeAssets = null,
            IReadOnlyList<ProjectAssetRestore>? afterAssets = null)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
            if (revision <= _current.Revision) throw new ArgumentOutOfRangeException(nameof(revision));
            var affected = affectedPageIds?.Distinct().ToHashSet() ?? throw new ArgumentNullException(nameof(affectedPageIds));
            // A page that exists on only one side must be copied even when the caller
            // supplied only the post-edit page IDs (the common delete-page case).
            affected.UnionWith(before.Pages.Select(page => page.PageId).Except(after.Pages.Select(page => page.PageId)));
            affected.UnionWith(after.Pages.Select(page => page.PageId).Except(before.Pages.Select(page => page.PageId)));
            var affectedArray = affected.ToArray();
            var beforeState = before.CreateHistoryState(affectedArray);
            var afterState = after.CreateHistoryState(affectedArray);
            return Record(beforeState, afterState, description, affectedArray, revision, beforeAssets, afterAssets);
        }

        internal HistoryEntry Record(ProjectHistoryState before, ProjectHistoryState after,
            string description, IEnumerable<Guid> affectedPageIds, long revision,
            IReadOnlyList<ProjectAssetRestore>? beforeAssets = null,
            IReadOnlyList<ProjectAssetRestore>? afterAssets = null)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));
            if (before.ProjectId != _projectId || after.ProjectId != _projectId)
            {
                throw new InvalidOperationException("The history state belongs to another project.");
            }
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
            if (revision <= _current.Revision) throw new ArgumentOutOfRangeException(nameof(revision));

            var affected = affectedPageIds?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(affectedPageIds));
            var copiedBeforeAssets = CloneAssets(beforeAssets);
            var copiedAfterAssets = CloneAssets(afterAssets);
            var entry = new HistoryEntry(description, revision, before, after, affected,
                EstimateBytes(before, after, copiedBeforeAssets, copiedAfterAssets),
                copiedBeforeAssets, copiedAfterAssets);

            // Clearing the list drops the only history-owned reference to the abandoned branch.
            _redoStack.Clear();
            _undoStack.Add(entry);
            _current = new HistoryNode(_current, revision, affected);
            if (revision >= _nextRevision) _nextRevision = checked(revision + 1);
            Trim();
            return entry;
        }

        internal bool TryCoalesceLast(ProjectDocument document, string description,
            IEnumerable<Guid> affectedPageIds, IReadOnlyList<ProjectAssetRestore> afterAssets,
            TimeSpan window)
        {
            EnsureProject(document);
            if (_redoStack.Count != 0 || _undoStack.Count == 0) return false;
            var previous = _undoStack[_undoStack.Count - 1];
            if (!string.Equals(previous.Description, description, StringComparison.Ordinal) ||
                DateTime.UtcNow - previous.CreatedAtUtc > window ||
                !new HashSet<Guid>(previous.AffectedPageIds).SetEquals(affectedPageIds)) return false;

            var affected = previous.AffectedPageIds.ToArray();
            var after = document.CreateHistoryState(affected);
            var copiedAfterAssets = CloneAssets(afterAssets);
            _undoStack[_undoStack.Count - 1] = new HistoryEntry(description, previous.Revision,
                previous.Before, after, affected,
                EstimateBytes(previous.Before, after, previous.BeforeAssets, copiedAfterAssets),
                previous.BeforeAssets, copiedAfterAssets);
            return true;
        }

        public HistoryEntry Undo(ProjectDocument target)
        {
            EnsureTarget(target);
            if (!CanUndo) throw new InvalidOperationException("There is no operation to undo.");
            var entry = _undoStack[_undoStack.Count - 1];
            target.RestoreHistoryState(entry.Before);
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _redoStack.Add(entry);
            _current = _current.Parent ?? throw new InvalidOperationException("History parent is missing.");
            return entry;
        }

        public HistoryEntry Redo(ProjectDocument target)
        {
            EnsureTarget(target);
            if (!CanRedo) throw new InvalidOperationException("There is no operation to redo.");
            var entry = _redoStack[_redoStack.Count - 1];
            target.RestoreHistoryState(entry.After);
            _redoStack.RemoveAt(_redoStack.Count - 1);
            _undoStack.Add(entry);
            _current = new HistoryNode(_current, entry.Revision, entry.AffectedPageIds);
            return entry;
        }

        internal IReadOnlySet<Guid> GetAffectedPageIdsSince(object node)
        {
            if (node is not HistoryNode saved) throw new ArgumentException("The node does not belong to this history.", nameof(node));
            var currentPath = GetPath(_current);
            var savedPath = GetPath(saved);
            var common = currentPath.LastOrDefault(savedPath.Contains);
            var affected = new HashSet<Guid>();
            foreach (var item in currentPath.SkipWhile(item => !ReferenceEquals(item, common))) affected.UnionWith(item.AffectedPageIds);
            foreach (var item in savedPath.SkipWhile(item => !ReferenceEquals(item, common))) affected.UnionWith(item.AffectedPageIds);
            return affected;
        }

        private void Trim()
        {
            while (Count > MaxEntries || EstimatedBytes > MaxBytes)
            {
                if (_undoStack.Count > 0) _undoStack.RemoveAt(0);
                else if (_redoStack.Count > 0) _redoStack.RemoveAt(0);
                else break;
            }
        }

        private static long EstimateBytes(ProjectHistoryState before, ProjectHistoryState after,
            IReadOnlyList<ProjectAssetRestore> beforeAssets, IReadOnlyList<ProjectAssetRestore> afterAssets)
        {
            // Includes page/object collection overhead and memento text/metadata. Asset bytes
            // are owned by ProjectSessionAssetStore and are not duplicated by this estimate.
            var assetBytes = beforeAssets.Sum(asset => (long)asset.Content.Length) + afterAssets.Sum(asset => (long)asset.Content.Length);
            return Math.Max(1024L, before.EstimateBytes() + after.EstimateBytes() + assetBytes);
        }

        private static IReadOnlyList<ProjectAssetRestore> CloneAssets(IReadOnlyList<ProjectAssetRestore>? assets)
        {
            if (assets == null || assets.Count == 0) return Array.Empty<ProjectAssetRestore>();
            return assets.Select(asset => new ProjectAssetRestore(asset.PageId, asset.ImageNumber,
                asset.Extension, asset.Content.ToArray())).ToArray();
        }

        private static List<HistoryNode> GetPath(HistoryNode node)
        {
            var path = new List<HistoryNode>();
            for (var current = node; current != null; current = current.Parent) path.Add(current);
            path.Reverse();
            return path;
        }

        private void EnsureTarget(ProjectDocument target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            EnsureProject(target);
        }

        private void EnsureProject(ProjectDocument document)
        {
            if (document.ProjectId != _projectId) throw new InvalidOperationException("The document belongs to another project.");
        }
    }
}
