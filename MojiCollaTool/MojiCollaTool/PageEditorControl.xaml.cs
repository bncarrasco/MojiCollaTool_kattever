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
using System.Windows.Shapes;

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
        private readonly List<BalloonVisual> _balloonVisuals = new();
        private readonly BalloonGeometryFactory _balloonGeometryFactory = new();
        private readonly Dictionary<Rectangle, ResizeHandle> _resizeHandles = new();
        private readonly Dictionary<Guid, Guid?> _selectedObjectIdsByPage = new();
        private CanvasEditWindow? _canvasEditWindow;
        private bool _runEvent;
        private bool _isDisposed;
        private PageDocument? _boundPage;
        private IProjectAssetSource? _assetSource;
        private IProjectAssetSink? _assetSink;
        private bool _suppressChanges;
        private BalloonVisual? _selectedBalloon;
        private BalloonDragState? _balloonDrag;
        private bool _restoringBalloon;

        private const double DefaultBalloonWidth = 240;
        private const double DefaultBalloonHeight = 140;
        private const double MinimumBalloonSize = 24;
        private const double ResizeHandleSize = 8;

        public PageEditorControl()
        {
            InitializeComponent();
            MojiListView.ItemsSource = _viewMojiPanels;
            MainCanvas.PreviewMouseLeftButtonDown += MainCanvas_PreviewMouseLeftButtonDown;
            ResetScale();
            _runEvent = true;
        }

        public CanvasData CanvasData { get; private set; } = new();

        public Canvas Canvas => MainCanvas;

        public IReadOnlyList<MojiPanel> MojiPanels => _mojiPanels;

        public IReadOnlyList<BalloonVisual> BalloonVisuals => _balloonVisuals;

        public IEnumerable<BalloonData> Balloons => _balloonVisuals.Select(visual => visual.BalloonData);

        public IEnumerable<MojiData> MojiDatas => _mojiPanels.Select(panel => panel.MojiData);

        public bool HasMojiPanels => _mojiPanels.Count > 0;

        public event EventHandler<PageFileDropEventArgs>? FileDropped;

        public event EventHandler? ContentChanged;

        public string ContentChangeDescription { get; private set; } = "ページ編集";

        public string? ContentChangeCoalesceKey { get; private set; }

        public PageDocument? BoundPage => _boundPage;

        public int ScalePercent => ScalingTextBox.Value;

        public Guid? SelectedObjectId => (MojiListView.SelectedItem as MojiPanel)?.MojiData.ObjectId
            ?? _selectedBalloon?.ObjectId;

        public Guid? SelectedBalloonId => _selectedBalloon?.ObjectId;

        public void RestoreViewState(int scalePercent, Guid? selectedObjectId)
        {
            _runEvent = false;
            try
            {
                UpdateScale(Math.Max(ScalingTextBox.ValueMinLimit, scalePercent));
                RestoreSelection(selectedObjectId);
                if (_boundPage != null) _selectedObjectIdsByPage[_boundPage.PageId] = SelectedObjectId;
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

            if (_boundPage != null)
            {
                RememberSelection(_boundPage.PageId);
                CapturePage(_boundPage);
            }
            _suppressChanges = true;
            try
            {
                CloseCanvasEditor();
                RemoveAllMojiPanel();
                RemoveAllBalloonVisuals();
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
                foreach (var balloon in page.Balloons)
                {
                    AddBalloonVisual(new BalloonVisual(PageDocument.CloneBalloonData(balloon), _balloonGeometryFactory), raiseContentChanged: false);
                }
                RebuildCanvasObjectOrder();
                RestoreSelectionForPage(page.PageId);
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
            RememberSelection(_boundPage.PageId);
            CapturePage(_boundPage);
            _suppressChanges = true;
            try
            {
                CloseCanvasEditor();
                RemoveAllMojiPanel();
                RemoveAllBalloonVisuals();
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

        public BalloonVisual AddNewBalloon(BalloonShapeKind shape = BalloonShapeKind.Ellipse)
        {
            ThrowIfDisposed();
            var index = _balloonVisuals.Count;
            var balloon = new BalloonData
            {
                X = 40 + (index % 4) * 30,
                Y = 40 + (index % 4) * 30,
                Bounds = new Rect(0, 0, DefaultBalloonWidth, DefaultBalloonHeight),
                ShapeKind = shape,
                ZIndex = _boundPage?.ObjectCount ?? (_mojiPanels.Count + _balloonVisuals.Count),
            };
            var visual = new BalloonVisual(balloon, _balloonGeometryFactory);
            AddBalloonVisual(visual);
            SelectBalloon(visual);
            return visual;
        }

        public void AddBalloon(BalloonData balloon)
        {
            ThrowIfDisposed();
            if (balloon == null) throw new ArgumentNullException(nameof(balloon));
            AddBalloonVisual(new BalloonVisual(PageDocument.CloneBalloonData(balloon), _balloonGeometryFactory));
        }

        public bool RemoveBalloon(Guid balloonId)
        {
            var visual = _balloonVisuals.FirstOrDefault(candidate => candidate.ObjectId == balloonId);
            if (visual == null) return false;
            RemoveBalloonVisual(visual);
            RaiseContentChanged("フキダシ削除");
            return true;
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
            RemoveAllBalloonVisuals();
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
                    // JPEG has no alpha channel. Keep CanvasColor as the foreground and
                    // use a white backplate only where the canvas is transparent.
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
            UpdateResizeHandles();
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
            if (_runEvent)
            {
                UpdateScale(e.Value);
                UpdateResizeHandles();
            }
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

        internal bool IsBalloonGestureActive => _balloonDrag != null;

        internal IReadOnlyCollection<Rectangle> ResizeHandleVisuals => _resizeHandles.Keys;

        internal Rectangle CanvasBackgroundVisual => CanvasBackgroundRect;

        internal bool BeginBalloonGesture(Guid balloonId, Point start, BalloonResizeHandle handle = BalloonResizeHandle.Move)
        {
            var visual = _balloonVisuals.FirstOrDefault(candidate => candidate.ObjectId == balloonId);
            if (visual == null || visual.BalloonData.IsLocked || _balloonDrag != null) return false;
            SelectBalloon(visual);
            _balloonDrag = new BalloonDragState(
                visual,
                start,
                visual.BalloonData.Clone(),
                ToResizeHandle(handle),
                Guid.NewGuid().ToString("D"));
            return true;
        }

        internal bool UpdateBalloonGesture(Point current)
        {
            if (_balloonDrag == null) return false;
            ApplyBalloonDrag(current);
            return true;
        }

        internal bool CommitBalloonGesture(Point current)
        {
            if (_balloonDrag == null) return false;
            var state = _balloonDrag;
            ApplyBalloonDrag(current);
            var changed = !BalloonEquivalent(state.Before, state.Visual.BalloonData);
            _balloonDrag = null;
            if (changed)
            {
                RaiseContentChanged("繝輔く繝繧ｷ菴咲ｽｮ繝ｻ繧ｵ繧､繧ｺ螟画峩", state.CoalesceKey);
            }
            return changed;
        }

        internal void CancelBalloonGesture()
        {
            if (_balloonDrag == null) return;
            var visual = _balloonDrag.Visual;
            _restoringBalloon = true;
            try { visual.ApplyData(_balloonDrag.Before.Clone()); }
            finally
            {
                _restoringBalloon = false;
                _balloonDrag = null;
            }
            UpdateResizeHandles();
        }

        internal void HandleCanvasClickSource(object? source)
        {
            if (source is Rectangle rectangle && _resizeHandles.ContainsKey(rectangle)) return;
            SelectBalloon(null);
        }

        private void RestoreSelectionForPage(Guid pageId)
        {
            _selectedObjectIdsByPage.TryGetValue(pageId, out var selectedObjectId);
            RestoreSelection(selectedObjectId);
        }

        private void RestoreSelection(Guid? selectedObjectId)
        {
            var textPanel = selectedObjectId.HasValue
                ? _mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == selectedObjectId.Value)
                : null;
            if (textPanel != null)
            {
                SelectBalloon(null);
                MojiListView.SelectedItem = textPanel;
                return;
            }

            MojiListView.SelectedItem = null;
            SelectBalloon(selectedObjectId.HasValue
                ? _balloonVisuals.FirstOrDefault(visual => visual.ObjectId == selectedObjectId.Value)
                : null);
        }

        private void RememberSelection(Guid pageId)
        {
            _selectedObjectIdsByPage[pageId] = SelectedObjectId;
        }

        private void AddBalloonButton_Click(object sender, RoutedEventArgs e)
        {
            var item = BalloonShapeComboBox.SelectedItem as ComboBoxItem;
            var shape = Enum.TryParse(item?.Tag as string, ignoreCase: true, out BalloonShapeKind parsed)
                ? parsed
                : BalloonShapeKind.Ellipse;
            AddNewBalloon(shape);
        }

        private void MainCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HandleCanvasClickSource(e.OriginalSource);
        }

        private void AddBalloonVisual(BalloonVisual visual, bool raiseContentChanged = true)
        {
            if (_balloonVisuals.Any(candidate => candidate.ObjectId == visual.ObjectId))
                throw new InvalidOperationException($"Duplicate balloon object ID: {visual.ObjectId}");

            visual.BalloonMouseLeftButtonDown += BalloonVisual_MouseLeftButtonDown;
            visual.BalloonMouseLeftButtonUp += BalloonVisual_MouseLeftButtonUpBoundary;
            visual.BalloonMouseMove += BalloonVisual_MouseMove;
            visual.BalloonLostMouseCapture += BalloonVisual_LostMouseCaptureBoundary;
            _balloonVisuals.Add(visual);
            MainCanvas.Children.Add(visual);
            if (raiseContentChanged) RaiseContentChanged("フキダシ追加");
        }

        private void RemoveBalloonVisual(BalloonVisual visual)
        {
            if (!_balloonVisuals.Remove(visual)) return;
            visual.BalloonMouseLeftButtonDown -= BalloonVisual_MouseLeftButtonDown;
            visual.BalloonMouseLeftButtonUp -= BalloonVisual_MouseLeftButtonUpBoundary;
            visual.BalloonMouseMove -= BalloonVisual_MouseMove;
            visual.BalloonLostMouseCapture -= BalloonVisual_LostMouseCaptureBoundary;
            MainCanvas.Children.Remove(visual);
            if (ReferenceEquals(_selectedBalloon, visual)) SelectBalloon(null);
        }

        private void RemoveAllBalloonVisuals()
        {
            _balloonDrag = null;
            SelectBalloon(null);
            foreach (var visual in _balloonVisuals.ToArray()) RemoveBalloonVisual(visual);
            ClearResizeHandles();
        }

        private void SelectBalloon(BalloonVisual? visual)
        {
            if (ReferenceEquals(_selectedBalloon, visual))
            {
                UpdateResizeHandles();
                return;
            }
            if (_selectedBalloon != null) _selectedBalloon.IsSelected = false;
            _selectedBalloon = visual;
            if (_selectedBalloon != null)
            {
                _selectedBalloon.IsSelected = true;
                MojiListView.SelectedItem = null;
            }
            UpdateResizeHandles();
            foreach (var candidate in _balloonVisuals) candidate.InvalidateVisual();
        }

        private void BalloonVisual_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not BalloonVisual visual) return;
            if (e.ChangedButton != MouseButton.Left || !BeginBalloonGesture(visual.ObjectId, e.GetPosition(MainCanvas)))
            {
                e.Handled = true;
                return;
            }
            visual.CaptureMouse();
            e.Handled = true;
        }

        private void BalloonVisual_MouseMove(object sender, MouseEventArgs e)
        {
            if (_balloonDrag == null || sender is not BalloonVisual visual || !ReferenceEquals(visual, _balloonDrag.Visual)) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            UpdateBalloonGesture(e.GetPosition(MainCanvas));
            e.Handled = true;
        }

        private void BalloonVisual_MouseLeftButtonUpBoundary(object sender, MouseButtonEventArgs e)
        {
            if (_balloonDrag == null || sender is not BalloonVisual visual || !ReferenceEquals(visual, _balloonDrag.Visual)) return;
            CommitBalloonGesture(e.GetPosition(MainCanvas));
            visual.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void BalloonVisual_LostMouseCaptureBoundary(object sender, MouseEventArgs e)
        {
            if (_balloonDrag == null || sender is not BalloonVisual visual || !ReferenceEquals(visual, _balloonDrag.Visual) || _restoringBalloon) return;
            CancelBalloonGesture();
        }

        private void BalloonVisual_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_balloonDrag == null || sender is not BalloonVisual visual || !ReferenceEquals(visual, _balloonDrag.Visual)) return;
            ApplyBalloonDrag(e.GetPosition(MainCanvas));
            var moved = !BalloonEquivalent(_balloonDrag.Before, visual.BalloonData);
            _balloonDrag = null;
            visual.ReleaseMouseCapture();
            if (moved) RaiseContentChanged("フキダシ位置・サイズ変更", visual.ObjectId.ToString("D"));
            e.Handled = true;
        }

        private void BalloonVisual_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_balloonDrag == null || sender is not BalloonVisual visual || !ReferenceEquals(visual, _balloonDrag.Visual) || _restoringBalloon) return;
            _restoringBalloon = true;
            try { visual.ApplyData(_balloonDrag.Before.Clone()); }
            finally
            {
                _restoringBalloon = false;
                _balloonDrag = null;
            }
            UpdateResizeHandles();
        }

        private void ApplyBalloonDrag(Point current)
        {
            if (_balloonDrag == null) return;
            var dx = current.X - _balloonDrag.Start.X;
            var dy = current.Y - _balloonDrag.Start.Y;
            var before = _balloonDrag.Before;
            var data = _balloonDrag.Visual.BalloonData;
            if (_balloonDrag.Handle == ResizeHandle.Move)
            {
                data.X = before.X + dx;
                data.Y = before.Y + dy;
            }
            else
            {
                var left = before.X;
                var top = before.Y;
                var right = before.X + before.Bounds.Width;
                var bottom = before.Y + before.Bounds.Height;
                if (_balloonDrag.Handle.HasFlag(ResizeHandle.Left)) left = Math.Min(before.X + dx, right - MinimumBalloonSize);
                if (_balloonDrag.Handle.HasFlag(ResizeHandle.Right)) right = Math.Max(before.X + before.Bounds.Width + dx, left + MinimumBalloonSize);
                if (_balloonDrag.Handle.HasFlag(ResizeHandle.Top)) top = Math.Min(before.Y + dy, bottom - MinimumBalloonSize);
                if (_balloonDrag.Handle.HasFlag(ResizeHandle.Bottom)) bottom = Math.Max(before.Y + before.Bounds.Height + dy, top + MinimumBalloonSize);
                data.X = left;
                data.Y = top;
                data.Bounds = new Rect(0, 0, Math.Max(MinimumBalloonSize, right - left), Math.Max(MinimumBalloonSize, bottom - top));
            }
            _balloonDrag.Visual.Refresh();
            UpdateResizeHandles();
        }

        private static bool BalloonEquivalent(BalloonData left, BalloonData right)
            => left.X == right.X && left.Y == right.Y && left.Bounds == right.Bounds && left.Rotation == right.Rotation;

        private void RebuildCanvasObjectOrder()
        {
            if (_boundPage == null) return;
            var visuals = _balloonVisuals.ToDictionary(visual => visual.ObjectId);
            var panels = _mojiPanels.ToDictionary(panel => panel.MojiData.ObjectId);
            foreach (var child in _mojiPanels.Cast<UIElement>().Concat(_balloonVisuals).ToArray()) MainCanvas.Children.Remove(child);
            foreach (var item in _boundPage.AllObjects)
            {
                if (item is BalloonData balloon && visuals.TryGetValue(balloon.ObjectId, out var visual)) MainCanvas.Children.Add(visual);
                else if (item is MojiData moji && panels.TryGetValue(moji.ObjectId, out var panel)) MainCanvas.Children.Add(panel);
            }
            UpdateResizeHandles();
        }

        private void UpdateResizeHandles()
        {
            ClearResizeHandles();
            if (_selectedBalloon == null || !_balloonVisuals.Contains(_selectedBalloon)) return;
            var scale = Math.Max(0.01, CanvasScaleTransform.ScaleX);
            var logicalSize = ResizeHandleSize / scale;
            var half = logicalSize / 2;
            var data = _selectedBalloon.BalloonData;
            var x = data.X;
            var y = data.Y;
            var w = Math.Max(MinimumBalloonSize, data.Bounds.Width);
            var h = Math.Max(MinimumBalloonSize, data.Bounds.Height);
            AddResizeHandle(ResizeHandle.TopLeft, x, y, logicalSize, half);
            AddResizeHandle(ResizeHandle.Top, x + w / 2, y, logicalSize, half);
            AddResizeHandle(ResizeHandle.TopRight, x + w, y, logicalSize, half);
            AddResizeHandle(ResizeHandle.Left, x, y + h / 2, logicalSize, half);
            AddResizeHandle(ResizeHandle.Right, x + w, y + h / 2, logicalSize, half);
            AddResizeHandle(ResizeHandle.BottomLeft, x, y + h, logicalSize, half);
            AddResizeHandle(ResizeHandle.Bottom, x + w / 2, y + h, logicalSize, half);
            AddResizeHandle(ResizeHandle.BottomRight, x + w, y + h, logicalSize, half);
        }

        private void AddResizeHandle(ResizeHandle handle, double centerX, double centerY, double size, double half)
        {
            var rectangle = new Rectangle
            {
                Width = size, Height = size, Fill = Brushes.White, Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1, Tag = handle, Cursor = GetCursor(handle),
            };
            rectangle.MouseLeftButtonDown += ResizeHandle_MouseLeftButtonDown;
            Canvas.SetLeft(rectangle, centerX - half);
            Canvas.SetTop(rectangle, centerY - half);
            MainCanvas.Children.Add(rectangle);
            _resizeHandles.Add(rectangle, handle);
        }

        private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Rectangle rectangle || _selectedBalloon == null || !_resizeHandles.TryGetValue(rectangle, out var handle)) return;
            if (!BeginBalloonGesture(_selectedBalloon.ObjectId, e.GetPosition(MainCanvas), FromResizeHandle(handle))) return;
            _selectedBalloon.CaptureMouse();
            e.Handled = true;
        }

        private static Cursor GetCursor(ResizeHandle handle)
        {
            return handle switch
            {
                ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
                ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
                ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
                _ => Cursors.SizeNESW,
            };
        }

        private static ResizeHandle ToResizeHandle(BalloonResizeHandle handle)
        {
            return handle switch
            {
                BalloonResizeHandle.Left => ResizeHandle.Left,
                BalloonResizeHandle.Right => ResizeHandle.Right,
                BalloonResizeHandle.Top => ResizeHandle.Top,
                BalloonResizeHandle.Bottom => ResizeHandle.Bottom,
                BalloonResizeHandle.TopLeft => ResizeHandle.TopLeft,
                BalloonResizeHandle.TopRight => ResizeHandle.TopRight,
                BalloonResizeHandle.BottomLeft => ResizeHandle.BottomLeft,
                BalloonResizeHandle.BottomRight => ResizeHandle.BottomRight,
                _ => ResizeHandle.Move,
            };
        }

        private static BalloonResizeHandle FromResizeHandle(ResizeHandle handle)
        {
            return handle switch
            {
                ResizeHandle.Left => BalloonResizeHandle.Left,
                ResizeHandle.Right => BalloonResizeHandle.Right,
                ResizeHandle.Top => BalloonResizeHandle.Top,
                ResizeHandle.Bottom => BalloonResizeHandle.Bottom,
                ResizeHandle.TopLeft => BalloonResizeHandle.TopLeft,
                ResizeHandle.TopRight => BalloonResizeHandle.TopRight,
                ResizeHandle.BottomLeft => BalloonResizeHandle.BottomLeft,
                ResizeHandle.BottomRight => BalloonResizeHandle.BottomRight,
                _ => BalloonResizeHandle.Move,
            };
        }

        private void ClearResizeHandles()
        {
            foreach (var rectangle in _resizeHandles.Keys.ToArray())
            {
                rectangle.MouseLeftButtonDown -= ResizeHandle_MouseLeftButtonDown;
                MainCanvas.Children.Remove(rectangle);
            }
            _resizeHandles.Clear();
        }

        [Flags]
        private enum ResizeHandle
        {
            Move = 0, Left = 1, Right = 2, Top = 4, Bottom = 8,
            TopLeft = Top | Left, TopRight = Top | Right,
            BottomLeft = Bottom | Left, BottomRight = Bottom | Right,
        }

        private sealed class BalloonDragState
        {
            public BalloonDragState(BalloonVisual visual, Point start, BalloonData before, ResizeHandle handle)
                : this(visual, start, before, handle, Guid.NewGuid().ToString("D"))
            {
            }

            public BalloonDragState(BalloonVisual visual, Point start, BalloonData before, ResizeHandle handle, string coalesceKey)
            {
                Visual = visual; Start = start; Before = before; Handle = handle; CoalesceKey = coalesceKey;
            }
            public BalloonVisual Visual { get; }
            public Point Start { get; }
            public BalloonData Before { get; }
            public ResizeHandle Handle { get; }
            public string CoalesceKey { get; }
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
            page.SetBalloons(_balloonVisuals.Select(visual => visual.BalloonData));
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
                System.IO.Path.GetExtension(filePath),
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

    [Flags]
    internal enum BalloonResizeHandle
    {
        Move = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
        TopLeft = Top | Left,
        TopRight = Top | Right,
        BottomLeft = Bottom | Left,
        BottomRight = Bottom | Right,
    }
}
