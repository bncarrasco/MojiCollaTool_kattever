using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace MojiCollaTool
{
    /// <summary>
    /// application内で開いているproject sessionを隔離して管理します。
    /// </summary>
    public sealed class ApplicationWorkspace : INotifyPropertyChanged, IDisposable
    {
        private readonly List<ProjectSession> _sessions = new List<ProjectSession>();
        private readonly ReadOnlyCollection<ProjectSession> _sessionsView;
        private ProjectSession? _activeSession;
        private bool _isClosed;

        public ApplicationWorkspace()
        {
            _sessionsView = new ReadOnlyCollection<ProjectSession>(_sessions);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler<ProjectSessionEventArgs>? SessionOpened;

        public event EventHandler<ProjectSessionEventArgs>? SessionClosed;

        public event EventHandler? ActiveSessionChanged;

        public IReadOnlyList<ProjectSession> Sessions => _sessionsView;

        public IReadOnlyList<ProjectSession> ProjectSessions => Sessions;

        public int SessionCount => _sessions.Count;

        public ProjectSession? ActiveSession => _activeSession;

        public ProjectSession? ActiveProject => ActiveSession;

        public PageDocument? ActivePage => ActiveSession?.ActivePage;

        public ProjectSession Open(ProjectDocument document, string? filePath = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var session = new ProjectSession(document, filePath);
            AddSession(session);
            return session;
        }

        public ProjectSession OpenSession(ProjectDocument document, string? filePath = null)
            => Open(document, filePath);

        public void AddSession(ProjectSession session)
        {
            EnsureOpen();
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.IsClosed) throw new ObjectDisposedException(nameof(session));
            if (_sessions.Contains(session)) throw new InvalidOperationException("The session is already open.");
            if (_sessions.Any(candidate => candidate.ProjectId == session.ProjectId))
            {
                throw new InvalidOperationException("A project with the same ID is already open.");
            }
            if (IsPathOpen(session.FilePath))
            {
                throw new InvalidOperationException("The file is already open in this workspace.");
            }

            _sessions.Add(session);
            session.PropertyChanged += OnSessionPropertyChanged;
            SessionOpened?.Invoke(this, new ProjectSessionEventArgs(session));
            OnPropertyChanged(nameof(Sessions));
            OnPropertyChanged(nameof(ProjectSessions));
            OnPropertyChanged(nameof(SessionCount));

            if (_activeSession == null) SetActiveSession(session);
        }

        public bool SetActiveSession(ProjectSession session)
        {
            EnsureOpen();
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!_sessions.Contains(session)) return false;
            if (ReferenceEquals(_activeSession, session)) return true;

            SetActiveSessionCore(session);
            return true;
        }

        public bool SetActiveSession(Guid projectId)
        {
            var session = _sessions.SingleOrDefault(candidate => candidate.ProjectId == projectId);
            return session != null && SetActiveSession(session);
        }

        public bool IsPathOpen(string? filePath)
        {
            var normalized = ProjectSession.NormalizeFilePath(filePath);
            return normalized != null && _sessions.Any(session =>
                StringComparer.OrdinalIgnoreCase.Equals(session.FilePath, normalized));
        }

        public void SetFilePath(ProjectSession session, string? filePath)
        {
            EnsureOpen();
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!_sessions.Contains(session)) throw new InvalidOperationException("The session is not open.");

            var normalized = ProjectSession.NormalizeFilePath(filePath);
            if (normalized != null && _sessions.Any(candidate =>
                    !ReferenceEquals(candidate, session) &&
                    StringComparer.OrdinalIgnoreCase.Equals(candidate.FilePath, normalized)))
            {
                throw new InvalidOperationException("The file is already open in this workspace.");
            }

            session.SetFilePath(normalized);
        }

        public bool CanCloseSession(ProjectSession session, CloseSessionPolicy policy = CloseSessionPolicy.RejectIfDirty)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!_sessions.Contains(session)) return false;
            return !session.IsDirty || policy != CloseSessionPolicy.RejectIfDirty;
        }

        public bool CloseSession(ProjectSession session, CloseSessionPolicy policy = CloseSessionPolicy.RejectIfDirty)
        {
            EnsureOpen();
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!CanCloseSession(session, policy)) return false;

            var wasActive = ReferenceEquals(_activeSession, session);
            session.PropertyChanged -= OnSessionPropertyChanged;
            _sessions.Remove(session);
            session.Dispose();
            SessionClosed?.Invoke(this, new ProjectSessionEventArgs(session));
            OnPropertyChanged(nameof(Sessions));
            OnPropertyChanged(nameof(ProjectSessions));
            OnPropertyChanged(nameof(SessionCount));

            if (wasActive) SetActiveSessionCore(_sessions.LastOrDefault());

            return true;
        }

        public bool TryCloseSession(ProjectSession session, CloseSessionPolicy policy = CloseSessionPolicy.RejectIfDirty)
            => CloseSession(session, policy);

        public void Dispose()
        {
            if (_isClosed) return;

            foreach (var session in _sessions.ToArray())
            {
                CloseSession(session, CloseSessionPolicy.AllowDirty);
            }

            _isClosed = true;
        }

        private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is ProjectSession session && ReferenceEquals(session, _activeSession))
            {
                if (e.PropertyName == nameof(ProjectSession.ActivePageId) ||
                    e.PropertyName == nameof(ProjectSession.ActivePage))
                {
                    OnPropertyChanged(nameof(ActivePage));
                }
            }

            OnPropertyChanged(nameof(Sessions));
        }

        private void SetActiveSessionCore(ProjectSession? session)
        {
            if (ReferenceEquals(_activeSession, session)) return;

            _activeSession = session;
            OnPropertyChanged(nameof(ActiveSession));
            OnPropertyChanged(nameof(ActiveProject));
            OnPropertyChanged(nameof(ActivePage));
            ActiveSessionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void EnsureOpen()
        {
            if (_isClosed) throw new ObjectDisposedException(nameof(ApplicationWorkspace));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
