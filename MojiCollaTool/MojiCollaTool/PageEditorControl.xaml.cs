using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
        private readonly Dictionary<Ellipse, ResizeHandle> _tailHandles = new();
        private readonly Dictionary<FrameworkElement, ObjectContextMenuBinding> _objectContextMenus = new();
        private readonly TextLayoutService _textLayoutService = new();
        private readonly ObservableCollection<TextLinkCandidate> _textLinkCandidates = new();
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
        private const double TailHandleSize = 10;

        public PageEditorControl()
        {
            InitializeComponent();
            MojiListView.ItemsSource = _viewMojiPanels;
            TextLinkComboBox.ItemsSource = _textLinkCandidates;
            MainCanvas.PreviewMouseLeftButtonDown += MainCanvas_PreviewMouseLeftButtonDown;
            MainCanvas.PreviewMouseRightButtonDown += MainCanvas_PreviewMouseRightButtonDown;
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

        public bool? SelectedObjectIsLocked
        {
            get
            {
                var id = SelectedObjectId;
                if (!id.HasValue || _boundPage == null) return null;
                return _boundPage.AllObjects.FirstOrDefault(item => item.ObjectId == id.Value)?.IsLocked;
            }
        }

        internal void SelectAttachedSymbolFromUi(AttachedSymbolVisual visual)
        {
            if (visual == null || !_attachedSymbolVisuals.Contains(visual)) return;
            SelectAttachedSymbol(visual);
        }

        internal void SelectBalloonFromUi(BalloonVisual visual)
        {
            if (visual == null || !_balloonVisuals.Contains(visual)) return;
            SelectBalloon(visual);
        }

        internal void SelectObjectForContextFromUi(Guid objectId)
            => SelectObjectForContext(objectId);

        internal void HandleMojiListItemClickFromUi(MojiPanel panel)
        {
            if (panel == null || !_mojiPanels.Contains(panel)) return;
            if (panel.MojiData.IsLocked)
            {
                _suppressSelectionSync = true;
                try { MojiListView.SelectedItem = null; }
                finally { _suppressSelectionSync = false; }
                SetBalloonStatus("ロック中の文字は左クリックで選択できません。右クリックからロックを解除してください。", false);
                return;
            }
            SelectAttachedSymbol(null);
            SelectBalloon(null);
            _suppressSelectionSync = true;
            try { MojiListView.SelectedItem = panel; }
            finally { _suppressSelectionSync = false; }
            RefreshBalloonTools();
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
                RebuildLinkedTextLayouts();
                foreach (var parent in _mojiPanels) RefreshAttachedSymbolsForParent(parent);
                RestoreSelectionForPage(page.PageId);
                foreach (var parent in _mojiPanels) parent.MojiWindow?.LoadMojiDataToWindow(parent.MojiData);
                RefreshBalloonTools();
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
            ConfigureObjectContextMenu(mojiPanel, mojiPanel.MojiData.ObjectId);
            RefreshBalloonTools();
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
            if (parent.MojiData.IsLocked) throw new InvalidOperationException("親文字がロック中のため、付加記号を変更できません。");
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
            if (!CanEditAttachedSymbol(visual)) return;
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
            if (!CanEditAttachedSymbol(visual)) return false;
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
            if (visual.BalloonData.IsLocked || IsLinkedTextLocked(visual.BalloonData)) return false;
            RemoveBalloonVisual(visual);
            RaiseContentChanged("フキダシ削除");
            return true;
        }

        internal bool AddTailToSelectedBalloon()
        {
            if (_selectedBalloon == null || _selectedBalloon.BalloonData.IsLocked || !_selectedBalloon.BalloonData.IsVisible)
                return SetBalloonStatus("編集できるフキダシを選択してください。", false);
            if (_selectedBalloon.BalloonData.Tail != null)
                return SetBalloonStatus("選択中のフキダシには既にしっぽがあります。", false);
            _selectedBalloon.BalloonData.Tail = BalloonTailGeometry.CreateDefault(_selectedBalloon.BalloonData);
            _selectedBalloon.Refresh();
            UpdateResizeHandles();
            RefreshBalloonTools("しっぽを追加しました。");
            RaiseContentChanged("フキダシしっぽ追加");
            return true;
        }

        internal bool RemoveTailFromSelectedBalloon()
        {
            if (_selectedBalloon == null || _selectedBalloon.BalloonData.IsLocked || !_selectedBalloon.BalloonData.IsVisible)
                return SetBalloonStatus("編集できるフキダシを選択してください。", false);
            if (_selectedBalloon.BalloonData.Tail == null)
                return SetBalloonStatus("選択中のフキダシにしっぽはありません。", false);
            _selectedBalloon.BalloonData.Tail = null;
            _selectedBalloon.Refresh();
            UpdateResizeHandles();
            RefreshBalloonTools("しっぽを削除しました。");
            RaiseContentChanged("フキダシしっぽ削除");
            return true;
        }

        internal bool TryLinkSelectedBalloon(Guid textObjectId)
        {
            if (_selectedBalloon == null)
                return SetBalloonStatus("フキダシを選択してください。", false);
            if (_selectedBalloon.BalloonData.IsLocked)
                return SetBalloonStatus("ロック中のフキダシはリンクを変更できません。", false);
            var panel = _mojiPanels.FirstOrDefault(item => item.MojiData.ObjectId == textObjectId);
            if (panel == null)
                return SetBalloonStatus("同じページにリンク対象の文字が見つかりません。", false);
            if (panel.MojiData.IsLocked)
                return SetBalloonStatus("ロック中の文字はリンク対象にできません。", false);
            var oldLinkedPanel = _selectedBalloon.BalloonData.TextLink == null ? null :
                _mojiPanels.FirstOrDefault(item => item.MojiData.ObjectId == _selectedBalloon.BalloonData.TextLink.TextObjectId);
            if (_selectedBalloon.BalloonData.TextLink != null &&
                (oldLinkedPanel == null || oldLinkedPanel.MojiData.IsLocked))
                return SetBalloonStatus("ロック中のリンク文字との関係は変更できません。", false);
            var owner = _balloonVisuals.FirstOrDefault(item => item.ObjectId != _selectedBalloon.ObjectId &&
                item.BalloonData.TextLink?.TextObjectId == textObjectId);
            if (owner != null)
                return SetBalloonStatus("その文字は別のフキダシにリンクされています。", false);
            if (_selectedBalloon.BalloonData.TextLink?.TextObjectId == textObjectId)
                return SetBalloonStatus("選択した文字は既にリンクされています。", false);

            var snapshot = CaptureLinkMutationSnapshot();
            try
            {
                _selectedBalloon.BalloonData.TextLink = new TextLinkData { TextObjectId = textObjectId };
                SynchronizeBoundPageAfterLinkMutation();
                RefreshBalloonTools($"文字 ID:{panel.MojiData.Id} をリンクしました。");
            }
            catch (Exception)
            {
                RestoreLinkMutationSnapshot(snapshot);
                RefreshBalloonTools("文字リンクに失敗しました。");
                SetBalloonStatus("文字リンクに失敗しました。", false);
                return false;
            }
            // This is the semantic commit boundary.  Subscriber exceptions
            // must propagate to the existing application error boundary and
            // must never be translated into a recoverable link failure.
            RaiseContentChanged("フキダシ文字リンク");
            return true;
        }

        internal bool UnlinkSelectedBalloon()
        {
            if (_selectedBalloon == null)
                return SetBalloonStatus("フキダシを選択してください。", false);
            if (_selectedBalloon.BalloonData.IsLocked)
                return SetBalloonStatus("ロック中のフキダシはリンクを変更できません。", false);
            if (_selectedBalloon.BalloonData.TextLink == null)
                return SetBalloonStatus("選択中のフキダシに文字リンクはありません。", false);
            var linkedPanel = _mojiPanels.FirstOrDefault(item => item.MojiData.ObjectId == _selectedBalloon.BalloonData.TextLink.TextObjectId);
            if (linkedPanel == null || linkedPanel.MojiData.IsLocked)
                return SetBalloonStatus("ロック中の文字はリンクを変更できません。", false);
            var snapshot = CaptureLinkMutationSnapshot();
            try
            {
                _selectedBalloon.BalloonData.TextLink = null;
                SynchronizeBoundPageAfterLinkMutation();
                RefreshBalloonTools("文字リンクを解除しました。");
            }
            catch (Exception)
            {
                RestoreLinkMutationSnapshot(snapshot);
                RefreshBalloonTools("文字リンク解除に失敗しました。");
                SetBalloonStatus("文字リンク解除に失敗しました。", false);
                return false;
            }
            RaiseContentChanged("フキダシ文字リンク解除");
            return true;
        }

        public bool ApplySelectedTextLayout()
        {
            if (_selectedBalloon == null || _boundPage == null)
                return SetBalloonStatus("レイアウト対象のフキダシを選択してください。", false);
            if (_selectedBalloon.BalloonData.TextLink is not TextLinkData currentLink)
                return SetBalloonStatus("文字リンクのあるフキダシを選択してください。", false);
            var panel = _mojiPanels.FirstOrDefault(item => item.MojiData.ObjectId == currentLink.TextObjectId);
            if (panel == null)
                return SetBalloonStatus("リンク先の文字が見つかりません。", false);

            var mode = ReadSelectedLayoutMode(currentLink.LayoutMode);
            var alignment = ReadSelectedAlignment(currentLink.Alignment);
            if (mode == BalloonTextLayoutMode.FitTextToBalloon &&
                (panel.MojiData.IsLocked || !panel.MojiData.IsVisible))
                return SetBalloonStatus("文字側がロック中または非表示のため、レイアウトを適用できません。", false);
            if (mode == BalloonTextLayoutMode.FitBalloonToText &&
                (_selectedBalloon.BalloonData.IsLocked || !_selectedBalloon.BalloonData.IsVisible))
                return SetBalloonStatus("フキダシ側がロック中または非表示のため、レイアウトを適用できません。", false);
            if (!TryReadLayoutSettings(panel.MojiData, _selectedBalloon.BalloonData, mode, out var padding,
                out var minimumFontSize, out var validationMessage))
                return SetBalloonStatus(validationMessage, false);

            var snapshot = CaptureLinkMutationSnapshot();
            var request = TextLayoutRequest.From(panel.MojiData, currentLink, _selectedBalloon.BalloonData);
            request.Padding = padding;
            request.MinimumFontSize = minimumFontSize;
            request.MinimumFrameWidth = MinimumBalloonSize;
            request.MinimumFrameHeight = MinimumBalloonSize;
            request.Alignment = alignment;
            TextLayoutResult layout;
            BalloonData candidateBalloon;
            MojiData candidateText;
            try
            {
                candidateBalloon = _selectedBalloon.BalloonData.Clone();
                candidateText = panel.MojiData.Clone();
                candidateBalloon.TextLink ??= currentLink.Clone();
                candidateBalloon.TextLink.LayoutMode = mode;
                candidateBalloon.TextLink.Alignment = alignment;
                candidateBalloon.TextLink.Padding = padding;
                candidateBalloon.TextLink.MinimumFontSize = minimumFontSize;

                if (mode == BalloonTextLayoutMode.FitTextToBalloon)
                {
                    layout = _textLayoutService.FitTextToBalloon(request);
                    candidateText.FontSize = ClampFontSize(layout.EffectiveFontSize, minimumFontSize);
                    request.FontSize = candidateText.FontSize;
                    layout = _textLayoutService.FitTextToBalloon(request);
                    candidateText.X = layout.TargetTextPosition.X;
                    candidateText.Y = layout.TargetTextPosition.Y;
                }
                else
                {
                    layout = _textLayoutService.FitBalloonToText(request);
                    var bounds = candidateBalloon.Bounds;
                    candidateBalloon.Position = layout.TargetBalloonPosition;
                    var targetBounds = layout.TargetBalloonBounds;
                    candidateBalloon.Bounds = new Rect(bounds.X, bounds.Y, targetBounds.Width, targetBounds.Height);
                }

                var beforeBalloon = snapshot.Balloons[_selectedBalloon.ObjectId];
                var beforeText = snapshot.MojiDatas[panel.MojiData.ObjectId];
                var changed = !TextLayoutEquivalent(beforeBalloon.TextLink, candidateBalloon.TextLink) ||
                    beforeText.FontSize != candidateText.FontSize || beforeText.X != candidateText.X ||
                    beforeText.Y != candidateText.Y || beforeBalloon.X != candidateBalloon.X ||
                    beforeBalloon.Y != candidateBalloon.Y || beforeBalloon.Bounds != candidateBalloon.Bounds;
                _selectedBalloon.ApplyData(candidateBalloon);
                panel.MojiData.Copy(candidateText);
                if (mode == BalloonTextLayoutMode.FitTextToBalloon ||
                    !TextLayoutEquivalent(currentLink, candidateBalloon.TextLink) || panel.ComputedLayout == null)
                    panel.ApplyComputedLayout(layout);
                RefreshBalloonTools(layout.Warning ?? "レイアウトを適用しました。");
                if (!changed) return true;
                SynchronizeBoundPageAfterLinkMutation();
            }
            catch (Exception)
            {
                RestoreLinkMutationSnapshot(snapshot);
                RefreshBalloonTools("レイアウト適用に失敗しました。");
                return false;
            }

            // This is deliberately outside the rollback catch. Once the page
            // and live visuals are synchronized, a subscriber exception must
            // propagate while preserving the committed state.
            RaiseContentChanged("文字レイアウト適用", Guid.NewGuid().ToString("N"));
            return true;
        }

        public bool ApplyTextLayout(BalloonTextLayoutMode mode)
        {
            SelectLayoutMode(mode);
            return ApplySelectedTextLayout();
        }

        public bool FitTextToBalloon() => ApplyTextLayout(BalloonTextLayoutMode.FitTextToBalloon);
        public bool FitBalloonToText() => ApplyTextLayout(BalloonTextLayoutMode.FitBalloonToText);

        private void SynchronizeBoundPageAfterLinkMutation()
        {
            if (_boundPage == null) return;
            // CapturePage validates a complete trial document before touching
            // the bound page, then refreshes model ZIndex and Canvas.Children.
            CapturePage(_boundPage);
            ApplyPageOrderToLiveObjects();
            RebuildCanvasObjectOrder();
        }

        private LinkMutationSnapshot CaptureLinkMutationSnapshot()
        {
            return new LinkMutationSnapshot(
                _boundPage?.Clone(_boundPage.PageId, preserveObjectIds: true),
                PageDocument.CloneCanvas(CanvasData),
                ScalePercent,
                SelectedObjectId,
                _selectedBalloon?.BalloonData.Clone(),
                _mojiPanels.ToDictionary(panel => panel.MojiData.ObjectId, panel => PageDocument.CloneMojiData(panel.MojiData)),
                _mojiPanels.ToDictionary(panel => panel.MojiData.ObjectId, panel => panel.ComputedLayout),
                _balloonVisuals.ToDictionary(visual => visual.ObjectId, visual => PageDocument.CloneBalloonData(visual.BalloonData)),
                _attachedSymbolModels.ToDictionary(symbol => symbol.ObjectId, symbol => PageDocument.CloneAttachedSymbolData(symbol)),
                _attachedSymbolVisuals.ToDictionary(visual => visual.ObjectId, visual => PageDocument.CloneAttachedSymbolData(visual.SymbolData)));
        }

        private void RestoreLinkMutationSnapshot(LinkMutationSnapshot snapshot)
        {
            var wasSuppressed = _suppressChanges;
            _suppressChanges = true;
            try
            {
                if (_boundPage != null && snapshot.Page != null)
                {
                    _boundPage.RestoreFrom(snapshot.Page);
                    CanvasData = PageDocument.CloneCanvas(snapshot.Canvas);
                    UpdateCanvas();
                    foreach (var panel in _mojiPanels)
                    {
                        if (!snapshot.MojiDatas.TryGetValue(panel.MojiData.ObjectId, out var data)) continue;
                        panel.MojiData = PageDocument.CloneMojiData(data);
                        snapshot.ComputedLayouts.TryGetValue(panel.MojiData.ObjectId, out var layout);
                        panel.ApplyComputedLayout(layout);
                    }
                    foreach (var visual in _balloonVisuals)
                    {
                        if (snapshot.Balloons.TryGetValue(visual.ObjectId, out var data))
                            visual.ApplyData(PageDocument.CloneBalloonData(data));
                    }
                    _attachedSymbolModels.Clear();
                    _attachedSymbolModels.AddRange(snapshot.SymbolModels.Values.Select(PageDocument.CloneAttachedSymbolData));
                    foreach (var visual in _attachedSymbolVisuals)
                    {
                        if (!snapshot.Symbols.TryGetValue(visual.ObjectId, out var data)) continue;
                        var parent = _mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == data.ParentId);
                        if (parent != null)
                            visual.ApplyData(PageDocument.CloneAttachedSymbolData(data), parent.MojiData,
                                parent.GetGraphemeAnchorBounds(data.GraphemeAnchor, MainCanvas));
                    }
                    ApplyPageOrderToLiveObjects();
                    RebuildCanvasObjectOrder();
                    RestoreViewState(snapshot.ScalePercent, snapshot.SelectedObjectId);
                }
                else if (_selectedBalloon != null && snapshot.SelectedBalloon != null)
                {
                    _selectedBalloon.ApplyData(PageDocument.CloneBalloonData(snapshot.SelectedBalloon));
                    RefreshBalloonTools();
                }
            }
            finally
            {
                _suppressChanges = wasSuppressed;
            }
        }

        internal bool MoveSelectedObject(ObjectOrderOperation operation)
        {
            var objectId = SelectedObjectId;
            if (!objectId.HasValue || _boundPage == null)
                return SetBalloonStatus("オブジェクトを選択してください。", false);

            CapturePage(_boundPage);
            var trial = _boundPage.Clone(preserveObjectIds: true);
            if (!trial.MoveObjectOrder(objectId.Value, operation))
            {
                var block = trial.GetObjectOrderBlock(objectId.Value);
                return SetBalloonStatus(block.Any(id => trial.GetDocumentObject(id).IsLocked)
                    ? "ロック中のオブジェクトを含むため、重なり順を変更できません。"
                    : "これ以上は重なり順を変更できません。", false);
            }

            if (!_boundPage.MoveObjectOrder(objectId.Value, operation))
                return SetBalloonStatus("重なり順の変更に失敗しました。", false);
            ApplyPageOrderToLiveObjects();
            RebuildCanvasObjectOrder();
            RefreshBalloonTools("重なり順を変更しました。");
            RaiseContentChanged("オブジェクト重なり順変更");
            return true;
        }

        internal bool SetSelectedObjectLocked(bool isLocked)
        {
            var objectId = SelectedObjectId;
            if (!objectId.HasValue || _boundPage == null)
                return SetBalloonStatus("ロック対象を選択してください。", false);
            var current = _boundPage.GetDocumentObject(objectId.Value);
            if (current.IsLocked == isLocked)
            {
                RefreshBalloonTools(isLocked ? "既にロックされています。" : "既にロック解除されています。");
                return false;
            }

            CancelActiveGesturesBeforeLockMutation();
            CapturePage(_boundPage);
            if (!_boundPage.SetObjectLocked(objectId.Value, isLocked)) return false;
            ApplyObjectLockStateToLiveObjects();
            RefreshBalloonTools(isLocked ? "ロックしました。" : "ロックを解除しました。");
            RaiseContentChanged(isLocked ? "オブジェクトをロック" : "オブジェクトのロック解除");
            return true;
        }

        internal bool MoveSelectedBalloonComposition(BalloonCompositionOrder operation)
        {
            if (_selectedBalloon == null || _boundPage == null)
                return SetBalloonStatus("フキダシを選択してください。", false);
            CapturePage(_boundPage);
            var trial = _boundPage.Clone(preserveObjectIds: true);
            if (!trial.MoveBalloonComposition(_selectedBalloon.ObjectId, operation))
            {
                var block = trial.GetObjectOrderBlock(_selectedBalloon.ObjectId);
                return SetBalloonStatus(block.Any(id => trial.GetDocumentObject(id).IsLocked)
                    ? "ロック中のオブジェクトを含むため、重なり順を変更できません。"
                    : "これ以上は重なり順を変更できません。", false);
            }
            if (!_boundPage.MoveBalloonComposition(_selectedBalloon.ObjectId, operation))
                return SetBalloonStatus("重なり順の変更に失敗しました。", false);
            ApplyPageOrderToLiveObjects();
            RebuildCanvasObjectOrder();
            RefreshBalloonTools("重なり順を変更しました。");
            RaiseContentChanged("フキダシ重なり順変更");
            return true;
        }

        public void ReproductionMoji(MojiPanel mojiPanel)
        {
            ArgumentNullException.ThrowIfNull(mojiPanel);
            AddMojiPanel(new MojiPanel(mojiPanel.MojiData.Reproduct(GetNextMojiId()), this));
        }

        public void RemoveMojiPanel(MojiPanel mojiPanel, bool force = false)
        {
            if (mojiPanel == null || !CanRemoveMojiPanel(mojiPanel, force)) return;
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
            foreach (var balloon in _balloonVisuals.Where(item => item.BalloonData.TextLink?.TextObjectId == mojiPanel.MojiData.ObjectId))
                balloon.BalloonData.TextLink = null;
            DetachObjectContextMenu(mojiPanel);
            mojiPanel.Dispose();
            MainCanvas.Children.Remove(mojiPanel);
            RefreshBalloonTools();
            RaiseContentChanged("文字削除");
        }

        private bool CanRemoveMojiPanel(MojiPanel mojiPanel, bool force)
        {
            if (force) return true;
            var objectId = mojiPanel.MojiData.ObjectId;
            if (mojiPanel.MojiData.IsLocked)
            {
                ReportInteractionStatus("ロック中の文字は削除できません。右クリックからロックを解除してください。");
                return false;
            }

            var lockedSymbol = _attachedSymbolVisuals.Any(symbol => symbol.SymbolData.ParentId == objectId &&
                !symbol.SymbolData.IsDetached && symbol.SymbolData.IsLocked) ||
                _attachedSymbolModels.Any(symbol => symbol.ParentId == objectId && !symbol.IsDetached && symbol.IsLocked) ||
                (_boundPage?.AttachedSymbols.Any(symbol => symbol.ParentId == objectId && !symbol.IsDetached && symbol.IsLocked) == true);
            if (lockedSymbol)
            {
                ReportInteractionStatus("関連する付加記号がロック中のため、文字を削除できません。");
                return false;
            }

            var lockedBalloon = _balloonVisuals.Any(balloon => balloon.BalloonData.TextLink?.TextObjectId == objectId &&
                balloon.BalloonData.IsLocked) ||
                (_boundPage?.Balloons.Any(balloon => balloon.TextLink?.TextObjectId == objectId && balloon.IsLocked) == true);
            if (lockedBalloon)
            {
                ReportInteractionStatus("リンク中のフキダシがロック中のため、文字を削除できません。");
                return false;
            }
            return true;
        }

        public void RemoveAllMojiPanel()
        {
            foreach (var panel in _mojiPanels.ToArray()) RemoveMojiPanel(panel, force: true);
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
            foreach (var panel in _mojiPanels.ToArray())
            {
                DetachObjectContextMenu(panel);
                panel.Dispose();
            }
            _mojiPanels.Clear();
            _viewMojiPanels.Clear();
            RemoveAllAttachedSymbolVisuals();
            _attachedSymbolModels.Clear();
            RemoveAllBalloonVisuals();
            foreach (var element in _objectContextMenus.Keys.ToArray()) DetachObjectContextMenu(element);
            MainCanvas.Children.Clear();
            MainCanvas.PreviewMouseRightButtonDown -= MainCanvas_PreviewMouseRightButtonDown;
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
                if (mojiPanel.MojiData.IsLocked)
                {
                    HandleMojiListItemClickFromUi(mojiPanel);
                    e.Handled = true;
                    return;
                }
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

        internal IReadOnlyCollection<Ellipse> TailHandleVisuals => _tailHandles.Keys;

        internal Rectangle CanvasBackgroundVisual => CanvasBackgroundRect;

        internal bool BeginBalloonGesture(Guid balloonId, Point start, BalloonResizeHandle handle = BalloonResizeHandle.Move)
        {
            var visual = _balloonVisuals.FirstOrDefault(candidate => candidate.ObjectId == balloonId);
            if (visual == null || !visual.BalloonData.IsVisible || _balloonDrag != null) return false;
            if (visual.BalloonData.IsLocked)
            {
                SetBalloonStatus("ロック中のフキダシは操作できません。", false);
                return false;
            }
            var linkedPanel = visual.BalloonData.TextLink == null ? null :
                _mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == visual.BalloonData.TextLink.TextObjectId);
            var resizeHandle = ToResizeHandle(handle);
            if (resizeHandle == ResizeHandle.Move && HasLockedLinkedCompositionMember(visual, linkedPanel))
            {
                SetBalloonStatus("ロック中のリンク文字を含むため、フキダシを移動できません。", false);
                return false;
            }
            SelectBalloon(visual);
            _balloonDrag = new BalloonDragState(
                visual,
                start,
                visual.BalloonData.Clone(),
                resizeHandle,
                Guid.NewGuid().ToString("D"),
                linkedPanel,
                linkedPanel == null ? null : PageDocument.CloneMojiData(linkedPanel.MojiData));
            return true;
        }

        internal bool UpdateBalloonGesture(Point current)
        {
            if (_balloonDrag == null) return false;
            if (!CanContinueBalloonGesture())
            {
                CancelBalloonGesture();
                SetBalloonStatus("ロック状態が変わったため、フキダシ操作を取り消しました。", false);
                return false;
            }
            ApplyBalloonDrag(current);
            return true;
        }

        internal bool CommitBalloonGesture(Point current)
        {
            if (_balloonDrag == null) return false;
            var state = _balloonDrag;
            if (!CanContinueBalloonGesture())
            {
                CancelBalloonGesture();
                SetBalloonStatus("ロック状態が変わったため、フキダシ操作を取り消しました。", false);
                return false;
            }
            ApplyBalloonDrag(current);
            var changed = !BalloonEquivalent(state.Before, state.Visual.BalloonData) ||
                !MojiPositionEquivalent(state.BeforeLinkedText, state.LinkedPanel?.MojiData);
            _balloonDrag = null;
            if (changed)
            {
                if (IsFrameResizeHandle(state.Handle) &&
                    state.Visual.BalloonData.TextLink?.TextObjectId is Guid textObjectId)
                    InvalidateLinkedTextLayout(textObjectId);
                RaiseContentChanged("フキダシ位置・サイズ変更", state.CoalesceKey);
            }
            return changed;
        }

        internal void CancelBalloonGesture()
        {
            if (_balloonDrag == null) return;
            var visual = _balloonDrag.Visual;
            var linkedPanel = _balloonDrag.LinkedPanel;
            var linkedBefore = _balloonDrag.BeforeLinkedText;
            _restoringBalloon = true;
            try
            {
                var restoredBalloon = _balloonDrag.Before.Clone();
                restoredBalloon.IsLocked = visual.BalloonData.IsLocked;
                visual.ApplyData(restoredBalloon);
                if (linkedPanel != null && linkedBefore != null)
                {
                    var locked = linkedPanel.MojiData.IsLocked;
                    linkedPanel.MojiData.Copy(linkedBefore);
                    linkedPanel.MojiData.IsLocked = locked;
                    linkedPanel.UpdateXYView();
                }
            }
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
            if (visual == null || _attachedSymbolDrag != null) return false;
            if (!CanEditAttachedSymbol(visual))
            {
                SetBalloonStatus("ロック中の文字または付加記号は操作できません。", false);
                return false;
            }
            SelectAttachedSymbol(visual);
            _attachedSymbolDrag = new AttachedSymbolDragState(
                visual, start, visual.SymbolData.Clone(), Guid.NewGuid().ToString("D"));
            return true;
        }

        internal bool UpdateAttachedSymbolGesture(Point current)
        {
            if (_attachedSymbolDrag == null) return false;
            if (!CanContinueAttachedSymbolGesture())
            {
                CancelAttachedSymbolGesture();
                SetBalloonStatus("ロック状態が変わったため、付加記号操作を取り消しました。", false);
                return false;
            }
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
            if (!CanContinueAttachedSymbolGesture())
            {
                CancelAttachedSymbolGesture();
                SetBalloonStatus("ロック状態が変わったため、付加記号操作を取り消しました。", false);
                return false;
            }
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
            var restored = state.Before.Clone();
            restored.IsLocked = state.Visual.SymbolData.IsLocked;
            state.Visual.ApplyData(restored, state.Visual.ParentTextData, state.Visual.AnchorBounds);
        }

        private bool CanContinueAttachedSymbolGesture()
            => _attachedSymbolDrag != null && CanEditAttachedSymbol(_attachedSymbolDrag.Visual);

        internal void HandleCanvasClickSource(object? source)
        {
            if (source is Rectangle rectangle && _resizeHandles.ContainsKey(rectangle)) return;
            if (source is Ellipse ellipse && _tailHandles.ContainsKey(ellipse)) return;
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

        private void AddTailButton_Click(object sender, RoutedEventArgs e) => AddTailToSelectedBalloon();
        private void RemoveTailButton_Click(object sender, RoutedEventArgs e) => RemoveTailFromSelectedBalloon();

        private void LinkTextButton_Click(object sender, RoutedEventArgs e)
        {
            if (TextLinkComboBox.SelectedItem is not TextLinkCandidate candidate)
            {
                SetBalloonStatus("リンクする文字を選択してください。", false);
                return;
            }
            TryLinkSelectedBalloon(candidate.ObjectId);
        }

        private void UnlinkTextButton_Click(object sender, RoutedEventArgs e) => UnlinkSelectedBalloon();
        private void BringToFrontButton_Click(object sender, RoutedEventArgs e) => MoveSelectedObject(ObjectOrderOperation.BringToFront);
        private void BringForwardButton_Click(object sender, RoutedEventArgs e) => MoveSelectedObject(ObjectOrderOperation.BringForward);
        private void SendBackwardButton_Click(object sender, RoutedEventArgs e) => MoveSelectedObject(ObjectOrderOperation.SendBackward);
        private void SendToBackButton_Click(object sender, RoutedEventArgs e) => MoveSelectedObject(ObjectOrderOperation.SendToBack);
        private void LockButton_Click(object sender, RoutedEventArgs e) => SetSelectedObjectLocked(true);
        private void UnlockButton_Click(object sender, RoutedEventArgs e) => SetSelectedObjectLocked(false);

        private void MainCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HandleCanvasClickSource(e.OriginalSource);
        }

        private void MainCanvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var objectId = FindObjectId(e.OriginalSource as DependencyObject);
            if (objectId.HasValue) SelectObjectForContext(objectId.Value);
        }

        private Guid? FindObjectId(DependencyObject? source)
        {
            var current = source;
            while (current != null && !ReferenceEquals(current, MainCanvas))
            {
                if (current is MojiPanel panel) return panel.MojiData.ObjectId;
                if (current is BalloonVisual balloon) return balloon.ObjectId;
                if (current is AttachedSymbolVisual symbol) return symbol.ObjectId;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void SelectObjectForContext(Guid objectId)
        {
            var text = _mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == objectId);
            if (text != null)
            {
                SelectAttachedSymbol(null);
                SelectBalloon(null);
                _suppressSelectionSync = true;
                try { MojiListView.SelectedItem = text; }
                finally { _suppressSelectionSync = false; }
                RefreshBalloonTools();
                return;
            }

            var symbol = _attachedSymbolVisuals.FirstOrDefault(candidate => candidate.ObjectId == objectId);
            if (symbol != null)
            {
                SelectBalloon(null);
                SelectAttachedSymbol(symbol);
                RefreshBalloonTools();
                return;
            }

            var balloon = _balloonVisuals.FirstOrDefault(candidate => candidate.ObjectId == objectId);
            if (balloon != null)
            {
                SelectAttachedSymbol(null);
                _suppressSelectionSync = true;
                try { MojiListView.SelectedItem = null; }
                finally { _suppressSelectionSync = false; }
                SelectBalloon(balloon);
            }
        }

        private void ConfigureObjectContextMenu(FrameworkElement element, Guid objectId)
        {
            DetachObjectContextMenu(element);
            var menu = new ContextMenu();
            var itemHandlers = new Dictionary<MenuItem, RoutedEventHandler>();
            foreach (var action in Enum.GetValues<ObjectContextAction>())
            {
                var item = new MenuItem { Header = GetContextMenuHeader(action), Tag = action };
                RoutedEventHandler clickHandler = (_, _) => ExecuteContextAction(objectId, action);
                item.Click += clickHandler;
                itemHandlers.Add(item, clickHandler);
                menu.Items.Add(item);
            }
            RoutedEventHandler openedHandler = (_, _) => RefreshContextMenu(menu, objectId);
            menu.Opened += openedHandler;
            _objectContextMenus.Add(element, new ObjectContextMenuBinding(menu, openedHandler, itemHandlers));
            element.ContextMenu = menu;
        }

        private void DetachObjectContextMenu(FrameworkElement element)
        {
            if (_objectContextMenus.Remove(element, out var binding))
            {
                binding.Menu.Opened -= binding.OpenedHandler;
                foreach (var pair in binding.ItemHandlers) pair.Key.Click -= pair.Value;
                if (binding.Menu.IsOpen) binding.Menu.IsOpen = false;
            }
            element.ContextMenu = null;
        }

        private void RefreshContextMenu(ContextMenu menu, Guid objectId)
        {
            var pageObject = _boundPage?.AllObjects.FirstOrDefault(item => item.ObjectId == objectId)
                ?? (IPageObjectData?)_mojiPanels.FirstOrDefault(item => item.MojiData.ObjectId == objectId)?.MojiData
                ?? (IPageObjectData?)_balloonVisuals.FirstOrDefault(item => item.ObjectId == objectId)?.BalloonData
                ?? (IPageObjectData?)_attachedSymbolVisuals.FirstOrDefault(item => item.ObjectId == objectId)?.SymbolData;
            if (pageObject == null) return;
            var locked = pageObject.IsLocked;
            var visible = pageObject.IsVisible;
            var canMove = visible && !locked && _boundPage != null && _boundPage.ContainsObject(objectId);
            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                var action = (ObjectContextAction)item.Tag;
                item.IsEnabled = action switch
                {
                    ObjectContextAction.BringToFront => canMove && _boundPage!.CanMoveObjectOrder(objectId, ObjectOrderOperation.BringToFront),
                    ObjectContextAction.BringForward => canMove && _boundPage!.CanMoveObjectOrder(objectId, ObjectOrderOperation.BringForward),
                    ObjectContextAction.SendBackward => canMove && _boundPage!.CanMoveObjectOrder(objectId, ObjectOrderOperation.SendBackward),
                    ObjectContextAction.SendToBack => canMove && _boundPage!.CanMoveObjectOrder(objectId, ObjectOrderOperation.SendToBack),
                    ObjectContextAction.Lock => visible && !locked,
                    ObjectContextAction.Unlock => visible && locked,
                    _ => false,
                };
            }
        }

        private void ExecuteContextAction(Guid objectId, ObjectContextAction action)
        {
            SelectObjectForContext(objectId);
            switch (action)
            {
                case ObjectContextAction.BringToFront: MoveSelectedObject(ObjectOrderOperation.BringToFront); break;
                case ObjectContextAction.BringForward: MoveSelectedObject(ObjectOrderOperation.BringForward); break;
                case ObjectContextAction.SendBackward: MoveSelectedObject(ObjectOrderOperation.SendBackward); break;
                case ObjectContextAction.SendToBack: MoveSelectedObject(ObjectOrderOperation.SendToBack); break;
                case ObjectContextAction.Lock: SetSelectedObjectLocked(true); break;
                case ObjectContextAction.Unlock: SetSelectedObjectLocked(false); break;
            }
        }

        private static string GetContextMenuHeader(ObjectContextAction action)
            => action switch
            {
                ObjectContextAction.BringToFront => "最前面へ",
                ObjectContextAction.BringForward => "前面へ",
                ObjectContextAction.SendBackward => "背面へ",
                ObjectContextAction.SendToBack => "最背面へ",
                ObjectContextAction.Lock => "ロック",
                ObjectContextAction.Unlock => "ロック解除",
                _ => string.Empty,
            };

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

        private void RebuildLinkedTextLayouts()
        {
            foreach (var balloon in _balloonVisuals)
            {
                if (balloon.BalloonData.TextLink is not TextLinkData link) continue;
                if (link.LayoutMode == BalloonTextLayoutMode.Unapplied) continue;
                if (link.LayoutMode != BalloonTextLayoutMode.FitTextToBalloon &&
                    link.LayoutMode != BalloonTextLayoutMode.FitBalloonToText) continue;
                var panel = _mojiPanels.FirstOrDefault(item => item.MojiData.ObjectId == link.TextObjectId);
                if (panel == null) continue;
                var request = TextLayoutRequest.From(panel.MojiData, link, balloon.BalloonData);
                var layout = link.LayoutMode == BalloonTextLayoutMode.FitTextToBalloon
                    ? _textLayoutService.LayoutWithinFrame(request)
                    : _textLayoutService.FitBalloonToText(request);
                panel.ApplyComputedLayout(layout);
            }
        }

        internal void InvalidateLinkedTextLayout(Guid textObjectId)
        {
            foreach (var balloon in _balloonVisuals.Where(item =>
                item.BalloonData.TextLink?.TextObjectId == textObjectId))
            {
                balloon.BalloonData.TextLink!.LayoutMode = BalloonTextLayoutMode.Unapplied;
            }
            _mojiPanels.FirstOrDefault(item => item.MojiData.ObjectId == textObjectId)
                ?.ApplyComputedLayout(null);
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
            ConfigureObjectContextMenu(visual, visual.ObjectId);
            Canvas.SetZIndex(visual, visual.SymbolData.ZIndex);
            if (raiseContentChanged) RaiseContentChanged("付加記号追加");
        }

        private void RemoveAttachedSymbolVisual(AttachedSymbolVisual visual)
        {
            if (!_attachedSymbolVisuals.Remove(visual)) return;
            DetachObjectContextMenu(visual);
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
                RefreshBalloonTools();
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
            RefreshBalloonTools();
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
            ConfigureObjectContextMenu(visual, visual.ObjectId);
            RefreshBalloonTools();
            if (raiseContentChanged) RaiseContentChanged("フキダシ追加");
        }

        private void RemoveBalloonVisual(BalloonVisual visual)
        {
            if (!_balloonVisuals.Remove(visual)) return;
            DetachObjectContextMenu(visual);
            visual.BalloonMouseLeftButtonDown -= BalloonVisual_MouseLeftButtonDown;
            visual.BalloonMouseLeftButtonUp -= BalloonVisual_MouseLeftButtonUpBoundary;
            visual.BalloonMouseMove -= BalloonVisual_MouseMove;
            visual.BalloonLostMouseCapture -= BalloonVisual_LostMouseCaptureBoundary;
            MainCanvas.Children.Remove(visual);
            if (ReferenceEquals(_selectedBalloon, visual)) SelectBalloon(null);
            RefreshBalloonTools();
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
            RefreshBalloonTools();
        }

        private void RefreshBalloonTools(string? message = null)
        {
            var selectedCandidateId = (TextLinkComboBox.SelectedItem as TextLinkCandidate)?.ObjectId
                ?? _selectedBalloon?.BalloonData.TextLink?.TextObjectId;
            _textLinkCandidates.Clear();
            foreach (var panel in _mojiPanels.OrderBy(item => item.MojiData.Id))
                _textLinkCandidates.Add(new TextLinkCandidate(panel.MojiData));
            TextLinkComboBox.SelectedItem = selectedCandidateId.HasValue
                ? _textLinkCandidates.FirstOrDefault(item => item.ObjectId == selectedCandidateId.Value)
                : null;

            var selectedLink = _selectedBalloon?.BalloonData.TextLink;
            SelectLayoutMode(selectedLink?.LayoutMode ?? BalloonTextLayoutMode.FitTextToBalloon);
            SelectLayoutAlignment(selectedLink?.Alignment ?? BalloonTextAlignment.Center);
            TextLayoutPaddingTextBox.Text = selectedLink?.Padding.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;
            TextLayoutMinimumFontSizeTextBox.Text = selectedLink?.MinimumFontSize.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;

            var editable = _selectedBalloon != null && _selectedBalloon.BalloonData.IsVisible && !_selectedBalloon.BalloonData.IsLocked;
            var selectedObject = SelectedObjectId.HasValue
                ? _boundPage?.AllObjects.FirstOrDefault(item => item.ObjectId == SelectedObjectId.Value)
                    ?? (IPageObjectData?)_mojiPanels.FirstOrDefault(item => item.MojiData.ObjectId == SelectedObjectId.Value)?.MojiData
                    ?? (IPageObjectData?)_balloonVisuals.FirstOrDefault(item => item.ObjectId == SelectedObjectId.Value)?.BalloonData
                    ?? (IPageObjectData?)_attachedSymbolVisuals.FirstOrDefault(item => item.ObjectId == SelectedObjectId.Value)?.SymbolData
                : null;
            var objectEditable = selectedObject != null && selectedObject.IsVisible && !selectedObject.IsLocked;
            var selectedId = SelectedObjectId;
            var canMove = selectedId.HasValue && objectEditable && _boundPage != null && _boundPage.ContainsObject(selectedId.Value);
            AddTailButton.IsEnabled = editable && _selectedBalloon!.BalloonData.Tail == null;
            RemoveTailButton.IsEnabled = editable && _selectedBalloon!.BalloonData.Tail != null;
            TextLinkComboBox.IsEnabled = editable && _textLinkCandidates.Count > 0;
            LinkTextButton.IsEnabled = editable && _textLinkCandidates.Count > 0;
            UnlinkTextButton.IsEnabled = editable && _selectedBalloon!.BalloonData.TextLink != null;
            UpdateTextLayoutApplyAvailability();
            BringToFrontButton.IsEnabled = canMove && _boundPage!.CanMoveObjectOrder(selectedId!.Value, ObjectOrderOperation.BringToFront);
            BringForwardButton.IsEnabled = canMove && _boundPage!.CanMoveObjectOrder(selectedId!.Value, ObjectOrderOperation.BringForward);
            SendBackwardButton.IsEnabled = canMove && _boundPage!.CanMoveObjectOrder(selectedId!.Value, ObjectOrderOperation.SendBackward);
            SendToBackButton.IsEnabled = canMove && _boundPage!.CanMoveObjectOrder(selectedId!.Value, ObjectOrderOperation.SendToBack);
            LockButton.IsEnabled = objectEditable;
            UnlockButton.IsEnabled = selectedObject?.IsLocked == true;

            if (message != null)
            {
                BalloonStatusTextBlock.Text = message;
                return;
            }
            if (_selectedBalloon?.BalloonData.TextLink is TextLinkData link)
            {
                var panel = _mojiPanels.FirstOrDefault(item => item.MojiData.ObjectId == link.TextObjectId);
                BalloonStatusTextBlock.Text = panel == null
                    ? "リンク先の文字が見つかりません。"
                    : $"リンク中: ID:{panel.MojiData.Id} {panel.MojiData.ExampleText}";
            }
            else
            {
                BalloonStatusTextBlock.Text = _selectedBalloon == null ? "フキダシ未選択" : "文字リンクなし";
            }
        }

        private void ApplyTextLayoutButton_Click(object sender, RoutedEventArgs e) => ApplySelectedTextLayout();

        private void TextLayoutModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            UpdateTextLayoutApplyAvailability();

        private void UpdateTextLayoutApplyAvailability()
        {
            var link = _selectedBalloon?.BalloonData.TextLink;
            var panel = link == null ? null : _mojiPanels.FirstOrDefault(item => item.MojiData.ObjectId == link.TextObjectId);
            var hasLink = link != null && panel != null;
            var mode = ReadSelectedLayoutMode(link?.LayoutMode ?? BalloonTextLayoutMode.FitTextToBalloon);
            var targetEditable = mode == BalloonTextLayoutMode.FitTextToBalloon
                ? panel != null && !panel.MojiData.IsLocked && panel.MojiData.IsVisible
                : _selectedBalloon != null && !_selectedBalloon.BalloonData.IsLocked && _selectedBalloon.BalloonData.IsVisible;
            ApplyTextLayoutButton.IsEnabled = hasLink && targetEditable;
            TextLayoutModeComboBox.IsEnabled = hasLink;
            TextLayoutAlignmentComboBox.IsEnabled = hasLink;
            TextLayoutPaddingTextBox.IsEnabled = hasLink;
            TextLayoutMinimumFontSizeTextBox.IsEnabled = hasLink;
        }

        private BalloonTextLayoutMode ReadSelectedLayoutMode(BalloonTextLayoutMode fallback)
        {
            var tag = (TextLayoutModeComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
            return Enum.TryParse(tag, true, out BalloonTextLayoutMode result) ? result : fallback;
        }

        private BalloonTextAlignment ReadSelectedAlignment(BalloonTextAlignment fallback)
        {
            var tag = (TextLayoutAlignmentComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
            return Enum.TryParse(tag, true, out BalloonTextAlignment result) ? result : fallback;
        }

        private void SelectLayoutMode(BalloonTextLayoutMode mode)
        {
            var name = mode == BalloonTextLayoutMode.FitBalloonToText ? "FitBalloonToText" : "FitTextToBalloon";
            TextLayoutModeComboBox.SelectedItem = TextLayoutModeComboBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, name, StringComparison.Ordinal));
        }

        private void SelectLayoutAlignment(BalloonTextAlignment alignment)
        {
            var name = alignment switch
            {
                BalloonTextAlignment.Start => "Start",
                BalloonTextAlignment.End => "End",
                _ => "Center",
            };
            TextLayoutAlignmentComboBox.SelectedItem = TextLayoutAlignmentComboBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, name, StringComparison.Ordinal));
        }

        private static int ClampFontSize(double value, double minimum)
        {
            var safeMinimum = double.IsNaN(minimum) || double.IsInfinity(minimum) ? 1 : Math.Max(1, minimum);
            var candidate = double.IsNaN(value) || double.IsInfinity(value) ? safeMinimum : value;
            return Math.Max((int)Math.Ceiling(safeMinimum), Math.Max(1, (int)Math.Floor(candidate)));
        }

        private bool TryReadLayoutSettings(MojiData text, BalloonData balloon, BalloonTextLayoutMode mode,
            out double padding, out double minimumFontSize, out string message)
        {
            padding = 0;
            minimumFontSize = 0;
            message = string.Empty;
            if (!TryParseFiniteNumber(TextLayoutPaddingTextBox.Text, out padding) || padding < 0)
            {
                message = "余白は有限な0以上の数値で入力してください。";
                return false;
            }
            if (!TryParseFiniteNumber(TextLayoutMinimumFontSizeTextBox.Text, out minimumFontSize) || minimumFontSize <= 0)
            {
                message = "最小文字サイズは有限な正の数値で入力してください。";
                return false;
            }
            if (padding > double.MaxValue / 4)
            {
                message = "余白が大きすぎます。有限な数値を入力してください。";
                return false;
            }
            if (minimumFontSize > text.FontSize)
            {
                message = "最小文字サイズは現在の文字サイズ以下にしてください。";
                return false;
            }
            if (double.IsNaN(balloon.Bounds.Width) || double.IsInfinity(balloon.Bounds.Width) ||
                double.IsNaN(balloon.Bounds.Height) || double.IsInfinity(balloon.Bounds.Height) ||
                balloon.Bounds.Width <= 0 || balloon.Bounds.Height <= 0)
            {
                message = "フキダシの大きさが不正です。";
                return false;
            }
            if (mode == BalloonTextLayoutMode.FitTextToBalloon &&
                padding * 2 >= Math.Min(balloon.Bounds.Width, balloon.Bounds.Height))
            {
                message = "余白がフキダシの大きさに対して大きすぎます。";
                return false;
            }
            return true;
        }

        private static bool TryParseFiniteNumber(string? value, out double number)
        {
            number = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var parsed = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number);
            return parsed && !double.IsNaN(number) && !double.IsInfinity(number);
        }

        private static bool TextLayoutEquivalent(TextLinkData? left, TextLinkData? right)
        {
            if (left == null || right == null) return left == right;
            return left.TextObjectId == right.TextObjectId && left.LayoutMode == right.LayoutMode &&
                left.Padding == right.Padding && left.MinimumFontSize == right.MinimumFontSize && left.Alignment == right.Alignment;
        }

        private bool SetBalloonStatus(string message, bool result)
        {
            BalloonStatusTextBlock.Text = message;
            return result;
        }

        internal void ReportInteractionStatus(string message)
            => SetBalloonStatus(message, false);

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
                if (data.Tail != null && before.Tail != null)
                {
                    data.Tail.TipX = before.Tail.TipX + dx;
                    data.Tail.TipY = before.Tail.TipY + dy;
                }
                if (_balloonDrag.LinkedPanel != null && _balloonDrag.BeforeLinkedText != null)
                {
                    _balloonDrag.LinkedPanel.MojiData.X = _balloonDrag.BeforeLinkedText.X + dx;
                    _balloonDrag.LinkedPanel.MojiData.Y = _balloonDrag.BeforeLinkedText.Y + dy;
                    _balloonDrag.LinkedPanel.UpdateXYView();
                }
            }
            else if (_balloonDrag.Handle == ResizeHandle.TailTip && data.Tail != null)
            {
                data.Tail.Tip = current;
                data.Tail.Validate();
            }
            else if (_balloonDrag.Handle == ResizeHandle.TailRoot && data.Tail != null)
            {
                var local = BalloonTailGeometry.PageToLocal(data, current);
                data.Tail.RootParameter = BalloonTailGeometry.FindNearestRootParameter(
                    data.ShapeKind, new Rect(0, 0, data.Bounds.Width, data.Bounds.Height), local, _balloonGeometryFactory);
                data.Tail.Validate();
            }
            else if (_balloonDrag.Handle == ResizeHandle.TailWidth && data.Tail != null)
            {
                var local = BalloonTailGeometry.PageToLocal(data, current);
                var placement = BalloonTailGeometry.GetRootPlacement(data.ShapeKind,
                    new Rect(0, 0, data.Bounds.Width, data.Bounds.Height), data.Tail.RootParameter, _balloonGeometryFactory);
                data.Tail.Width = Math.Max(0, Math.Abs(Vector.Multiply(local - placement.Point, placement.Tangent)) * 2);
                data.Tail.Validate();
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
            => left.X == right.X && left.Y == right.Y && left.Bounds == right.Bounds && left.Rotation == right.Rotation &&
               TailEquivalent(left.Tail, right.Tail);

        private static bool TailEquivalent(BalloonTailData? left, BalloonTailData? right)
            => left == null ? right == null : right != null && left.TipX == right.TipX && left.TipY == right.TipY &&
               left.RootParameter == right.RootParameter && left.Width == right.Width;

        private static bool MojiPositionEquivalent(MojiData? left, MojiData? right)
            => left == null ? right == null : right != null && left.X == right.X && left.Y == right.Y;

        private static bool IsFrameResizeHandle(ResizeHandle handle)
            => handle.HasFlag(ResizeHandle.Left) || handle.HasFlag(ResizeHandle.Right) ||
               handle.HasFlag(ResizeHandle.Top) || handle.HasFlag(ResizeHandle.Bottom);

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

        private void ApplyPageOrderToLiveObjects()
        {
            if (_boundPage == null) return;
            foreach (var item in _boundPage.AllObjects)
            {
                var panel = _mojiPanels.FirstOrDefault(candidate => candidate.MojiData.ObjectId == item.ObjectId);
                if (panel != null) panel.MojiData.ZIndex = item.ZIndex;
                var balloon = _balloonVisuals.FirstOrDefault(candidate => candidate.ObjectId == item.ObjectId);
                if (balloon != null) balloon.BalloonData.ZIndex = item.ZIndex;
                var symbolIndex = _attachedSymbolModels.FindIndex(candidate => candidate.ObjectId == item.ObjectId);
                if (symbolIndex >= 0) _attachedSymbolModels[symbolIndex].ZIndex = item.ZIndex;
                var symbolVisual = _attachedSymbolVisuals.FirstOrDefault(candidate => candidate.ObjectId == item.ObjectId);
                if (symbolVisual != null) symbolVisual.SymbolData.ZIndex = item.ZIndex;
            }
        }

        private void ApplyObjectLockStateToLiveObjects()
        {
            if (_boundPage == null) return;
            foreach (var item in _boundPage.AllObjects)
            {
                var panel = _mojiPanels.FirstOrDefault(candidate => candidate.MojiData.ObjectId == item.ObjectId);
                if (panel != null)
                {
                    panel.MojiData.IsLocked = item.IsLocked;
                    panel.MojiWindow?.LoadMojiDataToWindow(panel.MojiData);
                }

                var balloon = _balloonVisuals.FirstOrDefault(candidate => candidate.ObjectId == item.ObjectId);
                if (balloon != null)
                {
                    var data = balloon.BalloonData.Clone();
                    data.IsLocked = item.IsLocked;
                    balloon.ApplyData(data);
                }

                var symbolIndex = _attachedSymbolModels.FindIndex(candidate => candidate.ObjectId == item.ObjectId);
                if (symbolIndex >= 0) _attachedSymbolModels[symbolIndex].IsLocked = item.IsLocked;
                var symbolVisual = _attachedSymbolVisuals.FirstOrDefault(candidate => candidate.ObjectId == item.ObjectId);
                if (symbolVisual != null)
                {
                    var data = symbolVisual.SymbolData.Clone();
                    data.IsLocked = item.IsLocked;
                    symbolVisual.ApplyData(data, symbolVisual.ParentTextData, symbolVisual.AnchorBounds);
                }
            }
            foreach (var panel in _mojiPanels) panel.MojiWindow?.RefreshEditabilityFromModel();
            UpdateResizeHandles();
        }

        private void CancelActiveGesturesBeforeLockMutation()
        {
            CancelBalloonGesture();
            CancelAttachedSymbolGesture();
            foreach (var panel in _mojiPanels) panel.CancelActiveDragForLock();
        }

        internal bool CanMoveTextWithAttachedSymbols(Guid textObjectId)
        {
            var modelLocked = _attachedSymbolModels.Any(symbol => symbol.ParentId == textObjectId &&
                !symbol.IsDetached && symbol.IsLocked);
            var liveLocked = _attachedSymbolVisuals.Any(symbol => symbol.SymbolData.ParentId == textObjectId &&
                !symbol.SymbolData.IsDetached && symbol.SymbolData.IsLocked);
            return !modelLocked && !liveLocked;
        }

        private bool IsLinkedTextLocked(BalloonData balloon)
            => balloon.TextLink?.TextObjectId is Guid textObjectId &&
               (_mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == textObjectId) is not MojiPanel panel ||
                panel.MojiData.IsLocked);

        private bool CanContinueBalloonGesture()
        {
            if (_balloonDrag == null || _balloonDrag.Visual.BalloonData.IsLocked) return false;
            return _balloonDrag.Handle != ResizeHandle.Move ||
                !HasLockedLinkedCompositionMember(_balloonDrag.Visual, _balloonDrag.LinkedPanel);
        }

        private bool HasLockedLinkedCompositionMember(BalloonVisual visual, MojiPanel? linkedPanel)
        {
            if (linkedPanel?.MojiData.IsLocked == true) return true;
            if (linkedPanel == null) return false;
            return _attachedSymbolVisuals.Any(symbol => symbol.SymbolData.ParentId == linkedPanel.MojiData.ObjectId &&
                !symbol.SymbolData.IsDetached && symbol.SymbolData.IsLocked);
        }

        private void UpdateResizeHandles()
        {
            ClearResizeHandles();
            ClearTailHandles();
            if (_selectedBalloon == null || !_balloonVisuals.Contains(_selectedBalloon) ||
                _selectedBalloon.BalloonData.IsLocked || !_selectedBalloon.BalloonData.IsVisible) return;
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
            if (data.Tail != null)
            {
                var tailSize = TailHandleSize / scale;
                var placement = BalloonTailGeometry.GetRootPlacement(data, _balloonGeometryFactory);
                AddTailHandle(ResizeHandle.TailTip, data.Tail.Tip, tailSize, Brushes.OrangeRed);
                AddTailHandle(ResizeHandle.TailRoot, placement.Point, tailSize, Brushes.LimeGreen);
                AddTailHandle(ResizeHandle.TailWidth, BalloonTailGeometry.GetWidthHandlePoint(data, _balloonGeometryFactory), tailSize, Brushes.Gold);
            }
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
            Canvas.SetZIndex(rectangle, int.MaxValue);
            _resizeHandles.Add(rectangle, handle);
        }

        private void AddTailHandle(ResizeHandle handle, Point center, double size, Brush fill)
        {
            var ellipse = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = fill,
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1,
                Tag = handle,
                Cursor = Cursors.Cross,
            };
            ellipse.MouseLeftButtonDown += TailHandle_MouseLeftButtonDown;
            Canvas.SetLeft(ellipse, center.X - size / 2);
            Canvas.SetTop(ellipse, center.Y - size / 2);
            Canvas.SetZIndex(ellipse, int.MaxValue);
            MainCanvas.Children.Add(ellipse);
            _tailHandles.Add(ellipse, handle);
        }

        private void TailHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Ellipse ellipse || _selectedBalloon == null || !_tailHandles.TryGetValue(ellipse, out var handle)) return;
            if (!BeginBalloonGesture(_selectedBalloon.ObjectId, e.GetPosition(MainCanvas), FromResizeHandle(handle))) return;
            _selectedBalloon.CaptureMouse();
            e.Handled = true;
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
                BalloonResizeHandle.TailTip => ResizeHandle.TailTip,
                BalloonResizeHandle.TailRoot => ResizeHandle.TailRoot,
                BalloonResizeHandle.TailWidth => ResizeHandle.TailWidth,
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
                ResizeHandle.TailTip => BalloonResizeHandle.TailTip,
                ResizeHandle.TailRoot => BalloonResizeHandle.TailRoot,
                ResizeHandle.TailWidth => BalloonResizeHandle.TailWidth,
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

        private void ClearTailHandles()
        {
            foreach (var ellipse in _tailHandles.Keys.ToArray())
            {
                ellipse.MouseLeftButtonDown -= TailHandle_MouseLeftButtonDown;
                MainCanvas.Children.Remove(ellipse);
            }
            _tailHandles.Clear();
        }

        [Flags]
        private enum ResizeHandle
        {
            Move = 0, Left = 1, Right = 2, Top = 4, Bottom = 8,
            TopLeft = Top | Left, TopRight = Top | Right,
            BottomLeft = Bottom | Left, BottomRight = Bottom | Right,
            TailTip = 16, TailRoot = 32, TailWidth = 64,
        }

        private sealed class BalloonDragState
        {
            public BalloonDragState(BalloonVisual visual, Point start, BalloonData before, ResizeHandle handle, string coalesceKey,
                MojiPanel? linkedPanel, MojiData? beforeLinkedText)
            {
                Visual = visual; Start = start; Before = before; Handle = handle; CoalesceKey = coalesceKey;
                LinkedPanel = linkedPanel; BeforeLinkedText = beforeLinkedText;
            }
            public BalloonVisual Visual { get; }
            public Point Start { get; }
            public BalloonData Before { get; }
            public ResizeHandle Handle { get; }
            public string CoalesceKey { get; }
            public MojiPanel? LinkedPanel { get; }
            public MojiData? BeforeLinkedText { get; }
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

        private bool CanEditAttachedSymbol(AttachedSymbolVisual visual)
        {
            if (visual.SymbolData.IsLocked) return false;
            if (visual.SymbolData.ParentId is not Guid parentId) return true;
            return _mojiPanels.FirstOrDefault(panel => panel.MojiData.ObjectId == parentId)?.MojiData.IsLocked != true;
        }

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

        private enum ObjectContextAction
        {
            BringToFront,
            BringForward,
            SendBackward,
            SendToBack,
            Lock,
            Unlock,
        }

        private sealed class ObjectContextMenuBinding
        {
            public ObjectContextMenuBinding(ContextMenu menu, RoutedEventHandler openedHandler,
                IReadOnlyDictionary<MenuItem, RoutedEventHandler> itemHandlers)
            {
                Menu = menu;
                OpenedHandler = openedHandler;
                ItemHandlers = itemHandlers;
            }

            public ContextMenu Menu { get; }
            public RoutedEventHandler OpenedHandler { get; }
            public IReadOnlyDictionary<MenuItem, RoutedEventHandler> ItemHandlers { get; }
        }
    }

    internal sealed class LinkMutationSnapshot
    {
        public LinkMutationSnapshot(
            PageDocument? page,
            CanvasData canvas,
            int scalePercent,
            Guid? selectedObjectId,
            BalloonData? selectedBalloon,
            IReadOnlyDictionary<Guid, MojiData> mojiDatas,
            IReadOnlyDictionary<Guid, TextLayoutResult?> computedLayouts,
            IReadOnlyDictionary<Guid, BalloonData> balloons,
            IReadOnlyDictionary<Guid, AttachedSymbolData> symbolModels,
            IReadOnlyDictionary<Guid, AttachedSymbolData> symbols)
        {
            Page = page;
            Canvas = canvas;
            ScalePercent = scalePercent;
            SelectedObjectId = selectedObjectId;
            SelectedBalloon = selectedBalloon;
            MojiDatas = mojiDatas;
            ComputedLayouts = computedLayouts;
            Balloons = balloons;
            SymbolModels = symbolModels;
            Symbols = symbols;
        }

        public PageDocument? Page { get; }
        public CanvasData Canvas { get; }
        public int ScalePercent { get; }
        public Guid? SelectedObjectId { get; }
        public BalloonData? SelectedBalloon { get; }
        public IReadOnlyDictionary<Guid, MojiData> MojiDatas { get; }
        public IReadOnlyDictionary<Guid, TextLayoutResult?> ComputedLayouts { get; }
        public IReadOnlyDictionary<Guid, BalloonData> Balloons { get; }
        public IReadOnlyDictionary<Guid, AttachedSymbolData> SymbolModels { get; }
        public IReadOnlyDictionary<Guid, AttachedSymbolData> Symbols { get; }
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
        TailTip = 16,
        TailRoot = 32,
        TailWidth = 64,
    }

    internal sealed class TextLinkCandidate
    {
        public TextLinkCandidate(MojiData data)
        {
            ObjectId = data.ObjectId;
            DisplayText = $"ID:{data.Id} [{data.ObjectId.ToString("D")[..8]}] {data.ExampleText}";
        }

        public Guid ObjectId { get; }
        public string DisplayText { get; }
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
