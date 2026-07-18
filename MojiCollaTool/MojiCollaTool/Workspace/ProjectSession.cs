using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace MojiCollaTool
{
    /// <summary>
    /// 1つの開いているprojectのdocument、asset、履歴、dirty状態を隔離します。
    /// </summary>
    public sealed class ProjectSession : IDisposable, INotifyPropertyChanged
    {
        private long _currentRevision;
        private long _savedRevision;
        private long _nextRevision = 1;
        private Guid? _activePageId;
        private string? _filePath;
        private bool _isClosed;
        private ProjectDocument _lastObservedSnapshot;
        private IReadOnlyList<ProjectAssetRestore> _lastObservedAssets;
        private DateTime _lastChangeAtUtc;
        private string? _lastChangeDescription;
        private string? _lastChangeCoalesceKey;
        private Guid[] _lastChangePages = Array.Empty<Guid>();
        private object _savedHistoryNode;
        private TransactionState? _transaction;

        public ProjectSession(ProjectDocument document, string? filePath = null, ProjectSessionAssetStore? assetStore = null)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            if (document.Pages.Count == 0) throw new ArgumentException("A project must contain at least one page.", nameof(document));
            SessionId = Guid.NewGuid();
            _filePath = NormalizeFilePath(filePath);
            _activePageId = document.Pages[0].PageId;
            AssetStore = assetStore ?? new ProjectSessionAssetStore();
            History = new UndoRedoHistory(document);
            _lastObservedSnapshot = document.CreateHistorySnapshot();
            _lastObservedAssets = CaptureAssets();
            _savedHistoryNode = History.CurrentNode;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? Closed;
        public Guid SessionId { get; }
        public ProjectDocument Document { get; }
        public ProjectSessionAssetStore AssetStore { get; }
        public UndoRedoHistory History { get; }
        public Guid ProjectId => Document.ProjectId;
        public string? FilePath => _filePath;
        public long CurrentRevision => _currentRevision;
        public long Revision => CurrentRevision;
        public long SavedRevision => _savedRevision;
        public bool IsDirty => CurrentRevision != SavedRevision;
        public bool CanUndo => History.CanUndo;
        public bool CanRedo => History.CanRedo;
        public int UndoCount => History.UndoCount;
        public int RedoCount => History.RedoCount;
        public Guid? ActivePageId => _activePageId;
        public PageDocument? ActivePage => _activePageId.HasValue && Document.ContainsPage(_activePageId.Value)
            ? Document.GetPage(_activePageId.Value) : null;
        public bool IsClosed => _isClosed;

        public bool IsPageDirty(Guid pageId)
        {
            if (!IsDirty) return false;
            return History.GetAffectedPageIdsSince(_savedHistoryNode).Contains(pageId);
        }

        public void MarkChanged()
        {
            EnsureOpen();
            var affected = _activePageId.HasValue ? new[] { _activePageId.Value } : Document.Pages.Select(page => page.PageId);
            RecordObservedChange("編集", affected, allowCoalesce: false);
        }

        public void MarkChanged(string description)
        {
            EnsureOpen();
            var affected = _activePageId.HasValue ? new[] { _activePageId.Value } : Document.Pages.Select(page => page.PageId);
            RecordObservedChange(description, affected, allowCoalesce: true);
        }

        public void MarkChanged(Guid pageId) => MarkChanged(pageId, "ページ編集");

        public void MarkChanged(Guid pageId, string description)
            => MarkChanged(pageId, description, coalesceKey: null);

        public void MarkChanged(Guid pageId, string description, string? coalesceKey)
        {
            EnsureOpen();
            Document.GetPage(pageId);
            RecordObservedChange(description, new[] { pageId }, allowCoalesce: true, coalesceKey: coalesceKey);
        }

        public void Execute(Action<ProjectDocument> mutation) => Execute(mutation, "編集");

        public void Execute(Action<ProjectDocument> mutation, string description)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            EnsureOpen();
            var before = _lastObservedSnapshot;
            var beforeActivePageId = _activePageId;
            mutation(Document);
            EnsureActivePage();
            var affected = Document.Pages.Select(page => page.PageId).ToArray();
            RecordObservedChange(description, affected, before, allowCoalesce: true,
                beforeActivePageId: beforeActivePageId);
        }

        public void ExecutePage(Guid pageId, Action<PageDocument> mutation) => ExecutePage(pageId, mutation, "ページ編集", null);

        public void ExecutePage(Guid pageId, Action<PageDocument> mutation, string description)
            => ExecutePage(pageId, mutation, description, null);

        public void ExecutePage(Guid pageId, Action<PageDocument> mutation, string description, string? coalesceKey)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            EnsureOpen();
            var before = _lastObservedSnapshot;
            var beforeActivePageId = _activePageId;
            mutation(Document.GetPage(pageId));
            EnsureActivePage();
            RecordObservedChange(description, new[] { pageId }, before, allowCoalesce: true,
                beforeActivePageId: beforeActivePageId, coalesceKey: coalesceKey);
        }

        public bool Undo()
        {
            EnsureOpen();
            if (!History.CanUndo) return false;
            var entry = History.Undo(Document, restoreState: candidate => RestoreAssets(candidate.BeforeAssets));
            RestoreActivePage(entry.BeforeActivePageId);
            ResetCoalesceWindow();
            ApplyHistoryState();
            return true;
        }

        public bool Redo()
        {
            EnsureOpen();
            if (!History.CanRedo) return false;
            var entry = History.Redo(Document, restoreState: candidate => RestoreAssets(candidate.AfterAssets));
            RestoreActivePage(entry.AfterActivePageId);
            ResetCoalesceWindow();
            ApplyHistoryState();
            return true;
        }

        public IDisposable BeginTransaction(string description = "編集")
        {
            EnsureOpen();
            if (_transaction == null) _transaction = new TransactionState(_lastObservedSnapshot, _lastObservedAssets,
                _activePageId, description);
            else _transaction.Depth++;
            return new TransactionScope(this);
        }

        public void MarkSaved()
        {
            EnsureOpen();
            if (_savedRevision == _currentRevision) return;
            _savedRevision = _currentRevision;
            _savedHistoryNode = History.CurrentNode;
            ResetCoalesceWindow();
            OnPropertyChanged(nameof(SavedRevision));
            OnPropertyChanged(nameof(IsDirty));
        }

        /// <summary>
        /// 旧shell向けのrevision marker API。実際の文書復元はUndo/Redoが担います。
        /// </summary>
        public void RestoreRevision(long revision)
        {
            EnsureOpen();
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            _currentRevision = revision;
            if (revision >= _nextRevision) _nextRevision = checked(revision + 1);
            OnPropertyChanged(nameof(CurrentRevision));
            OnPropertyChanged(nameof(Revision));
            OnPropertyChanged(nameof(IsDirty));
        }

        public void ActivatePage(Guid pageId)
        {
            EnsureOpen();
            Document.GetPage(pageId);
            if (_activePageId == pageId) return;
            _activePageId = pageId;
            OnPropertyChanged(nameof(ActivePageId));
            OnPropertyChanged(nameof(ActivePage));
        }

        public void SetActivePage(Guid pageId) => ActivatePage(pageId);

        public void Dispose()
        {
            if (_isClosed) return;
            _isClosed = true;
            _activePageId = null;
            _transaction = null;
            AssetStore.Dispose();
            OnPropertyChanged(nameof(IsClosed));
            OnPropertyChanged(nameof(ActivePageId));
            OnPropertyChanged(nameof(ActivePage));
            Closed?.Invoke(this, EventArgs.Empty);
        }

        internal void SetFilePath(string? filePath)
        {
            EnsureOpen();
            var normalized = NormalizeFilePath(filePath);
            if (StringComparer.OrdinalIgnoreCase.Equals(_filePath, normalized)) return;
            _filePath = normalized;
            OnPropertyChanged(nameof(FilePath));
        }

        internal static string? NormalizeFilePath(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return null;
            return Path.GetFullPath(filePath);
        }

        private void EndTransaction()
        {
            EnsureOpen();
            if (_transaction == null) return;
            if (_transaction.Depth > 0) { _transaction.Depth--; return; }
            var transaction = _transaction;
            _transaction = null;
            if (transaction.AffectedPageIds.Count > 0)
            {
                RecordObservedChange(transaction.Description, transaction.AffectedPageIds, transaction.Before, transaction.BeforeAssets,
                    allowCoalesce: false, beforeActivePageId: transaction.BeforeActivePageId);
            }
        }

        private void RecordObservedChange(string description, IEnumerable<Guid> affectedPageIds,
            ProjectDocument? before = null, IReadOnlyList<ProjectAssetRestore>? beforeAssets = null,
            bool allowCoalesce = true, Guid? beforeActivePageId = null, string? coalesceKey = null)
        {
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
            var affected = affectedPageIds.Distinct().ToArray();
            if (_transaction != null)
            {
                _transaction.AffectedPageIds.UnionWith(affected);
                _lastObservedSnapshot = Document.CreateHistorySnapshot();
                _lastObservedAssets = CaptureAssets();
                return;
            }

            beforeAssets ??= _lastObservedAssets;
            var afterAssets = CaptureAssets();
            var afterActivePageId = _activePageId;
            var canCoalesce = allowCoalesce && _currentRevision != _savedRevision &&
                string.Equals(_lastChangeDescription, description, StringComparison.Ordinal) &&
                string.Equals(_lastChangeCoalesceKey, coalesceKey, StringComparison.Ordinal) &&
                _lastChangePages.SequenceEqual(affected) &&
                DateTime.UtcNow - _lastChangeAtUtc <= TimeSpan.FromMilliseconds(500);
            if (canCoalesce && History.TryCoalesceLast(Document, description, affected, afterAssets,
                afterActivePageId, coalesceKey, TimeSpan.FromMilliseconds(500)))
            {
                _currentRevision = History.CurrentRevision;
            }
            else
            {
                var revision = _nextRevision++;
                History.Record(before ?? _lastObservedSnapshot, Document, description, affected, revision, beforeAssets, afterAssets,
                    beforeActivePageId, afterActivePageId, coalesceKey);
                _currentRevision = revision;
            }
            _lastObservedSnapshot = Document.CreateHistorySnapshot();
            _lastObservedAssets = afterAssets;
            _lastChangeAtUtc = DateTime.UtcNow;
            _lastChangeDescription = description;
            _lastChangeCoalesceKey = coalesceKey;
            _lastChangePages = affected;
            RaiseRevisionChanged();
        }

        private void ApplyHistoryState()
        {
            _currentRevision = History.CurrentRevision;
            _lastObservedSnapshot = Document.CreateHistorySnapshot();
            _lastObservedAssets = CaptureAssets();
            RaiseRevisionChanged();
        }

        private void RaiseRevisionChanged()
        {
            OnPropertyChanged(nameof(CurrentRevision));
            OnPropertyChanged(nameof(Revision));
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(UndoCount));
            OnPropertyChanged(nameof(RedoCount));
        }

        private void ResetCoalesceWindow()
        {
            _lastChangeDescription = null;
            _lastChangeCoalesceKey = null;
            _lastChangePages = Array.Empty<Guid>();
            _lastChangeAtUtc = DateTime.MinValue;
        }

        private void EnsureOpen()
        {
            if (_isClosed) throw new ObjectDisposedException(nameof(ProjectSession));
        }

        private void EnsureActivePage()
        {
            if (_activePageId.HasValue && Document.ContainsPage(_activePageId.Value)) return;
            RestoreActivePage(Document.Pages[0].PageId);
        }

        private void RestoreActivePage(Guid? pageId)
        {
            var restoredPageId = pageId.HasValue && Document.ContainsPage(pageId.Value)
                ? pageId
                : Document.Pages[0].PageId;
            if (_activePageId == restoredPageId) return;
            _activePageId = restoredPageId;
            OnPropertyChanged(nameof(ActivePageId));
            OnPropertyChanged(nameof(ActivePage));
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private IReadOnlyList<ProjectAssetRestore> CaptureAssets()
        {
            return Document.Pages.SelectMany(page => AssetStore.GetPageAssets(page.PageId)).ToArray();
        }

        private void RestoreAssets(IReadOnlyList<ProjectAssetRestore> assets)
        {
            AssetStore.SaveImages(Document, assets);
        }

        private sealed class TransactionState
        {
            public TransactionState(ProjectDocument before, IReadOnlyList<ProjectAssetRestore> beforeAssets,
                Guid? beforeActivePageId, string description)
            {
                Before = before;
                BeforeAssets = beforeAssets;
                BeforeActivePageId = beforeActivePageId;
                Description = description;
            }
            public ProjectDocument Before { get; }
            public IReadOnlyList<ProjectAssetRestore> BeforeAssets { get; }
            public Guid? BeforeActivePageId { get; }
            public string Description { get; }
            public HashSet<Guid> AffectedPageIds { get; } = new HashSet<Guid>();
            public int Depth { get; set; }
        }

        private sealed class TransactionScope : IDisposable
        {
            private ProjectSession? _session;
            public TransactionScope(ProjectSession session) => _session = session;
            public void Dispose() { var session = _session; _session = null; session?.EndTransaction(); }
        }
    }
}
