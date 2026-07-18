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
            IReadOnlyList<ProjectAssetRestore>? afterAssets = null,
            Guid? beforeActivePageId = null,
            Guid? afterActivePageId = null,
            string? coalesceKey = null)
        {
            Description = description;
            Revision = revision;
            Before = before;
            After = after;
            AffectedPageIds = new ReadOnlyCollection<Guid>(affectedPageIds.Distinct().ToArray());
            EstimatedBytes = estimatedBytes;
            BeforeAssets = beforeAssets ?? Array.Empty<ProjectAssetRestore>();
            AfterAssets = afterAssets ?? Array.Empty<ProjectAssetRestore>();
            BeforeActivePageId = beforeActivePageId;
            AfterActivePageId = afterActivePageId;
            CoalesceKey = coalesceKey;
            CreatedAtUtc = DateTime.UtcNow;
        }

        public string Description { get; }
        public string OperationName => Description;
        public long Revision { get; }
        public IReadOnlyList<Guid> AffectedPageIds { get; }
        public long EstimatedBytes { get; }
        internal IReadOnlyList<ProjectAssetRestore> BeforeAssets { get; }
        internal IReadOnlyList<ProjectAssetRestore> AfterAssets { get; }
        internal Guid? BeforeActivePageId { get; }
        internal Guid? AfterActivePageId { get; }
        internal string? CoalesceKey { get; }
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

            public HistoryNode? Parent { get; set; }
            public long Revision { get; }
            public HashSet<Guid> AffectedPageIds { get; }
        }

        private readonly List<HistoryEntry> _undoStack = new List<HistoryEntry>();
        private readonly List<HistoryEntry> _redoStack = new List<HistoryEntry>();
        private readonly Guid _projectId;
        private readonly Dictionary<HistoryEntry, HistoryNode> _nodes = new Dictionary<HistoryEntry, HistoryNode>();
        private readonly HistoryNode _root;
        private HistoryNode _current;
        private HistoryNode _savedNode;
        private long _nextRevision = 1;

        public UndoRedoHistory(ProjectDocument initialDocument, int maxEntries = 100, long maxBytes = 64L * 1024 * 1024)
        {
            if (initialDocument == null) throw new ArgumentNullException(nameof(initialDocument));
            if (maxEntries < 1) throw new ArgumentOutOfRangeException(nameof(maxEntries));
            if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            _projectId = initialDocument.ProjectId;
            MaxEntries = maxEntries;
            MaxBytes = maxBytes;
            _root = new HistoryNode(null, 0, Array.Empty<Guid>());
            _current = _root;
            _savedNode = _root;
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
        internal int RetainedEntryCount => _nodes.Count;
        internal int RetainedNodeCount
        {
            get
            {
                var nodes = new HashSet<HistoryNode>();
                AddReachableNodes(_current, nodes);
                AddReachableNodes(_savedNode, nodes);
                foreach (var node in _nodes.Values) AddReachableNodes(node, nodes);
                return nodes.Count;
            }
        }

        internal void MarkSavedNode(object node)
        {
            if (node is not HistoryNode saved) throw new ArgumentException("The node does not belong to this history.", nameof(node));
            _savedNode = saved;
        }

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
            IReadOnlyList<ProjectAssetRestore>? afterAssets = null,
            Guid? beforeActivePageId = null,
            Guid? afterActivePageId = null,
            string? coalesceKey = null)
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
            return Record(beforeState, afterState, description, affectedArray, revision, beforeAssets, afterAssets,
                beforeActivePageId, afterActivePageId, coalesceKey);
        }

        internal HistoryEntry Record(ProjectHistoryState before, ProjectHistoryState after,
            string description, IEnumerable<Guid> affectedPageIds, long revision,
            IReadOnlyList<ProjectAssetRestore>? beforeAssets = null,
            IReadOnlyList<ProjectAssetRestore>? afterAssets = null,
            Guid? beforeActivePageId = null,
            Guid? afterActivePageId = null,
            string? coalesceKey = null)
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
                copiedBeforeAssets, copiedAfterAssets, beforeActivePageId, afterActivePageId, coalesceKey);

            // Clearing the list drops the only history-owned reference to the abandoned branch.
            foreach (var abandoned in _redoStack) DetachNode(abandoned);
            _redoStack.Clear();
            _undoStack.Add(entry);
            _current = new HistoryNode(_current, revision, affected);
            _nodes[entry] = _current;
            if (revision >= _nextRevision) _nextRevision = checked(revision + 1);
            Trim();
            return entry;
        }

        internal bool TryCoalesceLast(ProjectDocument document, string description,
            IEnumerable<Guid> affectedPageIds, IReadOnlyList<ProjectAssetRestore> afterAssets,
            Guid? afterActivePageId, string? coalesceKey, TimeSpan window)
        {
            EnsureProject(document);
            if (_redoStack.Count != 0 || _undoStack.Count == 0) return false;
            var previous = _undoStack[_undoStack.Count - 1];
            if (!string.Equals(previous.Description, description, StringComparison.Ordinal) ||
                DateTime.UtcNow - previous.CreatedAtUtc > window ||
                !new HashSet<Guid>(previous.AffectedPageIds).SetEquals(affectedPageIds) ||
                !string.Equals(previous.CoalesceKey, coalesceKey, StringComparison.Ordinal)) return false;

            var affected = previous.AffectedPageIds.ToArray();
            var after = document.CreateHistoryState(affected);
            var copiedAfterAssets = CloneAssets(afterAssets);
            _undoStack[_undoStack.Count - 1] = new HistoryEntry(description, previous.Revision,
                previous.Before, after, affected,
                EstimateBytes(previous.Before, after, previous.BeforeAssets, copiedAfterAssets),
                previous.BeforeAssets, copiedAfterAssets, previous.BeforeActivePageId, afterActivePageId, coalesceKey);
            var node = _nodes[previous];
            _nodes.Remove(previous);
            _nodes[_undoStack[_undoStack.Count - 1]] = node;
            return true;
        }

        public HistoryEntry Undo(ProjectDocument target)
            => Undo(target, restoreState: null);

        internal HistoryEntry Undo(ProjectDocument target, Action<HistoryEntry>? restoreState)
        {
            EnsureTarget(target);
            if (!CanUndo) throw new InvalidOperationException("There is no operation to undo.");
            var entry = _undoStack[_undoStack.Count - 1];
            target.RestoreHistoryState(entry.Before);
            try
            {
                restoreState?.Invoke(entry);
            }
            catch
            {
                target.RestoreHistoryState(entry.After);
                throw;
            }
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _redoStack.Add(entry);
            _current = _current.Parent ?? throw new InvalidOperationException("History parent is missing.");
            return entry;
        }

        public HistoryEntry Redo(ProjectDocument target)
            => Redo(target, restoreState: null);

        internal HistoryEntry Redo(ProjectDocument target, Action<HistoryEntry>? restoreState)
        {
            EnsureTarget(target);
            if (!CanRedo) throw new InvalidOperationException("There is no operation to redo.");
            var entry = _redoStack[_redoStack.Count - 1];
            target.RestoreHistoryState(entry.After);
            try
            {
                restoreState?.Invoke(entry);
            }
            catch
            {
                target.RestoreHistoryState(entry.Before);
                throw;
            }
            _redoStack.RemoveAt(_redoStack.Count - 1);
            _undoStack.Add(entry);
            _current = new HistoryNode(_current, entry.Revision, entry.AffectedPageIds);
            _nodes[entry] = _current;
            return entry;
        }

        internal IReadOnlySet<Guid> GetAffectedPageIdsSince(object node)
        {
            if (node is not HistoryNode saved) throw new ArgumentException("The node does not belong to this history.", nameof(node));
            var currentPath = GetPath(_current);
            var savedPath = GetPath(saved);
            var common = currentPath.LastOrDefault(savedPath.Contains);
            var affected = new HashSet<Guid>();
            var currentStart = common == null ? 0 : currentPath.IndexOf(common) + 1;
            var savedStart = common == null ? 0 : savedPath.IndexOf(common) + 1;
            foreach (var item in currentPath.Skip(currentStart)) affected.UnionWith(item.AffectedPageIds);
            foreach (var item in savedPath.Skip(savedStart)) affected.UnionWith(item.AffectedPageIds);
            return affected;
        }

        private void Trim()
        {
            while (Count > MaxEntries || EstimatedBytes > MaxBytes)
            {
                if (_undoStack.Count > 0)
                {
                    var removed = _undoStack[0];
                    _undoStack.RemoveAt(0);
                    if (_undoStack.Count > 0 && _nodes.TryGetValue(_undoStack[0], out var boundary))
                    {
                        // Reconnect a retained post-save branch to the compact saved anchor.
                        // All entry-owned nodes can then be detached, including saved-path nodes.
                        boundary.Parent = !ReferenceEquals(boundary, _savedNode) && IsSavedAncestorOf(boundary)
                            ? _savedNode : _root;
                    }
                    else
                    {
                        _current.Parent = _root;
                    }
                    DetachNode(removed);
                }
                else if (_redoStack.Count > 0)
                {
                    var removed = _redoStack[0];
                    _redoStack.RemoveAt(0);
                    DetachNode(removed);
                }
                else break;
            }
        }

        private void DetachNode(HistoryEntry entry)
        {
            if (!_nodes.TryGetValue(entry, out var node)) return;
            _nodes.Remove(entry);
            node.Parent = null;
        }

        private bool IsSavedAncestorOf(HistoryNode node)
        {
            for (var current = node; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, _savedNode)) return true;
            }

            return false;
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

        private static void AddReachableNodes(HistoryNode node, ISet<HistoryNode> nodes)
        {
            for (var current = node; current != null && nodes.Add(current); current = current.Parent)
            {
            }
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
