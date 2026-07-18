using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MojiCollaTool
{
    /// <summary>
    /// 1ページ分のキャンバス、画像、文字オブジェクト、ページ内操作を保持するコントロールです。
    /// MainWindowはプロジェクト入出力などのアプリケーションシェルに集中します。
    /// </summary>
    public partial class PageEditorControl : UserControl, IDisposable
    {
        private readonly List<MojiPanel> _mojiPanels = new();
        private readonly ObservableCollection<MojiPanel> _viewMojiPanels = new();
        private CanvasEditWindow? _canvasEditWindow;
        private bool _runEvent;
        private bool _isDisposed;
        private PageDocument? _boundPage;
        private IProjectAssetSource? _assetSource;
        private IProjectAssetSink? _assetSink;
        private bool _suppressChanges;

        public PageEditorControl()
        {
            InitializeComponent();
            MojiListView.ItemsSource = _viewMojiPanels;
            ResetScale();
            _runEvent = true;
        }

        public CanvasData CanvasData { get; private set; } = new();

        public Canvas Canvas => MainCanvas;

        public IReadOnlyList<MojiPanel> MojiPanels => _mojiPanels;

        public IEnumerable<MojiData> MojiDatas => _mojiPanels.Select(panel => panel.MojiData);

        public bool HasMojiPanels => _mojiPanels.Count > 0;

        public event EventHandler<PageFileDropEventArgs>? FileDropped;

        public event EventHandler? ContentChanged;

        public string ContentChangeDescription { get; private set; } = "ページ編集";

        public string? ContentChangeCoalesceKey { get; private set; }

        public PageDocument? BoundPage => _boundPage;

        public int ScalePercent => ScalingTextBox.Value;

        public Guid? SelectedObjectId => (MojiListView.SelectedItem as MojiPanel)?.MojiData.ObjectId;

        public void RestoreViewState(int scalePercent, Guid? selectedObjectId)
        {
            _runEvent = false;
            try
            {
                UpdateScale(Math.Max(ScalingTextBox.ValueMinLimit, scalePercent));
                MojiListView.SelectedItem = selectedObjectId.HasValue
                    ? _mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == selectedObjectId.Value)
                    : null;
            }
            finally
            {
                _runEvent = true;
            }
        }

        public void RefreshMojiList()
        {
            _viewMojiPanels.Clear();
            foreach (var mojiPanel in _mojiPanels)
            {
                _viewMojiPanels.Add(mojiPanel);
            }
        }

        public void SetCanvasData(CanvasData canvasData)
        {
            ThrowIfDisposed();
            CanvasData = canvasData ?? throw new ArgumentNullException(nameof(canvasData));
            UpdateCanvas();
            RaiseContentChanged("キャンバス変更");
        }

        public void BindPage(PageDocument page, IProjectAssetSource? assetSource, IProjectAssetSink? assetSink = null)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(page);
            if (ReferenceEquals(_boundPage, page)) return;

            if (_boundPage != null) CapturePage(_boundPage);
            _suppressChanges = true;
            try
            {
                CloseCanvasEditor();
                RemoveAllMojiPanel();
                UnloadImage(1);
                UnloadImage(2);
                _boundPage = page;
                _assetSource = assetSource;
                _assetSink = assetSink;
                CanvasData = page.Canvas.ToLegacyData();
                UpdateCanvas();
                LoadStoredImage(page, 1);
                LoadStoredImage(page, 2);
                foreach (var mojiData in page.MojiDatas)
                {
                    AddMojiPanel(new MojiPanel(PageDocument.CloneMojiData(mojiData), this));
                }
            }
            finally
            {
                _suppressChanges = false;
            }
        }

        public void CapturePage()
        {
            if (_boundPage != null) CapturePage(_boundPage);
        }

        public void UnbindPage()
        {
            if (_boundPage == null) return;
            CapturePage(_boundPage);
            _suppressChanges = true;
            try
            {
                CloseCanvasEditor();
                RemoveAllMojiPanel();
                UnloadImage(1);
                UnloadImage(2);
                _boundPage = null;
                _assetSource = null;
                _assetSink = null;
                CanvasData = new CanvasData();
                UpdateCanvas();
            }
            finally
            {
                _suppressChanges = false;
            }
        }

        internal void ReloadBoundPage()
        {
            if (_boundPage == null) return;
            var page = _boundPage;
            var source = _assetSource;
            var sink = _assetSink;
            _boundPage = null;
            BindPage(page, source, sink);
        }

        public void AddNewMojiPanel() => AddMojiPanel(new MojiPanel(GetNextMojiId(), this));

        public void AddMojiPanel(MojiPanel mojiPanel)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(mojiPanel);
            if (_mojiPanels.Contains(mojiPanel)) return;

            _mojiPanels.Add(mojiPanel);
            _viewMojiPanels.Add(mojiPanel);
            MainCanvas.Children.Add(mojiPanel);
            RaiseContentChanged("文字追加");
        }

        public void ReproductionMoji(MojiPanel mojiPanel)
        {
            ArgumentNullException.ThrowIfNull(mojiPanel);
            AddMojiPanel(new MojiPanel(mojiPanel.MojiData.Reproduct(GetNextMojiId()), this));
        }

        public void RemoveMojiPanel(MojiPanel mojiPanel)
        {
            if (!_mojiPanels.Remove(mojiPanel)) return;

            _viewMojiPanels.Remove(mojiPanel);
            mojiPanel.Dispose();
            MainCanvas.Children.Remove(mojiPanel);
            RaiseContentChanged("文字削除");
        }

        public void RemoveAllMojiPanel()
        {
            foreach (var panel in _mojiPanels.ToArray()) RemoveMojiPanel(panel);
        }

        public void LoadImage(string filePath, int imageNumber)
        {
            if (imageNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(imageNumber));
            var candidate = _boundPage != null && _assetSink is ProjectSessionAssetStore store
                ? store.PrepareImage(filePath)
                : CreateCandidate(filePath);
            if (_boundPage != null && _assetSink is ProjectSessionAssetStore assetStore)
            {
                var assets = assetStore.GetPageAssets(_boundPage.PageId)
                    .Where(asset => asset.ImageNumber != imageNumber)
                    .Append(candidate.ToAsset(_boundPage.PageId, imageNumber))
                    .ToArray();
                assetStore.ReplacePageAssets(_boundPage.PageId, assets);
            }
            ApplyImage(candidate, imageNumber);
            RaiseContentChanged("画像変更");
        }

        public void ApplyImage(ProjectImageCandidate candidate, int imageNumber)
        {
            ThrowIfDisposed();
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (imageNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(imageNumber));
            var imageSource = CreateImageSource(candidate.Content);
            (imageNumber == 1 ? ImageControl1 : ImageControl2).Source = imageSource;
            if (imageNumber == 1) CanvasData.ImageData1 = candidate.ToImageData();
            else CanvasData.ImageData2 = candidate.ToImageData();
        }

        public void ClearImage(int imageNumber)
        {
            ThrowIfDisposed();
            if (imageNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(imageNumber));
            (imageNumber == 1 ? ImageControl1 : ImageControl2).Source = null;
            if (imageNumber == 1) CanvasData.ImageData1.Init();
            else CanvasData.ImageData2.Init();
        }

        public void UnloadImage(int imageNumber)
        {
            if (imageNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(imageNumber));
            (imageNumber == 1 ? ImageControl1 : ImageControl2).Source = null;
        }

        internal void NotifyContentChanged(string description = "ページ編集", string? coalesceKey = null)
            => RaiseContentChanged(description, coalesceKey);

        public void CloseCanvasEditor()
        {
            var window = _canvasEditWindow;
            _canvasEditWindow = null;
            window?.Close();
        }

        /// <summary>
        /// ページの最終破棄時に、ページが所有するWindowと文字パネルを解放します。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            CloseCanvasEditor();
            foreach (var panel in _mojiPanels.ToArray()) panel.Dispose();
            _mojiPanels.Clear();
            _viewMojiPanels.Clear();
            MainCanvas.Children.Clear();
            FileDropped = null;
            ContentChanged = null;
        }

        internal void OnCanvasEditWindowClosed(CanvasEditWindow window)
        {
            if (ReferenceEquals(_canvasEditWindow, window)) _canvasEditWindow = null;
        }

        public void ExportImage(string filePath, BitmapEncoder encoder)
        {
            var previousScale = ScalingTextBox.Value;
            var previousBackground = CanvasBackgroundRect.Fill;
            var previousCanvasBackground = MainCanvas.Background;
            try
            {
                UpdateScale(100);
                if (encoder is JpegBitmapEncoder)
                {
                    // JPEG has no alpha channel. Use an opaque white background rather
                    // than allowing transparent pixels to become encoder-dependent black.
                    CanvasBackgroundRect.Fill = Brushes.White;
                    MainCanvas.Background = Brushes.White;
                }
                else
                {
                    // The gray workspace background must not be baked into transparent PNGs.
                    MainCanvas.Background = Brushes.Transparent;
                }
                MainCanvas.ToImage(filePath, encoder);
            }
            finally
            {
                CanvasBackgroundRect.Fill = previousBackground;
                MainCanvas.Background = previousCanvasBackground;
                UpdateScale(previousScale);
            }
        }

        public void UpdateCanvas()
        {
            if (!CanvasData.ImageData1.IsNullData())
            {
                ImageControl1.Width = CanvasData.ImageData1.ModifiedWidth;
                ImageControl1.Height = CanvasData.ImageData1.ModifiedHeight;
            }

            if (!CanvasData.ImageData2.IsNullData())
            {
                ImageControl2.Width = CanvasData.ImageData2.ModifiedWidth;
                ImageControl2.Height = CanvasData.ImageData2.ModifiedHeight;
            }

            MainCanvas.Width = CanvasData.CanvasWidth;
            MainCanvas.Height = CanvasData.CanvasHeight;
            CanvasBackgroundRect.Fill = new SolidColorBrush(CanvasData.CanvasColor);
            CanvasBackgroundRect.Width = CanvasData.CanvasWidth;
            CanvasBackgroundRect.Height = CanvasData.CanvasHeight;
            ImageControl1.Margin = CanvasData.GetImage1Margin();
            ImageControl2.Margin = CanvasData.GetImage2Margin();
        }

        private int GetNextMojiId() => _mojiPanels.Count == 0 ? 1 : _mojiPanels.Max(x => x.Id) + 1;

        private void ResetScale()
        {
            _runEvent = false;
            ScalingTextBox.SetValue(100, false);
            UpdateScale(100);
            _runEvent = true;
        }

        private void UpdateScale(int scalePercent)
        {
            CanvasScaleTransform.ScaleX = scalePercent / 100.0;
            CanvasScaleTransform.ScaleY = scalePercent / 100.0;
            if (ScalingTextBox.Value != scalePercent) ScalingTextBox.SetValue(scalePercent, false);
        }

        private void ScalingTextBox_ValueChanged(object sender, UpDownTextBoxEvent e)
        {
            if (_runEvent) UpdateScale(e.Value);
        }

        private void CanvasGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) ScalingTextBox.RunUpButton();
            else if (e.Delta < 0) ScalingTextBox.RunDownButton();
        }

        private void MojiListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (MojiListView.SelectedItem is MojiPanel mojiPanel) mojiPanel.ShowMojiWindow();
        }

        private void CanvasEditButton_Click(object sender, RoutedEventArgs e)
        {
            ThrowIfDisposed();
            if (_canvasEditWindow == null || !_canvasEditWindow.IsVisible)
            {
                _canvasEditWindow = new CanvasEditWindow(CanvasData, this);
                _canvasEditWindow.Show();
            }
        }

        private void CanvasGrid_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (filePaths.Length == 0) return;

            FileDropped?.Invoke(this, new PageFileDropEventArgs(filePaths[0]));
            e.Handled = true;
        }

        private void CanvasGrid_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);
            var extension = filePaths.Length == 0 ? string.Empty : System.IO.Path.GetExtension(filePaths[0]);
            e.Effects = extension is ".jpg" or ".png" or ".mctzip"
                ? DragDropEffects.All
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(PageEditorControl));
        }

        private void CapturePage(PageDocument page)
        {
            page.Canvas.CanvasWidth = CanvasData.CanvasWidth;
            page.Canvas.CanvasHeight = CanvasData.CanvasHeight;
            page.Canvas.ImageData1 = CanvasData.ImageData1.Clone();
            page.Canvas.ImageData2 = CanvasData.ImageData2.Clone();
            page.Canvas.Image2LocatePosition = CanvasData.Image2LocatePosition;
            page.Canvas.ImageMarginTop = CanvasData.ImageMarginTop;
            page.Canvas.ImageMarginLeft = CanvasData.ImageMarginLeft;
            page.Canvas.ImageMarginBottom = CanvasData.ImageMarginBottom;
            page.Canvas.ImageMarginRight = CanvasData.ImageMarginRight;
            page.Canvas.CanvasColor = CanvasData.CanvasColor;
            page.SetMojiDatas(_mojiPanels.Select(panel => panel.MojiData));
        }

        private void LoadStoredImage(PageDocument page, int imageNumber)
        {
            if (_assetSource == null) return;
            using var asset = _assetSource.OpenImage(page, imageNumber);
            if (asset == null) return;
            if (asset.Content.CanSeek) asset.Content.Position = 0;
            var imageSource = new BitmapImage();
            imageSource.BeginInit();
            imageSource.CacheOption = BitmapCacheOption.OnLoad;
            imageSource.StreamSource = asset.Content;
            imageSource.EndInit();
            imageSource.Freeze();
            var image = imageNumber == 1 ? ImageControl1 : ImageControl2;
            image.Source = imageSource;
        }

        private static BitmapImage CreateImageSource(byte[] content)
        {
            using var stream = new MemoryStream(content, writable: false);
            var imageSource = new BitmapImage();
            imageSource.BeginInit();
            imageSource.CacheOption = BitmapCacheOption.OnLoad;
            imageSource.StreamSource = stream;
            imageSource.EndInit();
            imageSource.Freeze();
            return imageSource;
        }

        private static ProjectImageCandidate CreateCandidate(string filePath)
        {
            var imageSource = ImageUtil.LoadImageSource2(filePath);
            return new ProjectImageCandidate(
                Path.GetExtension(filePath),
                File.ReadAllBytes(filePath),
                (int)imageSource.Width,
                (int)imageSource.Height);
        }

        private void RaiseContentChanged(string description = "ページ編集", string? coalesceKey = null)
        {
            if (_suppressChanges) return;
            ContentChangeDescription = description;
            ContentChangeCoalesceKey = coalesceKey;
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public sealed class PageFileDropEventArgs : EventArgs
    {
        public PageFileDropEventArgs(string filePath) => FilePath = filePath;

        public string FilePath { get; }
    }
}
