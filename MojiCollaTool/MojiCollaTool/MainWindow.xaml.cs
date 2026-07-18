using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace MojiCollaTool
{
    internal enum PersistenceErrorKind
    {
        Save,
        Load,
        WorkingCommit,
    }

    public partial class MainWindow : Window
    {
        private readonly ApplicationWorkspace _workspace = new ApplicationWorkspace();
        private string? _lastUsedDirectory;
        private bool _refreshingTabs;
        private bool _closing;

        public MainWindow()
        {
            InitializeComponent();
            _lastUsedDirectory = DataIO.GetExeDirPath();
            Title = $"{ProductIdentity.DisplayName} ver{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
            _workspace.PropertyChanged += Workspace_PropertyChanged;
            PageEditor.FileDropped += PageEditor_FileDropped;
            PageEditor.ContentChanged += PageEditor_ContentChanged;
            _workspace.Open(new ProjectDocument());
            BindActivePage();
            RefreshTabs();
        }

        public ApplicationWorkspace Workspace => _workspace;

        public CanvasData CanvasData
        {
            get => PageEditor.CanvasData;
            set => PageEditor.SetCanvasData(value);
        }

        private ProjectSession? ActiveSession => _workspace.ActiveSession;

        private PageDocument? ActivePage => ActiveSession?.ActivePage;

        private void Workspace_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ApplicationWorkspace.Sessions) ||
                e.PropertyName == nameof(ApplicationWorkspace.ActiveSession) ||
                e.PropertyName == nameof(ApplicationWorkspace.ActivePage))
            {
                RefreshTabs();
            }
        }

        private void PageEditor_ContentChanged(object? sender, EventArgs e)
        {
            CommitEditorChanges();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // MojiWindowは直接MojiDataを変更するため、再アクティブ化時に文書へ反映します。
            if (!_closing && ActiveSession != null) CaptureEditorState();
            PageEditor.RefreshMojiList();
        }

        private void ProjectTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshingTabs || e.AddedItems.Count == 0 || e.AddedItems[0] is not TabItem tab || tab.Tag is not ProjectSession session) return;
            CaptureEditorState();
            _workspace.SetActiveSession(session);
            BindActivePage();
            RefreshTabs();
        }

        private void PageTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshingTabs || e.AddedItems.Count == 0 || e.AddedItems[0] is not TabItem tab || tab.Tag is not PageDocument page || ActiveSession == null) return;
            CaptureEditorState();
            ActiveSession.ActivatePage(page.PageId);
            BindActivePage();
            RefreshTabs();
        }

        private void RefreshTabs()
        {
            _refreshingTabs = true;
            try
            {
                ProjectTabs.Items.Clear();
                foreach (var session in _workspace.Sessions)
                {
                    ProjectTabs.Items.Add(new TabItem
                    {
                        Tag = session,
                        Header = CreateTabHeader($"{session.Document.Name}{(session.IsDirty ? "*" : string.Empty)}", session),
                    });
                }
                if (ActiveSession != null)
                {
                    ProjectTabs.SelectedItem = ProjectTabs.Items
                        .OfType<TabItem>()
                        .FirstOrDefault(tab => ReferenceEquals(tab.Tag, ActiveSession));
                }
                else ProjectTabs.SelectedIndex = -1;

                PageTabs.Items.Clear();
                if (ActiveSession != null)
                {
                    foreach (var page in ActiveSession.Document.Pages)
                    {
                        PageTabs.Items.Add(new TabItem
                        {
                            Tag = page,
                            Header = $"{page.Name}{(ActiveSession.IsPageDirty(page.PageId) ? "*" : string.Empty)}",
                        });
                    }
                    PageTabs.SelectedItem = PageTabs.Items
                        .OfType<TabItem>()
                        .FirstOrDefault(tab => ReferenceEquals(tab.Tag, ActivePage));
                }
            }
            finally
            {
                _refreshingTabs = false;
            }
        }

        private StackPanel CreateTabHeader(string text, ProjectSession session)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            var close = new Button { Content = "×", Tag = session, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(4, 0, 4, 0) };
            close.Click += ProjectCloseButton_Click;
            header.Children.Add(close);
            return header;
        }

        private void ProjectCloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProjectSession session) CloseSession(session);
            e.Handled = true;
        }

        private void InitButton_Click(object sender, RoutedEventArgs e)
        {
            CaptureEditorState();
            _workspace.Open(new ProjectDocument());
            RefreshTabs();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var about = new AboutWindow { Owner = this };
            about.ShowDialog();
        }

        private void AddPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveSession == null) return;
            CaptureEditorState();
            var page = ActiveSession.Document.AddPage();
            ActiveSession.ActivatePage(page.PageId);
            ActiveSession.MarkChanged(page.PageId);
            BindActivePage();
            RefreshTabs();
        }

        private void DuplicatePageButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveSession == null || ActivePage == null) return;
            CaptureEditorState();
            var source = ActivePage;
            var clone = ActiveSession.Document.ClonePage(source.PageId);
            try
            {
                ActiveSession.AssetStore.CopyPageAssets(source, clone);
            }
            catch
            {
                ActiveSession.Document.RemovePage(clone.PageId);
                throw;
            }
            ActiveSession.ActivatePage(clone.PageId);
            ActiveSession.MarkChanged(clone.PageId);
            BindActivePage();
            RefreshTabs();
        }

        private void RemovePageButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveSession == null || ActivePage == null) return;
            if (ActiveSession.Document.PageCount == 1)
            {
                ShowInfoDialog("最後の1ページは削除できません。", "ページ削除");
                return;
            }
            var page = ActivePage;
            try
            {
                ActiveSession.AssetStore.RemovePage(page.PageId);
                ActiveSession.Execute(project => project.RemovePage(page.PageId));
                BindActivePage();
                RefreshTabs();
            }
            catch (Exception ex)
            {
                ShowError("ページ削除処理に失敗しました。", ex);
            }
        }

        private void RenamePageButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveSession == null || ActivePage == null) return;
            var name = PromptText("ページ名変更", "ページ名", ActivePage.Name);
            if (name == null) return;
            ActiveSession.Execute(project => project.RenamePage(ActivePage.PageId, name));
            RefreshTabs();
        }

        private void MovePageLeftButton_Click(object sender, RoutedEventArgs e) => MoveActivePage(-1);

        private void MovePageRightButton_Click(object sender, RoutedEventArgs e) => MoveActivePage(1);

        private void MoveActivePage(int delta)
        {
            if (ActiveSession == null || ActivePage == null) return;
            var target = ActivePage.Order + delta;
            if (target < 0 || target >= ActiveSession.Document.PageCount) return;
            ActiveSession.Execute(project => project.MovePage(ActivePage.PageId, target));
            RefreshTabs();
        }

        private void SwapImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = CreateOpenFileDialog("画像ファイル|*.jpg;*.jpeg;*.png");
            if (dialog.ShowDialog() == true) ReplaceImage(dialog.FileName);
        }

        private void ReplaceImage(string filePath)
        {
            if (ActiveSession == null || ActivePage == null) return;
            var session = ActiveSession;
            var page = ActivePage;
            var oldAssets = session.AssetStore.GetPageAssets(page.PageId);
            try
            {
                CaptureEditorState();
                var candidate = session.AssetStore.PrepareImage(filePath);
                session.AssetStore.ReplacePageAssets(page.PageId, new[] { candidate.ToAsset(page.PageId, 1) });
                PageEditor.ClearImage(1);
                PageEditor.ClearImage(2);
                PageEditor.ApplyImage(candidate, 1);
                CanvasData.UpdateCanvasSize();
                PageEditor.UpdateCanvas();
                PageEditor.NotifyContentChanged();
                _lastUsedDirectory = Path.GetDirectoryName(filePath);
            }
            catch (Exception ex)
            {
                try
                {
                    session.AssetStore.ReplacePageAssets(page.PageId, oldAssets);
                    PageEditor.ReloadBoundPage();
                }
                catch (Exception restoreException)
                {
                    ex = new AggregateException(ex, restoreException);
                }
                ShowError("画像入れ替え処理に失敗しました。", ex);
            }
        }

        private void MultiImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = CreateOpenFileDialog("画像ファイル|*.jpg;*.jpeg;*.png");
            if (dialog.ShowDialog() != true || ActiveSession == null || ActivePage == null) return;
            var session = ActiveSession;
            var page = ActivePage;
            var oldAssets = session.AssetStore.GetPageAssets(page.PageId);
            try
            {
                CaptureEditorState();
                var candidate = session.AssetStore.PrepareImage(dialog.FileName);
                var newAssets = oldAssets
                    .Where(asset => asset.ImageNumber != 2)
                    .Append(candidate.ToAsset(page.PageId, 2))
                    .ToArray();
                session.AssetStore.ReplacePageAssets(page.PageId, newAssets);
                PageEditor.ApplyImage(candidate, 2);
                CanvasData.ModifyImageSize();
                CanvasData.UpdateCanvasSize();
                PageEditor.UpdateCanvas();
                PageEditor.NotifyContentChanged();
                _lastUsedDirectory = Path.GetDirectoryName(dialog.FileName);
            }
            catch (Exception ex)
            {
                try
                {
                    session.AssetStore.ReplacePageAssets(page.PageId, oldAssets);
                    PageEditor.ReloadBoundPage();
                }
                catch (Exception restoreException)
                {
                    ex = new AggregateException(ex, restoreException);
                }
                ShowError("2枚目の画像追加処理に失敗しました。", ex);
            }
        }

        private void LoadProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = CreateOpenFileDialog("プロジェクトファイル|*.mctzip");
            if (dialog.ShowDialog() == true) LoadProject(dialog.FileName);
        }

        private void LoadProject(string filePath)
        {
            var normalizedPath = ProjectSession.NormalizeFilePath(filePath);
            if (_workspace.IsPathOpen(normalizedPath))
            {
                var existing = _workspace.Sessions.Single(session => StringComparer.OrdinalIgnoreCase.Equals(session.FilePath, normalizedPath));
                _workspace.SetActiveSession(existing);
                BindActivePage();
                RefreshTabs();
                ShowInfoDialog("このファイルは既に開いているため、既存のタブを表示します。", "プロジェクト読込");
                return;
            }

            var assetStore = new ProjectSessionAssetStore();
            try
            {
                var result = DataIO.ReadProject(filePath, assetStore);
                var session = _workspace.Open(result.Project, normalizedPath, assetStore);
                _workspace.SetActiveSession(session);
                BindActivePage();
                RefreshTabs();
                _lastUsedDirectory = Path.GetDirectoryName(filePath);
                foreach (var warning in result.Warnings) ShowInfoDialog(warning.Message, "移行のお知らせ");
            }
            catch (Exception ex)
            {
                assetStore.Dispose();
                ShowError(GetPersistenceErrorMessage(PersistenceErrorKind.Load), ex);
            }
        }

        private void SaveProjectButton_Click(object sender, RoutedEventArgs e) => SaveActiveSession(showCompletion: true);

        private bool SaveActiveSession(bool showCompletion)
        {
            var session = ActiveSession;
            if (session == null) return true;
            CaptureEditorState();
            var filePath = session.FilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                var dialog = new SaveFileDialog
                {
                    InitialDirectory = _lastUsedDirectory,
                    Filter = "プロジェクトファイル|*.mctzip",
                    FileName = $"MCToolProject{DateTime.Now:yyyyMMdd-HHmmss}.mctzip",
                };
                if (dialog.ShowDialog() != true) return false;
                filePath = dialog.FileName;
            }
            if (_workspace.Sessions.Any(other => !ReferenceEquals(other, session) && StringComparer.OrdinalIgnoreCase.Equals(other.FilePath, ProjectSession.NormalizeFilePath(filePath))))
            {
                ShowInfoDialog("同じファイルを別のプロジェクトとして保存できません。", "プロジェクト保存");
                return false;
            }

            try
            {
                DataIO.WriteVersionedProject(filePath, session.Document, session.AssetStore);
                _workspace.SetFilePath(session, filePath);
                session.MarkSaved();
                _lastUsedDirectory = Path.GetDirectoryName(filePath);
                RefreshTabs();
                if (showCompletion) ShowInfoDialog($"{filePath} プロジェクト保存完了", "プロジェクト保存");
                return true;
            }
            catch (Exception ex)
            {
                ShowError(GetPersistenceErrorMessage(PersistenceErrorKind.Save), ex);
                return false;
            }
        }

        private void AddTextButton_Click(object sender, RoutedEventArgs e) => PageEditor.AddNewMojiPanel();

        private void OutputImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                InitialDirectory = _lastUsedDirectory,
                Filter = "PNG画像|*.png|JPEG画像|*.jpg",
                FileName = $"MojiColla{DateTime.Now:yyyyMMdd-HHmmss}.png",
            };
            if (dialog.ShowDialog() != true) return;
            try
            {
                PageEditor.ExportImage(dialog.FileName, dialog.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    ? new JpegBitmapEncoder()
                    : new PngBitmapEncoder());
                _lastUsedDirectory = Path.GetDirectoryName(dialog.FileName);
                ShowInfoDialog($"{dialog.FileName} 画像出力完了", "画像出力");
            }
            catch (Exception ex)
            {
                ShowError("画像出力処理に失敗しました。", ex);
            }
        }

        public void UpdateCanvas() => PageEditor.UpdateCanvas();

        private void PageEditor_FileDropped(object? sender, PageFileDropEventArgs e)
        {
            switch (Path.GetExtension(e.FilePath).ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                case ".png": ReplaceImage(e.FilePath); break;
                case ".mctzip": LoadProject(e.FilePath); break;
            }
        }

        private void CommitEditorChanges()
        {
            if (ActiveSession == null || ActivePage == null || PageEditor.BoundPage == null) return;
            CaptureEditorState();
            ActiveSession.MarkChanged(ActivePage.PageId);
            RefreshTabs();
        }

        private void CaptureEditorState()
        {
            PageEditor.CapturePage();
        }

        private void BindActivePage()
        {
            if (ActiveSession?.ActivePage == null) return;
            PageEditor.BindPage(ActiveSession.ActivePage, ActiveSession.AssetStore, ActiveSession.AssetStore);
        }

        private void CloseSession(ProjectSession session)
        {
            var policy = ClosePolicyFor(session);
            if (policy == null) return;
            var wasActive = ReferenceEquals(ActiveSession, session);
            CaptureEditorState();
            if (_workspace.CloseSession(session, policy.Value))
            {
                if (wasActive)
                {
                    if (_workspace.ActivePage != null) BindActivePage();
                    else PageEditor.UnbindPage();
                }
                RefreshTabs();
            }
        }

        private CloseSessionPolicy? ClosePolicyFor(ProjectSession session)
        {
            if (!session.IsDirty) return CloseSessionPolicy.DiscardChanges;
            var result = MessageBox.Show("保存されていない変更があります。保存しますか？", "プロジェクトを閉じる", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel) return null;
            if (result == MessageBoxResult.Yes)
            {
                if (!ReferenceEquals(ActiveSession, session))
                {
                    _workspace.SetActiveSession(session);
                    BindActivePage();
                }
                return SaveActiveSession(showCompletion: false) ? CloseSessionPolicy.DiscardChanges : null;
            }
            return CloseSessionPolicy.DiscardChanges;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_closing) return;
            CaptureEditorState();
            foreach (var session in _workspace.Sessions.ToArray())
            {
                if (!ReferenceEquals(ActiveSession, session))
                {
                    _workspace.SetActiveSession(session);
                    BindActivePage();
                }
                var policy = ClosePolicyFor(session);
                if (policy == null)
                {
                    e.Cancel = true;
                    return;
                }
                PageEditor.CapturePage();
                if (!_workspace.CloseSession(session, policy.Value))
                {
                    e.Cancel = true;
                    return;
                }
            }
            _closing = true;
            PageEditor.Dispose();
            _workspace.Dispose();
        }

        private OpenFileDialog CreateOpenFileDialog(string filter) => new() { InitialDirectory = _lastUsedDirectory, Filter = filter };

        private static string? PromptText(string title, string label, string initialValue)
        {
            var window = new Window { Title = title, Width = 360, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock { Text = label });
            var textBox = new TextBox { Text = initialValue, Margin = new Thickness(0, 6, 0, 8) };
            panel.Children.Add(textBox);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "決定", IsDefault = true, Margin = new Thickness(4) };
            var cancel = new Button { Content = "キャンセル", IsCancel = true, Margin = new Thickness(4) };
            ok.Click += (_, _) => { window.DialogResult = true; };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            window.Content = panel;
            return window.ShowDialog() == true ? textBox.Text : null;
        }

        internal static string GetPersistenceErrorMessage(PersistenceErrorKind errorKind) => errorKind switch
        {
            PersistenceErrorKind.Save => "プロジェクト保存処理に失敗しました。",
            PersistenceErrorKind.Load => "プロジェクト読込処理に失敗しました。",
            PersistenceErrorKind.WorkingCommit => "プロジェクト作業データの反映に失敗しました。",
            _ => "プロジェクト処理に失敗しました。",
        };

        internal static string BuildErrorLogMessage(string message, Exception? ex)
        {
            var logMessage = new StringBuilder(message);
            if (ex != null) logMessage.AppendLine().Append(ex);
            return logMessage.ToString();
        }

        internal static string BuildErrorDialogMessage(string message) => message;

        public static void ShowError(string message, Exception? ex = null)
        {
            DataIO.WriteErrorLog(BuildErrorLogMessage(message, ex));
            MessageBox.Show(BuildErrorDialogMessage(message), "エラー", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        public static void ShowInfoDialog(string message, string title = "お知らせ") => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

        public static bool ShowOKCancelDialog(string message) => MessageBox.Show(message, "確認", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
    }
}
