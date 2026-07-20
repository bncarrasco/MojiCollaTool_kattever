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
        private readonly List<AttachedSymbolVisual> _attachedSymbolVisuals = new();
        private readonly List<AttachedSymbolData> _attachedSymbolModels = new();
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
        private bool _suppressSelectionSync;
        private BalloonVisual? _selectedBalloon;
        private AttachedSymbolVisual? _selectedAttachedSymbol;
        private BalloonDragState? _balloonDrag;
        private AttachedSymbolDragState? _attachedSymbolDrag;
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

        public IReadOnlyList<AttachedSymbolVisual> AttachedSymbolVisuals => _attachedSymbolVisuals;

        public IEnumerable<AttachedSymbolData> AttachedSymbols => _attachedSymbolModels.Select(PageDocument.CloneAttachedSymbolData).ToArray();

        public IEnumerable<BalloonData> Balloons => _balloonVisuals.Select(visual => visual.BalloonData);

        public IEnumerable<MojiData> MojiDatas => _mojiPanels.Select(panel => panel.MojiData);

        public bool HasMojiPanels => _mojiPanels.Count > 0;

        public event EventHandler<PageFileDropEventArgs>? FileDropped;

        public event EventHandler? ContentChanged;

        public string ContentChangeDescription { get; private set; } = "ページ編集";

        public string? ContentChangeCoalesceKey { get; private set; }

        public PageDocument? BoundPage => _boundPage;

        public int ScalePercent => ScalingTextBox.Value;

        public Guid? SelectedObjectId => _selectedAttachedSymbol?.ObjectId
            ?? (MojiListView.SelectedItem as MojiPanel)?.MojiData.ObjectId
            ?? _selectedBalloon?.ObjectId;

        public Guid? SelectedBalloonId => _selectedBalloon?.ObjectId;

        public Guid? SelectedAttachedSymbolId => _selectedAttachedSymbol?.ObjectId;

        internal void SelectAttachedSymbolFromUi(AttachedSymbolVisual visual)
        {
            if (visual == null || !_attachedSymbolVisuals.Contains(visual)) return;
            SelectAttachedSymbol(visual);
        }

        internal void HandleMojiListItemClickFromUi(MojiPanel panel)
        {
            if (panel == null || !_mojiPanels.Contains(panel)) return;
            SelectAttachedSymbol(null);
            SelectBalloon(null);
            _suppressSelectionSync = true;
            try { MojiListView.SelectedItem = panel; }
            finally { _suppressSelectionSync = false; }
        }

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
                RemoveAllAttachedSymbolVisuals();
                _attachedSymbolModels.Clear();
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
                _attachedSymbolModels.Clear();
                _attachedSymbolModels.AddRange(page.AttachedSymbols.Select(PageDocument.CloneAttachedSymbolData));
                foreach (var symbol in _attachedSymbolModels.Where(item => !item.IsDetached && item.ParentId.HasValue))
                {
                    var parent = _mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == symbol.ParentId);
                    if (parent != null) AddAttachedSymbolVisual(
                        new AttachedSymbolVisual(symbol.Clone(), parent.MojiData), raiseContentChanged: false);
                }
                RebuildCanvasObjectOrder();
                MainCanvas.UpdateLayout();
                foreach (var parent in _mojiPanels) RefreshAttachedSymbolsForParent(parent);
                RestoreSelectionForPage(page.PageId);
                foreach (var parent in _mojiPanels) parent.MojiWindow?.LoadMojiDataToWindow(parent.MojiData);
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
                RemoveAllAttachedSymbolVisuals();
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

        public AttachedSymbolVisual AddAttachedSymbol(AttachedSymbolData symbol)
        {
            ThrowIfDisposed();
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            var candidate = symbol.Clone();
            candidate.Validate();
            if (candidate.IsDetached) throw new InvalidOperationException("A newly added attached symbol must have a parent.");
            var parent = _mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == candidate.ParentId);
            if (parent == null) throw new InvalidOperationException("付加記号の親文字が見つかりません。");
            if (candidate.GraphemeAnchor >= parent.MojiData.GraphemeCount)
                throw new InvalidOperationException("Attached symbol grapheme anchor is outside the parent text.");
            if (HasObjectId(candidate.ObjectId))
                throw new InvalidOperationException($"Duplicate attached symbol object ID: {candidate.ObjectId}");
            candidate.ZIndex = GetNextZIndex();
            var visual = new AttachedSymbolVisual(candidate, parent.MojiData);
            try
            {
                AddAttachedSymbolVisual(visual, raiseContentChanged: false);
                _attachedSymbolModels.Add(candidate.Clone());
            }
            catch
            {
                throw;
            }
            RefreshAttachedSymbolsForParent(parent);
            SelectAttachedSymbol(visual);
            return visual;
        }

        public AttachedSymbolVisual AddAttachedSymbol(Guid parentObjectId, int graphemeAnchor, string text)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("付加記号を入力してください。", nameof(text));
            var parent = _mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == parentObjectId)
                ?? throw new InvalidOperationException("付加記号の親文字が見つかりません。");
            var defaultOffset = AttachedSymbolPlacement.GetDefaultOffset(parent.MojiData.TextDirection, text);
            var symbol = new AttachedSymbolData
            {
                ParentId = parentObjectId,
                GraphemeAnchor = graphemeAnchor,
                AnchorText = graphemeAnchor >= 0 && graphemeAnchor < parent.MojiData.GraphemeCount
                    ? parent.MojiData.GetGrapheme(graphemeAnchor).Text : null,
                Text = text,
                OffsetX = defaultOffset.X,
                OffsetY = defaultOffset.Y,
                Scale = AttachedSymbolPlacement.DefaultScale,
                ZIndex = GetNextZIndex(),
            };
            var visual = AddAttachedSymbol(symbol);
            RaiseContentChanged("付加記号追加");
            return visual;
        }

        public void UpdateAttachedSymbol(Guid symbolId, Action<AttachedSymbolData> update,
            string description = "付加記号編集", string? coalesceKey = null)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            var visual = _attachedSymbolVisuals.FirstOrDefault(item => item.ObjectId == symbolId)
                ?? throw new KeyNotFoundException($"Attached symbol was not found: {symbolId}");
            var candidate = visual.SymbolData.Clone();
            update(candidate);
            if (candidate.ObjectId != symbolId) throw new InvalidOperationException("An attached symbol ID cannot be changed.");
            candidate.Validate();
            var parent = _mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == candidate.ParentId);
            if (!candidate.IsDetached && parent == null) throw new InvalidOperationException("付加記号の親文字が見つかりません。");
            if (parent != null && candidate.GraphemeAnchor >= parent.MojiData.GraphemeCount)
                throw new InvalidOperationException("付加記号の書記素アンカーが範囲外です。");
            visual.ApplyData(candidate, parent?.MojiData ?? visual.ParentTextData, visual.AnchorBounds);
            var modelIndex = _attachedSymbolModels.FindIndex(item => item.ObjectId == symbolId);
            if (modelIndex >= 0) _attachedSymbolModels[modelIndex] = candidate.Clone();
            RaiseContentChanged(description, coalesceKey ?? symbolId.ToString("D"));
        }

        public bool RemoveAttachedSymbol(Guid symbolId)
        {
            var visual = _attachedSymbolVisuals.FirstOrDefault(item => item.ObjectId == symbolId);
            if (visual == null) return false;
            RemoveAttachedSymbolVisual(visual);
            _attachedSymbolModels.RemoveAll(item => item.ObjectId == symbolId);
            RaiseContentChanged("付加記号削除");
            return true;
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

            // Keep a removed parent's symbols as explicit detached objects so
            // the model can persist the safe, non-crashing orphan state.
            foreach (var visual in _attachedSymbolVisuals.Where(item => item.SymbolData.ParentId == mojiPanel.MojiData.ObjectId).ToArray())
            {
                var model = _attachedSymbolModels.FirstOrDefault(item => item.ObjectId == visual.ObjectId);
                if (model != null)
                {
                    model.ParentId = null;
                    model.IsDetached = true;
                    model.AnchorText = null;
                }
                RemoveAttachedSymbolVisual(visual);
            }

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
            RemoveAllAttachedSymbolVisuals();
            _attachedSymbolModels.Clear();
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
            if (MojiListView.SelectedItem is MojiPanel mojiPanel)
            {
                HandleMojiListItemClickFromUi(mojiPanel);
                mojiPanel.ShowMojiWindow();
            }
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
                RaiseContentChanged("フキダシ位置・サイズ変更", state.CoalesceKey);
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

        internal bool IsAttachedSymbolGestureActive => _attachedSymbolDrag != null;

        internal bool BeginAttachedSymbolGesture(Guid symbolId, Point start)
        {
            var visual = _attachedSymbolVisuals.FirstOrDefault(candidate => candidate.ObjectId == symbolId);
            if (visual == null || visual.SymbolData.IsLocked || _attachedSymbolDrag != null) return false;
            SelectAttachedSymbol(visual);
            _attachedSymbolDrag = new AttachedSymbolDragState(
                visual, start, visual.SymbolData.Clone(), Guid.NewGuid().ToString("D"));
            return true;
        }

        internal bool UpdateAttachedSymbolGesture(Point current)
        {
            if (_attachedSymbolDrag == null) return false;
            var visual = _attachedSymbolDrag.Visual;
            var dx = current.X - _attachedSymbolDrag.Start.X;
            var dy = current.Y - _attachedSymbolDrag.Start.Y;
            var parentRotation = visual.ParentTextData.IsRotateActive ? visual.ParentTextData.RotateAngle : 0;
            var localDelta = parentRotation == 0
                ? new Vector(dx, dy)
                : ToVector(new RotateTransform(-parentRotation).Transform(new Point(dx, dy)));
            var em = Math.Max(1, visual.ParentTextData.FontSize);
            visual.SymbolData.OffsetX = _attachedSymbolDrag.Before.OffsetX + localDelta.X / em;
            visual.SymbolData.OffsetY = _attachedSymbolDrag.Before.OffsetY + localDelta.Y / em;
            visual.Refresh(visual.AnchorBounds);
            return true;
        }

        internal bool CommitAttachedSymbolGesture(Point current)
        {
            if (_attachedSymbolDrag == null) return false;
            var state = _attachedSymbolDrag;
            UpdateAttachedSymbolGesture(current);
            var changed = !AttachedSymbolEquivalent(state.Before, state.Visual.SymbolData);
            _attachedSymbolDrag = null;
            if (changed)
            {
                var index = _attachedSymbolModels.FindIndex(item => item.ObjectId == state.Visual.ObjectId);
                if (index >= 0) _attachedSymbolModels[index] = state.Visual.SymbolData.Clone();
                RaiseContentChanged("付加記号位置変更", state.CoalesceKey);
            }
            return changed;
        }

        internal void CancelAttachedSymbolGesture()
        {
            if (_attachedSymbolDrag == null) return;
            var state = _attachedSymbolDrag;
            _attachedSymbolDrag = null;
            state.Visual.ApplyData(state.Before, state.Visual.ParentTextData, state.Visual.AnchorBounds);
        }

        internal void HandleCanvasClickSource(object? source)
        {
            if (source is Rectangle rectangle && _resizeHandles.ContainsKey(rectangle)) return;
            if (source is AttachedSymbolVisual) return;
            SelectAttachedSymbol(null);
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
                SelectAttachedSymbol(null);
                SelectBalloon(null);
                _suppressSelectionSync = true;
                try { MojiListView.SelectedItem = textPanel; }
                finally { _suppressSelectionSync = false; }
                return;
            }

            var attachedSymbol = selectedObjectId.HasValue
                ? _attachedSymbolVisuals.FirstOrDefault(visual => visual.ObjectId == selectedObjectId.Value)
                : null;
            if (attachedSymbol != null)
            {
                SelectBalloon(null);
                SelectAttachedSymbol(attachedSymbol);
                return;
            }

            SelectAttachedSymbol(null);
            _suppressSelectionSync = true;
            try { MojiListView.SelectedItem = null; }
            finally { _suppressSelectionSync = false; }
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

        private void MojiListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionSync) return;
            if (MojiListView.SelectedItem is MojiPanel)
            {
                SelectAttachedSymbol(null);
                SelectBalloon(null);
            }
        }

        internal void RefreshAttachedSymbolsForParent(MojiPanel parent)
        {
            foreach (var visual in _attachedSymbolVisuals.Where(item => item.SymbolData.ParentId == parent.MojiData.ObjectId))
            {
                var bounds = parent.GetGraphemeAnchorBounds(visual.SymbolData.GraphemeAnchor, MainCanvas);
                visual.ApplyData(visual.SymbolData, parent.MojiData, bounds);
            }
        }

        private void AddAttachedSymbolVisual(AttachedSymbolVisual visual, bool raiseContentChanged = true)
        {
            if (_attachedSymbolVisuals.Any(candidate => candidate.ObjectId == visual.ObjectId))
                throw new InvalidOperationException($"Duplicate attached symbol object ID: {visual.ObjectId}");

            visual.SymbolMouseLeftButtonDown += AttachedSymbolVisual_MouseLeftButtonDown;
            visual.SymbolMouseLeftButtonUp += AttachedSymbolVisual_MouseLeftButtonUp;
            visual.SymbolMouseMove += AttachedSymbolVisual_MouseMove;
            visual.SymbolLostMouseCapture += AttachedSymbolVisual_LostMouseCapture;
            _attachedSymbolVisuals.Add(visual);
            MainCanvas.Children.Add(visual);
            Canvas.SetZIndex(visual, visual.SymbolData.ZIndex);
            if (raiseContentChanged) RaiseContentChanged("付加記号追加");
        }

        private void RemoveAttachedSymbolVisual(AttachedSymbolVisual visual)
        {
            if (!_attachedSymbolVisuals.Remove(visual)) return;
            visual.SymbolMouseLeftButtonDown -= AttachedSymbolVisual_MouseLeftButtonDown;
            visual.SymbolMouseLeftButtonUp -= AttachedSymbolVisual_MouseLeftButtonUp;
            visual.SymbolMouseMove -= AttachedSymbolVisual_MouseMove;
            visual.SymbolLostMouseCapture -= AttachedSymbolVisual_LostMouseCapture;
            MainCanvas.Children.Remove(visual);
            if (ReferenceEquals(_selectedAttachedSymbol, visual)) SelectAttachedSymbol(null);
        }

        private void RemoveAllAttachedSymbolVisuals()
        {
            _attachedSymbolDrag = null;
            SelectAttachedSymbol(null);
            foreach (var visual in _attachedSymbolVisuals.ToArray()) RemoveAttachedSymbolVisual(visual);
        }

        private void SelectAttachedSymbol(AttachedSymbolVisual? visual)
        {
            if (ReferenceEquals(_selectedAttachedSymbol, visual))
            {
                if (visual != null) visual.Refresh(visual.AnchorBounds);
                return;
            }
            if (_selectedAttachedSymbol != null)
            {
                _selectedAttachedSymbol.IsSelected = false;
                _selectedAttachedSymbol.Refresh(_selectedAttachedSymbol.AnchorBounds);
            }
            _selectedAttachedSymbol = visual;
            if (visual != null)
            {
                visual.IsSelected = true;
                var parent = _mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == visual.SymbolData.ParentId);
                if (parent != null)
                {
                    _suppressSelectionSync = true;
                    try { MojiListView.SelectedItem = parent; }
                    finally { _suppressSelectionSync = false; }
                }
                SelectBalloon(null);
                visual.Refresh(visual.AnchorBounds);
            }
        }

        private void AttachedSymbolVisual_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not AttachedSymbolVisual visual || e.ChangedButton != MouseButton.Left || visual.SymbolData.IsLocked)
            {
                e.Handled = true;
                return;
            }
            if (BeginAttachedSymbolGesture(visual.ObjectId, e.GetPosition(MainCanvas))) visual.CaptureMouse();
            e.Handled = true;
        }

        private void AttachedSymbolVisual_MouseMove(object sender, MouseEventArgs e)
        {
            if (_attachedSymbolDrag == null || sender is not AttachedSymbolVisual visual ||
                !ReferenceEquals(visual, _attachedSymbolDrag.Visual) || e.LeftButton != MouseButtonState.Pressed) return;
            UpdateAttachedSymbolGesture(e.GetPosition(MainCanvas));
            e.Handled = true;
        }

        private void AttachedSymbolVisual_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_attachedSymbolDrag == null || sender is not AttachedSymbolVisual visual ||
                !ReferenceEquals(visual, _attachedSymbolDrag.Visual)) return;
            CommitAttachedSymbolGesture(e.GetPosition(MainCanvas));
            visual.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void AttachedSymbolVisual_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_attachedSymbolDrag == null || sender is not AttachedSymbolVisual visual ||
                !ReferenceEquals(visual, _attachedSymbolDrag.Visual)) return;
            CancelAttachedSymbolGesture();
        }

        private static bool AttachedSymbolEquivalent(AttachedSymbolData left, AttachedSymbolData right)
            => left.OffsetX == right.OffsetX && left.OffsetY == right.OffsetY &&
               left.Scale == right.Scale && left.Rotation == right.Rotation;

        private static Vector ToVector(Point point) => new(point.X, point.Y);

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
            if (visual != null) SelectAttachedSymbol(null);
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
            var symbols = _attachedSymbolVisuals.ToDictionary(visual => visual.ObjectId);
            foreach (var child in _mojiPanels.Cast<UIElement>().Concat(_balloonVisuals).Concat(_attachedSymbolVisuals).ToArray())
                MainCanvas.Children.Remove(child);
            foreach (var item in _boundPage.AllObjects)
            {
                if (item is BalloonData balloon && visuals.TryGetValue(balloon.ObjectId, out var visual)) MainCanvas.Children.Add(visual);
                else if (item is MojiData moji && panels.TryGetValue(moji.ObjectId, out var panel)) MainCanvas.Children.Add(panel);
                else if (item is AttachedSymbolData symbol && symbols.TryGetValue(symbol.ObjectId, out var symbolVisual)) MainCanvas.Children.Add(symbolVisual);
            }
            foreach (var item in _boundPage.AllObjects)
            {
                if (item is BalloonData balloon && visuals.TryGetValue(balloon.ObjectId, out var balloonVisual))
                    Canvas.SetZIndex(balloonVisual, item.ZIndex);
                else if (item is MojiData moji && panels.TryGetValue(moji.ObjectId, out var mojiVisual))
                    Canvas.SetZIndex(mojiVisual, item.ZIndex);
                else if (item is AttachedSymbolData symbol && symbols.TryGetValue(symbol.ObjectId, out var symbolVisual))
                    Canvas.SetZIndex(symbolVisual, item.ZIndex);
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

        private bool HasObjectId(Guid objectId)
            => _mojiPanels.Any(panel => panel.MojiData.ObjectId == objectId)
                || _balloonVisuals.Any(balloon => balloon.ObjectId == objectId)
                || _attachedSymbolModels.Any(symbol => symbol.ObjectId == objectId)
                || _attachedSymbolVisuals.Any(visual => visual.ObjectId == objectId);

        private int GetNextZIndex()
        {
            var pageMaximum = _boundPage?.AllObjects.Select(item => item.ZIndex).DefaultIfEmpty(-1).Max() ?? -1;
            var liveMaximum = _mojiPanels.Select(panel => panel.MojiData.ZIndex)
                .Concat(_balloonVisuals.Select(visual => visual.BalloonData.ZIndex))
                .Concat(_attachedSymbolModels.Select(symbol => symbol.ZIndex))
                .DefaultIfEmpty(-1)
                .Max();
            return Math.Max(pageMaximum, liveMaximum) + 1;
        }

        private void CapturePage(PageDocument page)
        {
            var sourceSymbols = _attachedSymbolModels.Select(PageDocument.CloneAttachedSymbolData).ToList();
            foreach (var visual in _attachedSymbolVisuals)
            {
                var index = sourceSymbols.FindIndex(symbol => symbol.ObjectId == visual.ObjectId);
                if (index >= 0) sourceSymbols[index] = PageDocument.CloneAttachedSymbolData(visual.SymbolData);
                else sourceSymbols.Add(PageDocument.CloneAttachedSymbolData(visual.SymbolData));
            }

            // Validate the complete editor state on a disposable document before
            // changing the bound document. This keeps Capture atomic on bad data.
            var trial = page.Clone(preserveObjectIds: true);
            trial.SetMojiDatas(_mojiPanels.Select(panel => PageDocument.CloneMojiData(panel.MojiData)));
            var symbols = MergeAttachedSymbolStates(trial.AttachedSymbols, sourceSymbols);
            trial.SetAttachedSymbols(symbols);
            trial.SetBalloons(_balloonVisuals.Select(visual => PageDocument.CloneBalloonData(visual.BalloonData)));

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
            symbols = MergeAttachedSymbolStates(page.AttachedSymbols, sourceSymbols);
            page.SetAttachedSymbols(symbols);
            page.SetBalloons(_balloonVisuals.Select(visual => visual.BalloonData));
            _attachedSymbolModels.Clear();
            _attachedSymbolModels.AddRange(page.AttachedSymbols.Select(PageDocument.CloneAttachedSymbolData));
            foreach (var visual in _attachedSymbolVisuals)
            {
                var restored = page.AttachedSymbols.FirstOrDefault(symbol => symbol.ObjectId == visual.ObjectId);
                if (restored == null) continue;
                var parent = _mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == restored.ParentId);
                if (parent != null) visual.ApplyData(PageDocument.CloneAttachedSymbolData(restored), parent.MojiData,
                    parent.GetGraphemeAnchorBounds(restored.GraphemeAnchor, MainCanvas));
            }
            RebuildCanvasObjectOrder();
        }

        private static List<AttachedSymbolData> MergeAttachedSymbolStates(
            IEnumerable<AttachedSymbolData> reconciled,
            IEnumerable<AttachedSymbolData> source)
        {
            var reconciledById = reconciled.ToDictionary(symbol => symbol.ObjectId);
            var merged = new List<AttachedSymbolData>();
            foreach (var sourceSymbol in source)
            {
                var candidate = PageDocument.CloneAttachedSymbolData(sourceSymbol);
                if (reconciledById.TryGetValue(candidate.ObjectId, out var state))
                {
                    candidate.ParentId = state.ParentId;
                    candidate.GraphemeAnchor = state.GraphemeAnchor;
                    candidate.AnchorText = state.AnchorText;
                    candidate.IsDetached = state.IsDetached;
                }
                merged.Add(candidate);
            }
            return merged;
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

    internal sealed class AttachedSymbolDragState
    {
        public AttachedSymbolDragState(AttachedSymbolVisual visual, Point start, AttachedSymbolData before, string coalesceKey)
        {
            Visual = visual;
            Start = start;
            Before = before;
            CoalesceKey = coalesceKey;
        }

        public AttachedSymbolVisual Visual { get; }
        public Point Start { get; }
        public AttachedSymbolData Before { get; }
        public string CoalesceKey { get; }
    }
}
