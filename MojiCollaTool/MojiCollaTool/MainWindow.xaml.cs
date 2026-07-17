using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Media.Imaging;

namespace MojiCollaTool
{
    internal enum PersistenceErrorKind
    {
        Save,
        Load,
        WorkingCommit,
    }

    /// <summary>
    /// アプリケーションシェル。ページ内の描画・入力はPageEditorControlへ委譲します。
    /// </summary>
    public partial class MainWindow : Window
    {
        private string? lastUsedDirectory;

        public MainWindow()
        {
            InitializeComponent();
            lastUsedDirectory = DataIO.GetExeDirPath();
            Title = $"MojiCollaTool ver{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
            PageEditor.FileDropped += PageEditor_FileDropped;
        }

        public CanvasData CanvasData
        {
            get => PageEditor.CanvasData;
            set => PageEditor.SetCanvasData(value);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // MojiDataは変更通知を持たないため、編集ウィンドウから戻るたびに一覧を再構築します。
            PageEditor.RefreshMojiList();
        }

        private void InitButton_Click(object sender, RoutedEventArgs e)
        {
            if (PageEditor.HasMojiPanels && !ShowOKCancelDialog("文字データが存在しています。削除しても問題ありませんか？")) return;

            var openFileDialog = CreateOpenFileDialog("image files|*.jpg;*.png;");
            if (openFileDialog.ShowDialog() != true) return;

            try
            {
                PageEditor.CloseCanvasEditor();
                PageEditor.RemoveAllMojiPanel();
                CanvasData.Init();
                DataIO.InitWorkingDirectory();
                PageEditor.UnloadImage(1);
                PageEditor.UnloadImage(2);
                PageEditor.LoadImage(openFileDialog.FileName, 1);
                CanvasData.UpdateCanvasSize();
                PageEditor.UpdateCanvas();
                DataIO.CopyImageToWorkingDirectory(1, openFileDialog.FileName);
                lastUsedDirectory = Path.GetDirectoryName(openFileDialog.FileName);
            }
            catch (Exception ex)
            {
                ShowError("初期化エラー", ex);
            }
        }

        private void SwapImageButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = CreateOpenFileDialog("image files|*.jpg;*.png;");
            if (openFileDialog.ShowDialog() == true) SwapImage(openFileDialog.FileName);
        }

        private void SwapImage(string filePath)
        {
            try
            {
                PageEditor.CloseCanvasEditor();
                CanvasData.Init();
                DataIO.DeleteAllWorkingDirImage(filePath);
                PageEditor.UnloadImage(1);
                PageEditor.UnloadImage(2);
                PageEditor.LoadImage(filePath, 1);
                CanvasData.UpdateCanvasSize();
                PageEditor.UpdateCanvas();
                DataIO.CopyImageToWorkingDirectory(1, filePath);
                lastUsedDirectory = Path.GetDirectoryName(filePath);
            }
            catch (Exception ex)
            {
                ShowError("画像入れ替えエラー", ex);
            }
        }

        private void MultiImageButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = CreateOpenFileDialog("image files|*.jpg;*.png;");
            if (openFileDialog.ShowDialog() != true) return;

            if (CanvasData.ImageData1.IsNullData())
            {
                SwapImage(openFileDialog.FileName);
                return;
            }

