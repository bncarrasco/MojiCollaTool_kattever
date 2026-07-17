using System;
using System.ComponentModel;
using System.IO;

namespace MojiCollaTool
{
    /// <summary>
    /// 開いている一つのprojectに属するshell-facing stateです。
    /// serializerやWPF controlを所有せず、documentのライフサイクルだけを管理します。
    /// </summary>
    public sealed class ProjectSession : IDisposable, INotifyPropertyChanged
    {
        private long _currentRevision;
        private long _savedRevision;
        private long _nextRevision = 1;
        private Guid? _activePageId;
        private string? _filePath;
        private bool _isClosed;

        public ProjectSession(ProjectDocument document, string? filePath = null)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            SessionId = Guid.NewGuid();
            _filePath = NormalizeFilePath(filePath);
            _activePageId = document.Pages[0].PageId;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler? Closed;

        public Guid SessionId { get; }

        public ProjectDocument Document { get; }

        public Guid ProjectId => Document.ProjectId;

        public string? FilePath => _filePath;

        public long CurrentRevision => _currentRevision;

        public long Revision => CurrentRevision;

        public long SavedRevision => _savedRevision;

        public bool IsDirty => CurrentRevision != SavedRevision;

        public Guid? ActivePageId => _activePageId;

        public PageDocument? ActivePage => _activePageId.HasValue && Document.ContainsPage(_activePageId.Value)
            ? Document.GetPage(_activePageId.Value)
            : null;

        public bool IsClosed => _isClosed;

        /// <summary>
        /// 文書変更を、過去へ復元しても再利用されないrevision tokenへ反映します。
        /// </summary>
        public void MarkChanged()
        {
            EnsureOpen();
            _currentRevision = _nextRevision;
            _nextRevision = checked(_nextRevision + 1);
            OnPropertyChanged(nameof(CurrentRevision));
            OnPropertyChanged(nameof(Revision));
            OnPropertyChanged(nameof(IsDirty));
        }

        public void Execute(Action<ProjectDocument> mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            EnsureOpen();
            mutation(Document);
            EnsureActivePage();
            MarkChanged();
        }

        public void ExecutePage(Guid pageId, Action<PageDocument> mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            EnsureOpen();
            mutation(Document.GetPage(pageId));
            EnsureActivePage();
            MarkChanged();
        }

        public void MarkSaved()
        {
            EnsureOpen();
            if (_savedRevision == _currentRevision) return;

            _savedRevision = _currentRevision;
            OnPropertyChanged(nameof(SavedRevision));
            OnPropertyChanged(nameof(IsDirty));
        }

        /// <summary>
        /// Undo/redo serviceが現在位置を復元するときに使うrevision markerです。
        /// </summary>
        public void RestoreRevision(long revision)
        {
            EnsureOpen();
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));

            _currentRevision = revision;
            if (revision >= _nextRevision)
            {
                _nextRevision = checked(revision + 1);
            }
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

        private void EnsureOpen()
        {
            if (_isClosed) throw new ObjectDisposedException(nameof(ProjectSession));
        }

        private void EnsureActivePage()
        {
            if (_activePageId.HasValue && Document.ContainsPage(_activePageId.Value)) return;

            _activePageId = Document.Pages[0].PageId;
            OnPropertyChanged(nameof(ActivePageId));
            OnPropertyChanged(nameof(ActivePage));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