            try
            {
                PageEditor.CloseCanvasEditor();
                CanvasData.ImageData2.Init();
                DataIO.DeleteWorkingDirImage(2, openFileDialog.FileName);
                PageEditor.UnloadImage(2);
                PageEditor.LoadImage(openFileDialog.FileName, 2);
                CanvasData.ModifyImageSize();
                CanvasData.UpdateCanvasSize();
                PageEditor.UpdateCanvas();
                DataIO.CopyImageToWorkingDirectory(2, openFileDialog.FileName);
                lastUsedDirectory = Path.GetDirectoryName(openFileDialog.FileName);
            }
            catch (Exception ex)
            {
                ShowError("画像追加エラー", ex);
            }
        }

        private void LoadProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (PageEditor.HasMojiPanels && !ShowOKCancelDialog("文字データが存在しています。置き換えても問題ありませんか？")) return;

            var openFileDialog = CreateOpenFileDialog("mctzip project file|*.mctzip");
            if (openFileDialog.ShowDialog() == true) LoadProject(openFileDialog.FileName);
        }

        private void LoadProject(string filePath)
        {
            LegacyProjectData loadedProject;
            try
            {
                // 先に一時領域で全entry/XMLを検証する。失敗時は現在の画面とWorkingを変更しない。
                loadedProject = DataIO.ReadProjectData(filePath);
            }
            catch (Exception ex)
            {
                ShowError(GetPersistenceErrorMessage(PersistenceErrorKind.Load), ex);
                return;
            }

            try
            {
                // 検証済みデータを安全にWorking領域へ反映する。
                DataIO.CommitProjectDataToWorkingDir(loadedProject, DataIO.GetWorkingDirPath());
            }
            catch (Exception ex)
            {
                loadedProject.Dispose();
                ShowError(GetPersistenceErrorMessage(PersistenceErrorKind.WorkingCommit), ex);
                return;
            }

            using (loadedProject)
            {
                try
                {
                    PageEditor.CloseCanvasEditor();
                    PageEditor.RemoveAllMojiPanel();
                    PageEditor.UnloadImage(1);
                    PageEditor.UnloadImage(2);

                    var workingDirImagePath = DataIO.GetWorkingDirImagePath(1);
                    if (!string.IsNullOrEmpty(workingDirImagePath)) PageEditor.LoadImage(workingDirImagePath, 1);
                    workingDirImagePath = DataIO.GetWorkingDirImagePath(2);
                    if (!string.IsNullOrEmpty(workingDirImagePath)) PageEditor.LoadImage(workingDirImagePath, 2);

                    CanvasData = DataIO.ReadCanvasDataFromWorkingDir();
                    PageEditor.UpdateCanvas();
                    foreach (var mojiData in DataIO.ReadMojiDatasFromWorkingDir())
                    {
                        PageEditor.AddMojiPanel(new MojiPanel(mojiData, PageEditor));
                    }

                    lastUsedDirectory = Path.GetDirectoryName(filePath);
                }
                catch (Exception ex)
                {
                    ShowError(GetPersistenceErrorMessage(PersistenceErrorKind.Load), ex);
                }
            }
        }

        private void SaveProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = lastUsedDirectory,
                Filter = "mctzip project file|*.mctzip",
                FileName = $"MCToolProject{DateTime.Now:yyyyMMdd-HHmmss}.mctzip"
            };
            if (saveFileDialog.ShowDialog() != true) return;

            try
            {
                DataIO.WriteWorkingDirToProjectDataFile(saveFileDialog.FileName, PageEditor.MojiDatas, CanvasData);
                lastUsedDirectory = Path.GetDirectoryName(saveFileDialog.FileName);
                ShowInfoDialog($"{saveFileDialog.FileName} プロジェクト保存完了");
            }
            catch (Exception ex)
            {
                ShowError(GetPersistenceErrorMessage(PersistenceErrorKind.Save), ex);
            }
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

        private void AddTextButton_Click(object sender, RoutedEventArgs e) => PageEditor.AddNewMojiPanel();

        private void OutputImageButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = lastUsedDirectory,
                Filter = "png file|*.png|jpg file|*.jpg",
                FileName = $"MojiColla{DateTime.Now:yyyyMMdd-HHmmss}.png"
            };
            if (saveFileDialog.ShowDialog() != true) return;

            try
            {
                if (saveFileDialog.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    PageEditor.ExportImage(saveFileDialog.FileName, new JpegBitmapEncoder());
                else
                    PageEditor.ExportImage(saveFileDialog.FileName, new PngBitmapEncoder());

                lastUsedDirectory = Path.GetDirectoryName(saveFileDialog.FileName);
                ShowInfoDialog($"{saveFileDialog.FileName} 画像出力完了");
            }
            catch (Exception ex)
            {
                ShowError("画像出力エラー", ex);
            }
        }

        public void UpdateCanvas() => PageEditor.UpdateCanvas();

        private void PageEditor_FileDropped(object? sender, PageFileDropEventArgs e)
        {
            switch (Path.GetExtension(e.FilePath).ToLowerInvariant())
            {
                case ".jpg":
                case ".png":
                    SwapImage(e.FilePath);
                    break;
                case ".mctzip":
                    if (!PageEditor.HasMojiPanels || ShowOKCancelDialog("文字データが存在しています。置き換えても問題ありませんか？"))
                        LoadProject(e.FilePath);
                    break;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (PageEditor.HasMojiPanels && !ShowOKCancelDialog("文字データが存在しています。終了しても問題ありませんか？"))
            {
                e.Cancel = true;
                return;
            }

            PageEditor.Dispose();
        }

        private OpenFileDialog CreateOpenFileDialog(string filter) => new()
        {
            InitialDirectory = lastUsedDirectory,
            Filter = filter
        };

        public static void ShowError(string message, Exception? ex = null)
        {
            DataIO.WriteErrorLog(BuildErrorLogMessage(message, ex));
            MessageBox.Show(BuildErrorDialogMessage(message), "エラー", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        public static void ShowInfoDialog(string message) =>
            MessageBox.Show(message, "インフォメーション", MessageBoxButton.OK, MessageBoxImage.Information);

        public static bool ShowOKCancelDialog(string message) =>
            MessageBox.Show(message, "確認", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
    }
}
