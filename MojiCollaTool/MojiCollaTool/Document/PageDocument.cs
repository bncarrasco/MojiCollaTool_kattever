using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace MojiCollaTool
{
    /// <summary>
    /// 1ページ分の永続化対象データを表す文書モデルです。
    /// WPFの表示要素や選択状態は保持しません。
    /// </summary>
    public sealed class PageDocument
    {
        private readonly List<MojiData> _mojiDatas;
        private readonly List<BalloonData> _balloons;
        private readonly List<AttachedSymbolData> _attachedSymbols;
        private readonly List<Guid> _objectOrder = new List<Guid>();

        public PageDocument(string name)
            : this(Guid.NewGuid(), name, new CanvasData(), Enumerable.Empty<MojiData>())
        {
        }

        public PageDocument(string name, IEnumerable<MojiData> mojiDatas)
            : this(Guid.NewGuid(), name, new CanvasData(), mojiDatas)
        {
        }

        public PageDocument(string name, IEnumerable<MojiData> mojiDatas, IEnumerable<BalloonData> balloons)
            : this(Guid.NewGuid(), name, new CanvasData(), mojiDatas, balloons)
        {
        }

        public PageDocument(string name, IEnumerable<BalloonData> balloons)
            : this(Guid.NewGuid(), name, new CanvasData(), Enumerable.Empty<MojiData>(), balloons)
        {
        }

        public PageDocument(
            Guid pageId,
            string name,
            CanvasData canvas,
            IEnumerable<MojiData> mojiDatas)
            : this(pageId, name, canvas, mojiDatas, Enumerable.Empty<BalloonData>())
        {
        }

        public PageDocument(
            Guid pageId,
            string name,
            CanvasData canvas,
            IEnumerable<MojiData> mojiDatas,
            IEnumerable<BalloonData> balloons)
            : this(pageId, name, canvas, mojiDatas, balloons, Enumerable.Empty<AttachedSymbolData>())
        {
        }

        public PageDocument(
            Guid pageId,
            string name,
            CanvasData canvas,
            IEnumerable<MojiData> mojiDatas,
            IEnumerable<BalloonData> balloons,
            IEnumerable<AttachedSymbolData> attachedSymbols)
        {
            if (pageId == Guid.Empty) throw new ArgumentException("Page ID must not be empty.", nameof(pageId));
            PageId = pageId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Canvas = new CanvasDocument(canvas ?? throw new ArgumentNullException(nameof(canvas)));
            _mojiDatas = (mojiDatas ?? throw new ArgumentNullException(nameof(mojiDatas)))
                .Select(CloneMojiData)
                .ToList();
            _balloons = (balloons ?? throw new ArgumentNullException(nameof(balloons)))
                .Select(CloneBalloonData)
                .ToList();
            _attachedSymbols = (attachedSymbols ?? throw new ArgumentNullException(nameof(attachedSymbols)))
                .Select(CloneAttachedSymbolData)
                .ToList();
            var allObjects = _mojiDatas.Cast<IPageObjectData>().Concat(_balloons).Concat(_attachedSymbols).ToArray();
            if (allObjects.Length > 0 &&
                allObjects.Select(item => item.ZIndex).Distinct().Count() == allObjects.Length &&
                allObjects.All(item => item.ZIndex >= 0 && item.ZIndex < allObjects.Length))
            {
                _objectOrder.AddRange(allObjects.OrderBy(item => item.ZIndex).Select(item => item.ObjectId));
            }
            else
            {
                _objectOrder.AddRange(_mojiDatas.Select(mojiData => mojiData.ObjectId));
                _objectOrder.AddRange(_balloons.Select(balloon => balloon.ObjectId));
            }
            // Version 2.2 archives written before TASK-130 could contain more
            // than one balloon linked to the same text.  The constructor is
            // the format-boundary where that legacy state is repaired; all
            // subsequent mutations use the strict validator below.
            NormalizeObjectOrder(migrateDuplicateLinks: true);
        }

        public PageDocument(
            Guid pageId,
            string name,
            CanvasDocument canvas,
            IEnumerable<MojiData> mojiDatas)
            : this(
                pageId,
                name,
                (canvas ?? throw new ArgumentNullException(nameof(canvas))).LegacyData,
                mojiDatas)
        {
        }

        public PageDocument(
            Guid pageId,
            string name,
            CanvasDocument canvas,
            IEnumerable<MojiData> mojiDatas,
            IEnumerable<BalloonData> balloons)
            : this(
                pageId,
                name,
                (canvas ?? throw new ArgumentNullException(nameof(canvas))).LegacyData,
                mojiDatas,
                balloons)
        {
        }

        public Guid PageId { get; }

        /// <summary>
        /// ユーザーが入力したページ名。空白を含めて変更しません。
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// プロジェクト内の正規化済み順序。0始まりです。
        /// </summary>
        public int Order { get; internal set; }

        public CanvasDocument Canvas { get; }

        public CanvasDocument CanvasDocument => Canvas;

        /// <summary>
        /// 旧CanvasDataを必要とする既存adapter向けの互換アクセサです。
        /// </summary>
        public CanvasData CanvasData => Canvas.LegacyData;

        /// <summary>
        /// 現段階で扱う既存文字データ。将来のオブジェクトモデルへのadapter境界です。
        /// </summary>
        public IReadOnlyList<MojiData> MojiDatas => new ReadOnlyCollection<MojiData>(_mojiDatas);

        /// <summary>
        /// 後続の共通オブジェクトモデル向けの互換名です。
        /// </summary>
        public IReadOnlyList<MojiData> Objects => MojiDatas;

        public IReadOnlyList<BalloonData> Balloons => new ReadOnlyCollection<BalloonData>(_balloons);

        public IReadOnlyList<BalloonData> BalloonDatas => Balloons;

        public IReadOnlyList<AttachedSymbolData> AttachedSymbols => new ReadOnlyCollection<AttachedSymbolData>(_attachedSymbols);

        public IReadOnlyList<AttachedSymbolData> AttachedSymbolDatas => AttachedSymbols;

        /// <summary>
        /// All page objects in canonical page-level drawing order.
        /// </summary>
        public IReadOnlyList<IPageObjectData> AllObjects => new ReadOnlyCollection<IPageObjectData>(
            _objectOrder.Select(GetDocumentObject).ToList());

        public IReadOnlyList<IPageObjectData> DocumentObjects => AllObjects;

        public int ObjectCount => _objectOrder.Count;

        public void Rename(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public void AddMojiData(MojiData mojiData)
        {
            if (mojiData == null) throw new ArgumentNullException(nameof(mojiData));
            var clone = CloneMojiData(mojiData);
            ValidateObjectIds(_mojiDatas.Concat(new[] { clone }), _balloons, _attachedSymbols);
            _mojiDatas.Add(clone);
            _objectOrder.Add(clone.ObjectId);
            NormalizeObjectOrder();
        }

        public void SetMojiDatas(IEnumerable<MojiData> mojiDatas)
            => SetMojiDatas(mojiDatas, AttachedSymbolOrphanPolicy.ReanchorToNearest);

        /// <summary>
        /// Replaces the text collection through the same boundary used by the
        /// editor. Attached-symbol anchors are reconciled against the old and
        /// new grapheme sequences before any page collection is changed.
        /// </summary>
        public void SetMojiDatas(IEnumerable<MojiData> mojiDatas, AttachedSymbolOrphanPolicy orphanPolicy)
        {
            if (mojiDatas == null) throw new ArgumentNullException(nameof(mojiDatas));
            var replacement = mojiDatas.Select(CloneMojiData).ToList();
            var replacementBalloons = _balloons.Select(CloneBalloonData).ToList();
            var replacementSymbols = _attachedSymbols.Select(CloneAttachedSymbolData).ToList();
            var replacementTextIds = replacement.Select(item => item.ObjectId).ToHashSet();
            foreach (var balloon in replacementBalloons.Where(item => item.TextLink != null && !replacementTextIds.Contains(item.TextLink.TextObjectId)))
                balloon.TextLink = null;
            ValidateObjectIds(replacement, replacementBalloons, replacementSymbols);

            foreach (var previous in _mojiDatas)
            {
                var current = replacement.SingleOrDefault(item => item.ObjectId == previous.ObjectId);
                if (current != null)
                {
                    var changes = PrepareAnchorChanges(previous.ObjectId, previous.FullText, current.FullText,
                        orphanPolicy, replacementSymbols);
                    ApplyAnchorChanges(replacementSymbols, changes, current.FullText, previous.ObjectId);
                }
                else
                {
                    var orphaned = replacementSymbols.Where(symbol => symbol.ParentId == previous.ObjectId).ToArray();
                    if (orphaned.Length > 0 && orphanPolicy == AttachedSymbolOrphanPolicy.Reject)
                    {
                        throw new InvalidDataException("Removing a text object would orphan an attached symbol.");
                    }
                    if (orphanPolicy == AttachedSymbolOrphanPolicy.Remove)
                    {
                        replacementSymbols.RemoveAll(symbol => symbol.ParentId == previous.ObjectId);
                    }
                    else
                    {
                        foreach (var symbol in orphaned) DetachSymbol(symbol);
                    }
                }
            }

            ValidateObjectIds(replacement, replacementBalloons, replacementSymbols);
            ValidateAttachedSymbolState(replacement, replacementSymbols);
            ValidateBalloonTextLinks(replacement, replacementBalloons);
            _mojiDatas.Clear();
            _mojiDatas.AddRange(replacement);
            _balloons.Clear();
            _balloons.AddRange(replacementBalloons);
            _attachedSymbols.Clear();
            _attachedSymbols.AddRange(replacementSymbols);
            RebuildObjectOrderPreservingExisting();
            NormalizeObjectOrder();
        }

        public void UpdateMojiData(Guid objectId, Action<MojiData> update,
            AttachedSymbolOrphanPolicy orphanPolicy = AttachedSymbolOrphanPolicy.ReanchorToNearest)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            var index = _mojiDatas.FindIndex(item => item.ObjectId == objectId);
            if (index < 0) throw new KeyNotFoundException($"Text object was not found: {objectId}");
            var candidate = CloneMojiData(_mojiDatas[index]);
            var oldText = candidate.FullText;
            update(candidate);
            if (candidate.ObjectId != objectId) throw new InvalidOperationException("A text object ID cannot be changed.");
            var candidateSymbols = _attachedSymbols.Select(CloneAttachedSymbolData).ToList();
            var symbolChanges = PrepareAnchorChanges(objectId, oldText, candidate.FullText, orphanPolicy, candidateSymbols);
            ApplyAnchorChanges(candidateSymbols, symbolChanges, candidate.FullText, objectId);
            var candidateTexts = _mojiDatas.Select(item => item.ObjectId == objectId ? candidate : item).ToList();
            ValidateObjectIds(candidateTexts, _balloons, candidateSymbols);
            ValidateAttachedSymbolState(candidateTexts, candidateSymbols);
            _mojiDatas[index] = candidate;
            _attachedSymbols.Clear();
            _attachedSymbols.AddRange(candidateSymbols);
            NormalizeObjectOrder();
        }

        public void AddBalloon(BalloonData balloon)
        {
            if (balloon == null) throw new ArgumentNullException(nameof(balloon));
            var clone = CloneBalloonData(balloon);
            clone.Validate();
            ValidateObjectIds(_mojiDatas, _balloons.Concat(new[] { clone }), _attachedSymbols);
            ValidateBalloonTextLinks(_mojiDatas, _balloons.Concat(new[] { clone }));
            _balloons.Add(clone);
            _objectOrder.Add(clone.ObjectId);
            NormalizeObjectOrder();
        }

        public void AddBalloonData(BalloonData balloon) => AddBalloon(balloon);

        public void SetBalloons(IEnumerable<BalloonData> balloons)
        {
            if (balloons == null) throw new ArgumentNullException(nameof(balloons));
            var replacement = balloons.Select(CloneBalloonData).ToList();
            foreach (var balloon in replacement) balloon.Validate();
            ValidateObjectIds(_mojiDatas, replacement, _attachedSymbols);
            ValidateBalloonTextLinks(_mojiDatas, replacement);
            _balloons.Clear();
            _balloons.AddRange(replacement);
            RebuildObjectOrderPreservingExisting();
            NormalizeObjectOrder();
        }

        public void AddAttachedSymbol(AttachedSymbolData symbol)
        {
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            var clone = CloneAttachedSymbolData(symbol);
            ValidateAttachedSymbolParent(clone);
            clone.Validate();
            PopulateAnchorText(clone);
            ValidateObjectIds(_mojiDatas, _balloons, _attachedSymbols.Concat(new[] { clone }));
            _attachedSymbols.Add(clone);
            _objectOrder.Add(clone.ObjectId);
            NormalizeObjectOrder();
        }

        public void SetAttachedSymbols(IEnumerable<AttachedSymbolData> symbols)
        {
            if (symbols == null) throw new ArgumentNullException(nameof(symbols));
            var replacement = symbols.Select(CloneAttachedSymbolData).ToList();
            foreach (var symbol in replacement)
            {
                ValidateAttachedSymbolParent(symbol);
                symbol.Validate();
                PopulateAnchorText(symbol);
            }
            ValidateObjectIds(_mojiDatas, _balloons, replacement);
            ValidateAttachedSymbolState(_mojiDatas, replacement);
            _attachedSymbols.Clear();
            _attachedSymbols.AddRange(replacement);
            RebuildObjectOrderPreservingExisting();
            NormalizeObjectOrder();
        }

        public AttachedSymbolData GetAttachedSymbol(Guid symbolId)
        {
            return _attachedSymbols.SingleOrDefault(symbol => symbol.ObjectId == symbolId)
                ?? throw new KeyNotFoundException($"Attached symbol was not found: {symbolId}");
        }

        public bool ContainsAttachedSymbol(Guid symbolId) => _attachedSymbols.Any(symbol => symbol.ObjectId == symbolId);

        public void UpdateAttachedSymbol(Guid symbolId, Action<AttachedSymbolData> update)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            var index = _attachedSymbols.FindIndex(symbol => symbol.ObjectId == symbolId);
            if (index < 0) throw new KeyNotFoundException($"Attached symbol was not found: {symbolId}");
            var candidate = _attachedSymbols[index].Clone();
            update(candidate);
            ReplaceAttachedSymbol(symbolId, candidate);
        }

        internal void ReplaceAttachedSymbol(Guid symbolId, AttachedSymbolData replacement)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            var index = _attachedSymbols.FindIndex(symbol => symbol.ObjectId == symbolId);
            if (index < 0) throw new KeyNotFoundException($"Attached symbol was not found: {symbolId}");
            var candidate = replacement.Clone();
            if (candidate.ObjectId != symbolId) throw new InvalidOperationException("An attached symbol ID cannot be changed.");
            ValidateAttachedSymbolParent(candidate);
            candidate.Validate();
            PopulateAnchorText(candidate);
            var candidateSymbols = _attachedSymbols.Select(item => item.ObjectId == symbolId ? candidate : item).ToList();
            ValidateObjectIds(_mojiDatas, _balloons, candidateSymbols);
            ValidateAttachedSymbolState(_mojiDatas, candidateSymbols);
            _attachedSymbols[index] = candidate;
            NormalizeObjectOrder();
        }

        public bool RemoveAttachedSymbol(Guid symbolId)
        {
            var symbol = _attachedSymbols.FirstOrDefault(item => item.ObjectId == symbolId);
            if (symbol == null) return false;
            _attachedSymbols.Remove(symbol);
            _objectOrder.Remove(symbolId);
            NormalizeObjectOrder();
            return true;
        }

        public bool RemoveMojiData(MojiData mojiData)
        {
            if (mojiData == null) throw new ArgumentNullException(nameof(mojiData));
            var matching = _mojiDatas.FirstOrDefault(candidate => ReferenceEquals(candidate, mojiData) ||
                (mojiData.ObjectId != Guid.Empty && candidate.ObjectId == mojiData.ObjectId));
            if (matching == null || matching.IsLocked) return false;
            if (_balloons.Any(balloon => balloon.TextLink?.TextObjectId == matching.ObjectId && balloon.IsLocked) ||
                _attachedSymbols.Any(symbol => symbol.ParentId == matching.ObjectId && !symbol.IsDetached && symbol.IsLocked))
                return false;

            _mojiDatas.Remove(matching);
            _objectOrder.Remove(matching.ObjectId);
            foreach (var balloon in _balloons.Where(balloon => balloon.TextLink?.TextObjectId == matching.ObjectId))
            {
                balloon.TextLink = null;
            }
            foreach (var symbol in _attachedSymbols.Where(symbol => symbol.ParentId == matching.ObjectId))
            {
                symbol.ParentId = null;
                symbol.IsDetached = true;
                symbol.AnchorText = null;
            }
            NormalizeObjectOrder();
            return true;
        }

        public bool RemoveBalloon(BalloonData balloon)
        {
            if (balloon == null) throw new ArgumentNullException(nameof(balloon));
            var matching = _balloons.FirstOrDefault(candidate => ReferenceEquals(candidate, balloon) || candidate.ObjectId == balloon.ObjectId);
            if (matching == null) return false;
            _balloons.Remove(matching);
            _objectOrder.Remove(matching.ObjectId);
            NormalizeObjectOrder();
            return true;
        }

        public bool RemoveBalloon(Guid balloonId)
        {
            return ContainsBalloon(balloonId) && RemoveBalloon(GetBalloon(balloonId));
        }

        public bool RemoveBalloonData(Guid balloonId) => RemoveBalloon(balloonId);

        public BalloonData GetBalloon(Guid objectId)
        {
            return _balloons.SingleOrDefault(balloon => balloon.ObjectId == objectId)
                ?? throw new KeyNotFoundException($"Balloon was not found: {objectId}");
        }

        public bool ContainsBalloon(Guid objectId) => _balloons.Any(balloon => balloon.ObjectId == objectId);

        /// <summary>
        /// Atomically updates a balloon model. The original remains unchanged when validation fails.
        /// </summary>
        public void UpdateBalloon(Guid objectId, Action<BalloonData> update)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            var index = _balloons.FindIndex(balloon => balloon.ObjectId == objectId);
            if (index < 0) throw new KeyNotFoundException($"Balloon was not found: {objectId}");
            var candidate = _balloons[index].Clone();
            update(candidate);
            ReplaceBalloon(objectId, candidate);
        }

        internal void ReplaceBalloon(Guid objectId, BalloonData replacement)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            var index = _balloons.FindIndex(balloon => balloon.ObjectId == objectId);
            if (index < 0) throw new KeyNotFoundException($"Balloon was not found: {objectId}");
            var candidate = replacement.Clone();
            if (candidate.ObjectId != objectId)
            {
                throw new InvalidOperationException("A balloon object ID cannot be changed.");
            }
            candidate.Validate();
            var candidateBalloons = _balloons.Select(item => item.ObjectId == objectId ? candidate : item).ToList();
            ValidateBalloonTextLinks(_mojiDatas, candidateBalloons);
            _balloons[index] = candidate;
            NormalizeObjectOrder();
        }

        public void SetBalloonTail(Guid balloonId, BalloonTailData? tail)
        {
            UpdateBalloon(balloonId, balloon => balloon.Tail = tail?.Clone());
        }

        public void LinkBalloonText(Guid balloonId, Guid textObjectId, TextLinkData? link = null)
        {
            if (!ContainsObject(textObjectId) || !_mojiDatas.Any(text => text.ObjectId == textObjectId))
            {
                throw new KeyNotFoundException($"Text object was not found: {textObjectId}");
            }
            if (_balloons.Any(balloon => balloon.ObjectId != balloonId && balloon.TextLink?.TextObjectId == textObjectId))
            {
                throw new InvalidOperationException("The text object is already linked to another balloon.");
            }

            UpdateBalloon(balloonId, balloon =>
            {
                var value = link?.Clone() ?? new TextLinkData();
                value.TextObjectId = textObjectId;
                balloon.TextLink = value;
            });
        }

        public void UnlinkBalloonText(Guid balloonId)
        {
            UpdateBalloon(balloonId, balloon => balloon.TextLink = null);
        }

        /// <summary>
        /// Moves the block containing any page object.  The block is resolved
        /// from typed composition relationships, so the caller does not need
        /// to know whether the selection is text, a balloon, or a symbol.
        /// </summary>
        public bool MoveObjectOrder(Guid objectId, ObjectOrderOperation operation)
        {
            GetDocumentObject(objectId);
            var blocks = BuildObjectOrderBlocks();
            var currentIndex = blocks.FindIndex(block => block.Contains(objectId));
            if (currentIndex < 0) throw new InvalidOperationException("The selected object was not found in object order.");

            // A composition must never bypass a locked member.  Validate the
            // complete block before changing _objectOrder so the operation is
            // atomic on rejection.
            if (blocks[currentIndex].Any(id => GetDocumentObject(id).IsLocked)) return false;

            var targetIndex = GetObjectOrderTargetIndex(currentIndex, blocks.Count, operation);
            if (targetIndex == currentIndex) return false;

            var selected = blocks[currentIndex];
            blocks.RemoveAt(currentIndex);
            blocks.Insert(targetIndex, selected);
            _objectOrder.Clear();
            _objectOrder.AddRange(blocks.SelectMany(block => block));
            NormalizeObjectOrder();
            return true;
        }

        /// <summary>
        /// Reports whether a visible, editable caller can perform the supplied
        /// operation without mutating the page.  UI availability uses this
        /// same canonical block and edge calculation as MoveObjectOrder.
        /// </summary>
        public bool CanMoveObjectOrder(Guid objectId, ObjectOrderOperation operation)
        {
            GetDocumentObject(objectId);
            var blocks = BuildObjectOrderBlocks();
            var currentIndex = blocks.FindIndex(block => block.Contains(objectId));
            if (currentIndex < 0 || blocks[currentIndex].Any(id => GetDocumentObject(id).IsLocked)) return false;
            return GetObjectOrderTargetIndex(currentIndex, blocks.Count, operation) != currentIndex;
        }

        /// <summary>
        /// Changes only the selected object's lock flag.  This intentionally
        /// does not propagate through a typed composition.
        /// </summary>
        public bool SetObjectLocked(Guid objectId, bool isLocked)
        {
            var target = GetDocumentObject(objectId);
            if (target.IsLocked == isLocked) return false;
            target.IsLocked = isLocked;
            return true;
        }

        public bool LockObject(Guid objectId) => SetObjectLocked(objectId, true);

        public bool UnlockObject(Guid objectId) => SetObjectLocked(objectId, false);

        /// <summary>
        /// Returns the canonical block containing the supplied object.
        /// </summary>
        public IReadOnlyList<Guid> GetObjectOrderBlock(Guid objectId)
        {
            GetDocumentObject(objectId);
            return BuildObjectOrderBlocks().Single(block => block.Contains(objectId));
        }

        /// <summary>
        /// Compatibility entry point retained for TASK-130 callers.
        /// </summary>
        public bool MoveBalloonComposition(Guid balloonId, BalloonCompositionOrder operation)
        {
            GetBalloon(balloonId);
            return MoveObjectOrder(balloonId, (ObjectOrderOperation)operation);
        }

        public IReadOnlyList<Guid> GetBalloonCompositionObjectIds(Guid balloonId)
        {
            var balloon = GetBalloon(balloonId);
            var ids = new List<Guid> { balloon.ObjectId };
            if (balloon.TextLink != null && _mojiDatas.Any(text => text.ObjectId == balloon.TextLink.TextObjectId))
            {
                var textId = balloon.TextLink.TextObjectId;
                ids.Add(textId);
                ids.AddRange(_objectOrder.Where(objectId => _attachedSymbols.Any(symbol =>
                    symbol.ObjectId == objectId && symbol.ParentId == textId && !symbol.IsDetached)));
            }
            return ids;
        }

        private IReadOnlyList<Guid> GetTextCompositionObjectIds(Guid textObjectId)
        {
            GetObject(textObjectId);
            return new[] { textObjectId }
                .Concat(_objectOrder.Where(objectId => _attachedSymbols.Any(symbol =>
                    symbol.ObjectId == objectId && symbol.ParentId == textObjectId && !symbol.IsDetached)))
                .ToArray();
        }

        private List<List<Guid>> BuildObjectOrderBlocks()
        {
            var blockByObject = new Dictionary<Guid, IReadOnlyList<Guid>>();
            foreach (var balloon in _balloons)
            {
                var composition = GetBalloonCompositionObjectIds(balloon.ObjectId);
                foreach (var id in composition) blockByObject[id] = composition;
            }

            foreach (var text in _mojiDatas.Where(text => !_balloons.Any(balloon =>
                balloon.TextLink?.TextObjectId == text.ObjectId)))
            {
                var composition = GetTextCompositionObjectIds(text.ObjectId);
                foreach (var id in composition) blockByObject[id] = composition;
            }

            var blocks = new List<List<Guid>>();
            var emitted = new HashSet<Guid>();
            foreach (var objectId in _objectOrder)
            {
                if (!emitted.Add(objectId)) continue;
                if (!blockByObject.TryGetValue(objectId, out var composition))
                {
                    blocks.Add(new List<Guid> { objectId });
                    continue;
                }

                var block = composition.Where(emitted.Add).ToList();
                block.Insert(0, objectId);
                blocks.Add(CanonicalizeComposition(block, composition));
            }
            return blocks;
        }

        private static int GetObjectOrderTargetIndex(int currentIndex, int blockCount, ObjectOrderOperation operation)
        {
            if (blockCount <= 0) return currentIndex;
            return operation switch
            {
                ObjectOrderOperation.BringToFront => blockCount - 1,
                ObjectOrderOperation.BringForward => Math.Min(blockCount - 1, currentIndex + 1),
                ObjectOrderOperation.SendBackward => Math.Max(0, currentIndex - 1),
                ObjectOrderOperation.SendToBack => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
        }

        /// <summary>
        /// Makes list order the canonical drawing order and repairs IDs from
        /// legacy XML that did not contain the new identity fields.
        /// </summary>
        public void NormalizeObjectOrder(bool migrateDuplicateLinks = false)
        {
            var objectIds = new HashSet<Guid>();
            foreach (var mojiData in _mojiDatas)
            {
                if (mojiData.ObjectId == Guid.Empty)
                {
                    mojiData.ObjectId = Guid.NewGuid();
                }

                if (!objectIds.Add(mojiData.ObjectId))
                {
                    throw new InvalidOperationException($"Duplicate object ID: {mojiData.ObjectId}");
                }

                if (string.IsNullOrWhiteSpace(mojiData.Type))
                {
                    mojiData.Type = DocumentObjectTypes.Text;
                }
            }

            foreach (var balloon in _balloons)
            {
                if (balloon.ObjectId == Guid.Empty) balloon.ObjectId = Guid.NewGuid();
                if (!objectIds.Add(balloon.ObjectId))
                {
                    throw new InvalidOperationException($"Duplicate object ID: {balloon.ObjectId}");
                }
                if (string.IsNullOrWhiteSpace(balloon.Type)) balloon.Type = DocumentObjectTypes.Balloon;
                balloon.Validate();
            }

            foreach (var symbol in _attachedSymbols)
            {
                if (symbol.ObjectId == Guid.Empty) symbol.ObjectId = Guid.NewGuid();
                if (!objectIds.Add(symbol.ObjectId))
                {
                    throw new InvalidOperationException($"Duplicate object ID: {symbol.ObjectId}");
                }
                if (string.IsNullOrWhiteSpace(symbol.Type)) symbol.Type = DocumentObjectTypes.AttachedSymbol;
                if (!symbol.IsDetached) ValidateAttachedSymbolParent(symbol);
                symbol.Validate();
            }

            ReconcileRelationships(objectIds);
            if (migrateDuplicateLinks)
            {
                MigrateDuplicateBalloonTextLinks();
            }
            ValidateBalloonTextLinks(_mojiDatas, _balloons);
            var knownIds = new HashSet<Guid>(_mojiDatas.Select(item => item.ObjectId)
                .Concat(_balloons.Select(item => item.ObjectId))
                .Concat(_attachedSymbols.Select(item => item.ObjectId)));
            _objectOrder.RemoveAll(id => !knownIds.Contains(id));
            foreach (var id in _mojiDatas.Select(item => item.ObjectId)
                .Concat(_balloons.Select(item => item.ObjectId))
                .Concat(_attachedSymbols.Select(item => item.ObjectId)))
            {
                if (!_objectOrder.Contains(id)) _objectOrder.Add(id);
            }
            CanonicalizeBalloonCompositions();
            for (var index = 0; index < _objectOrder.Count; index++)
            {
                GetDocumentObject(_objectOrder[index]).ZIndex = index;
            }
        }

        public bool ContainsObject(Guid objectId)
        {
            return _mojiDatas.Any(mojiData => mojiData.ObjectId == objectId) ||
                _balloons.Any(balloon => balloon.ObjectId == objectId) ||
                _attachedSymbols.Any(symbol => symbol.ObjectId == objectId);
        }

        public MojiData GetObject(Guid objectId)
        {
            return _mojiDatas.SingleOrDefault(mojiData => mojiData.ObjectId == objectId)
                ?? throw new KeyNotFoundException($"Object was not found: {objectId}");
        }

        public IPageObjectData GetDocumentObject(Guid objectId)
        {
            return _mojiDatas.FirstOrDefault(mojiData => mojiData.ObjectId == objectId)
                ?? (IPageObjectData?)_balloons.FirstOrDefault(balloon => balloon.ObjectId == objectId)
                ?? _attachedSymbols.FirstOrDefault(symbol => symbol.ObjectId == objectId)
                ?? throw new KeyNotFoundException($"Object was not found: {objectId}");
        }

        internal IReadOnlyList<MojiData> CreateObjectSnapshot()
        {
            return _mojiDatas.Select(CloneMojiData).ToList();
        }

        internal IReadOnlyList<IPageObjectData> CreateAllObjectSnapshot()
        {
            return AllObjects.Select(ClonePageObject).ToList();
        }

        /// <summary>
        /// ページを別IDで複製します。キャンバス・文字データはdeep copyされます。
        /// </summary>
        public PageDocument Clone(Guid? pageId = null, string? name = null, bool preserveObjectIds = false)
        {
            IEnumerable<MojiData> objects;
            IEnumerable<BalloonData> balloons;
            IEnumerable<AttachedSymbolData> attachedSymbols;
            if (preserveObjectIds)
            {
                objects = _mojiDatas;
                balloons = _balloons;
                attachedSymbols = _attachedSymbols;
            }
            else
            {
                (objects, balloons, attachedSymbols) = CloneObjectsWithRemappedRelationships();
            }
            return new PageDocument(
                pageId ?? Guid.NewGuid(),
                name ?? Name,
                Canvas.LegacyData,
                objects,
                balloons,
                attachedSymbols);
        }

        /// <summary>
        /// Restores this page in place from a same-identity snapshot.  The
        /// editor uses this only for rolling back a failed, pre-notification
        /// synchronization; keeping the page instance is important because
        /// ProjectSession and the active editor both reference it.
        /// </summary>
        internal void RestoreFrom(PageDocument source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.PageId != PageId) throw new InvalidOperationException("Page identity cannot be changed while restoring a snapshot.");
            var restored = source.Clone(PageId, preserveObjectIds: true);
            Name = restored.Name;
            Order = restored.Order;
            Canvas.CanvasWidth = restored.Canvas.CanvasWidth;
            Canvas.CanvasHeight = restored.Canvas.CanvasHeight;
            Canvas.ImageData1 = restored.Canvas.ImageData1.Clone();
            Canvas.ImageData2 = restored.Canvas.ImageData2.Clone();
            Canvas.Image2LocatePosition = restored.Canvas.Image2LocatePosition;
            Canvas.ImageMarginTop = restored.Canvas.ImageMarginTop;
            Canvas.ImageMarginLeft = restored.Canvas.ImageMarginLeft;
            Canvas.ImageMarginBottom = restored.Canvas.ImageMarginBottom;
            Canvas.ImageMarginRight = restored.Canvas.ImageMarginRight;
            Canvas.CanvasColor = restored.Canvas.CanvasColor;
            _mojiDatas.Clear();
            _mojiDatas.AddRange(restored._mojiDatas.Select(CloneMojiData));
            _balloons.Clear();
            _balloons.AddRange(restored._balloons.Select(CloneBalloonData));
            _attachedSymbols.Clear();
            _attachedSymbols.AddRange(restored._attachedSymbols.Select(CloneAttachedSymbolData));
            _objectOrder.Clear();
            _objectOrder.AddRange(restored._objectOrder);
        }

        internal static CanvasData CloneCanvas(CanvasData source)
        {
            var clone = source.Clone();
            // CanvasData.Copyは旧形式互換の既存実装で配置位置をコピーしないため、
            // document境界では明示的に補完する。
            clone.Image2LocatePosition = source.Image2LocatePosition;
            return clone;
        }

        internal static MojiData CloneMojiData(MojiData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var clone = new MojiData
            {
                Id = source.Id,
                ObjectId = source.ObjectId,
            };
            clone.Copy(source);
            return clone;
        }

        internal static BalloonData CloneBalloonData(BalloonData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Clone();
        }

        internal static AttachedSymbolData CloneAttachedSymbolData(AttachedSymbolData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Clone();
        }

        internal static IPageObjectData ClonePageObject(IPageObjectData source)
        {
            return source switch
            {
                MojiData mojiData => CloneMojiData(mojiData),
                BalloonData balloon => CloneBalloonData(balloon),
                AttachedSymbolData symbol => CloneAttachedSymbolData(symbol),
                _ => throw new InvalidOperationException($"Unsupported page object type: {source.GetType().Name}"),
            };
        }

        private (IEnumerable<MojiData> MojiDatas, IEnumerable<BalloonData> Balloons, IEnumerable<AttachedSymbolData> AttachedSymbols) CloneObjectsWithRemappedRelationships()
        {
            var clones = _mojiDatas.Select(mojiData => mojiData.CloneAsNewObject()).ToArray();
            var balloonClones = _balloons.Select(balloon => balloon.CloneAsNewObject()).ToArray();
            var symbolClones = _attachedSymbols.Select(symbol => symbol.CloneAsNewObject()).ToArray();
            var objectIds = _mojiDatas.Cast<IPageObjectData>().Concat(_balloons).Concat(_attachedSymbols)
                .Select((item, index) => new { item.ObjectId, CloneId = index < clones.Length ? clones[index].ObjectId : index < clones.Length + balloonClones.Length ? balloonClones[index - clones.Length].ObjectId : symbolClones[index - clones.Length - balloonClones.Length].ObjectId })
                .ToDictionary(item => item.ObjectId, item => item.CloneId);

            foreach (var clone in clones.Cast<IPageObjectData>().Concat(balloonClones).Concat(symbolClones))
            {
                if (clone.ParentId.HasValue && objectIds.TryGetValue(clone.ParentId.Value, out var parentId)) clone.ParentId = parentId;
                if (clone.GroupId.HasValue && objectIds.TryGetValue(clone.GroupId.Value, out var groupId)) clone.GroupId = groupId;
            }

            foreach (var clone in balloonClones)
            {
                if (clone.TextLink?.TextObjectId is Guid textId && objectIds.TryGetValue(textId, out var remappedTextId))
                {
                    clone.TextLink.TextObjectId = remappedTextId;
                }
            }

            return (clones, balloonClones, symbolClones);
        }

        private void RebuildObjectOrderPreservingExisting()
        {
            var currentIds = new HashSet<Guid>(_mojiDatas.Select(item => item.ObjectId)
                .Concat(_balloons.Select(item => item.ObjectId))
                .Concat(_attachedSymbols.Select(item => item.ObjectId)));
            var preserved = _objectOrder.Where(currentIds.Contains).Distinct().ToList();
            foreach (var id in _mojiDatas.Select(item => item.ObjectId)
                .Concat(_balloons.Select(item => item.ObjectId))
                .Concat(_attachedSymbols.Select(item => item.ObjectId)))
            {
                if (!preserved.Contains(id)) preserved.Add(id);
            }

            _objectOrder.Clear();
            _objectOrder.AddRange(preserved);
        }

        private static void ValidateObjectIds(
            IEnumerable<MojiData> mojiDatas,
            IEnumerable<BalloonData> balloons,
            IEnumerable<AttachedSymbolData> attachedSymbols)
        {
            var ids = new HashSet<Guid>();
            foreach (var item in mojiDatas.Cast<IPageObjectData>()
                .Concat(balloons)
                .Concat(attachedSymbols))
            {
                if (item.ObjectId == Guid.Empty)
                {
                    throw new InvalidOperationException("Object ID must not be empty.");
                }
                if (!ids.Add(item.ObjectId))
                {
                    throw new InvalidOperationException($"Duplicate object ID: {item.ObjectId}");
                }
            }
        }

        private static void ValidateBalloonTextLinks(IEnumerable<MojiData> mojiDatas, IEnumerable<BalloonData> balloons)
        {
            var textIds = mojiDatas.Select(item => item.ObjectId).ToHashSet();
            var linkedTextIds = new HashSet<Guid>();
            foreach (var balloon in balloons.Where(item => item.TextLink != null))
            {
                var textId = balloon.TextLink!.TextObjectId;
                if (!textIds.Contains(textId)) throw new InvalidDataException("Balloon text link target was not found on this page.");
                if (!linkedTextIds.Add(textId)) throw new InvalidDataException("A text object cannot be linked to more than one balloon.");
            }
        }

        private void MigrateDuplicateBalloonTextLinks()
        {
            var order = _objectOrder
                .Select((objectId, index) => (objectId, index))
                .ToDictionary(item => item.objectId, item => item.index);
            var linkedTextIds = new HashSet<Guid>();
            foreach (var balloon in _balloons
                .Where(item => item.TextLink != null)
                .OrderBy(item => order.TryGetValue(item.ObjectId, out var index) ? index : int.MaxValue))
            {
                var textId = balloon.TextLink!.TextObjectId;
                if (!linkedTextIds.Add(textId))
                {
                    // Keep the first link in canonical drawing order.  This
                    // is deterministic even when the archive's list order
                    // differs from its persisted ZIndex values.
                    balloon.TextLink = null;
                }
            }
        }

        private List<Guid> CanonicalizeComposition(IEnumerable<Guid> block, IReadOnlyList<Guid> composition)
        {
            var blockIds = block.ToHashSet();
            return composition.Where(blockIds.Contains).ToList();
        }

        private void CanonicalizeBalloonCompositions()
        {
            foreach (var balloon in _balloons.Where(item => item.TextLink != null))
            {
                var composition = GetBalloonCompositionObjectIds(balloon.ObjectId);
                var members = composition.ToHashSet();
                var balloonIndex = _objectOrder.IndexOf(balloon.ObjectId);
                if (balloonIndex < 0) continue;
                var insertionIndex = _objectOrder.Take(balloonIndex).Count(objectId => !members.Contains(objectId));
                _objectOrder.RemoveAll(members.Contains);
                _objectOrder.InsertRange(Math.Min(insertionIndex, _objectOrder.Count), composition);
            }
        }

        private void ReconcileRelationships(HashSet<Guid> objectIds)
        {
            foreach (var item in _mojiDatas.Cast<IPageObjectData>().Concat(_balloons).Concat(_attachedSymbols))
            {
                if (item.ParentId == item.ObjectId || (item.ParentId.HasValue && !objectIds.Contains(item.ParentId.Value))) item.ParentId = null;
                if (item.GroupId == item.ObjectId || (item.GroupId.HasValue && !objectIds.Contains(item.GroupId.Value))) item.GroupId = null;
            }

            foreach (var balloon in _balloons)
            {
                if (balloon.TextLink != null &&
                    (!_mojiDatas.Any(text => text.ObjectId == balloon.TextLink.TextObjectId) || balloon.TextLink.TextObjectId == balloon.ObjectId))
                {
                    balloon.TextLink = null;
                }
            }

            foreach (var symbol in _attachedSymbols)
            {
                if (symbol.IsDetached) continue;
                if (!symbol.ParentId.HasValue || !_mojiDatas.Any(text => text.ObjectId == symbol.ParentId.Value))
                {
                    symbol.ParentId = null;
                    symbol.IsDetached = true;
                    symbol.AnchorText = null;
                }
            }
        }

        private void EnsureNewObjectId(Guid objectId)
        {
            if (objectId == Guid.Empty || _mojiDatas.Any(item => item.ObjectId == objectId) ||
                _balloons.Any(item => item.ObjectId == objectId) || _attachedSymbols.Any(item => item.ObjectId == objectId))
            {
                throw new InvalidOperationException($"Duplicate or empty object ID: {objectId}");
            }
        }

        private void ValidateAttachedSymbolParent(AttachedSymbolData symbol)
        {
            if (symbol.IsDetached) return;
            if (!symbol.ParentId.HasValue || !_mojiDatas.Any(item => item.ObjectId == symbol.ParentId.Value))
            {
                throw new InvalidDataException("Attached symbol parent text object was not found.");
            }
            var parent = _mojiDatas.Single(item => item.ObjectId == symbol.ParentId.Value);
            if (symbol.GraphemeAnchor >= GraphemeService.Count(parent.FullText))
            {
                throw new InvalidDataException("Attached symbol grapheme anchor is outside the parent text.");
            }
        }

        private void PopulateAnchorText(AttachedSymbolData symbol)
        {
            if (symbol.IsDetached || !symbol.ParentId.HasValue) return;
            var parent = _mojiDatas.Single(item => item.ObjectId == symbol.ParentId.Value);
            symbol.AnchorText = GraphemeService.GetAt(parent.FullText, symbol.GraphemeAnchor).Text;
        }

        private IReadOnlyList<(Guid SymbolId, int NewAnchor, bool Detach, bool Remove)> PrepareAnchorChanges(
            Guid parentId,
            string oldText,
            string newText,
            AttachedSymbolOrphanPolicy policy,
            IEnumerable<AttachedSymbolData> symbols)
        {
            var oldClusters = GraphemeService.Segment(oldText);
            var newClusters = GraphemeService.Segment(newText);
            var changes = new List<(Guid, int, bool, bool)>();
            foreach (var symbol in symbols.Where(item => item.ParentId == parentId && !item.IsDetached))
            {
                var anchorText = symbol.AnchorText;
                if (string.IsNullOrEmpty(anchorText) && symbol.GraphemeAnchor < oldClusters.Count)
                {
                    anchorText = oldClusters[symbol.GraphemeAnchor].Text;
                }
                var newAnchor = symbol.GraphemeAnchor;
                var valid = newAnchor >= 0 && newAnchor < newClusters.Count;
                if (valid && anchorText == newClusters[newAnchor].Text) continue;
                if (policy == AttachedSymbolOrphanPolicy.Detach)
                {
                    changes.Add((symbol.ObjectId, 0, true, false));
                    continue;
                }
                if (policy == AttachedSymbolOrphanPolicy.Remove)
                {
                    changes.Add((symbol.ObjectId, -1, false, true));
                    continue;
                }
                if (policy == AttachedSymbolOrphanPolicy.Reject)
                {
                    throw new InvalidDataException("Editing text would invalidate an attached symbol anchor.");
                }

                // ReanchorToNearest is the only policy allowed to search. The
                // service returns -1 for zero or multiple candidates.
                newAnchor = string.IsNullOrEmpty(anchorText)
                    ? -1
                    : GraphemeService.FindNearest(newText, anchorText, newAnchor);
                changes.Add(newAnchor >= 0
                    ? (symbol.ObjectId, newAnchor, false, false)
                    : (symbol.ObjectId, 0, true, false));
            }
            return changes;
        }

        private static void ApplyAnchorChanges(
            List<AttachedSymbolData> symbols,
            IReadOnlyList<(Guid SymbolId, int NewAnchor, bool Detach, bool Remove)> changes,
            string newText,
            Guid parentId)
        {
            foreach (var change in changes)
            {
                var symbol = symbols.Single(item => item.ObjectId == change.SymbolId);
                if (change.Remove)
                {
                    symbols.Remove(symbol);
                    continue;
                }
                symbol.GraphemeAnchor = change.NewAnchor;
                if (change.Detach)
                {
                    DetachSymbol(symbol);
                }
                else
                {
                    symbol.ParentId = parentId;
                    symbol.AnchorText = GraphemeService.GetAt(newText, change.NewAnchor).Text;
                }
            }
        }

        private static void DetachSymbol(AttachedSymbolData symbol)
        {
            symbol.ParentId = null;
            symbol.IsDetached = true;
            symbol.AnchorText = null;
        }

        private static void ValidateAttachedSymbolState(
            IEnumerable<MojiData> mojiDatas,
            IEnumerable<AttachedSymbolData> symbols)
        {
            var textById = mojiDatas.ToDictionary(item => item.ObjectId);
            foreach (var symbol in symbols)
            {
                symbol.Validate();
                if (symbol.IsDetached) continue;
                if (!symbol.ParentId.HasValue || !textById.TryGetValue(symbol.ParentId.Value, out var parent))
                {
                    throw new InvalidDataException("Attached symbol parent text object was not found.");
                }
                if (symbol.GraphemeAnchor >= GraphemeService.Count(parent.FullText))
                {
                    throw new InvalidDataException("Attached symbol grapheme anchor is outside the parent text.");
                }
            }
        }
    }

    public enum ObjectOrderOperation
    {
        BringToFront,
        BringForward,
        SendBackward,
        SendToBack,
    }

    public enum BalloonCompositionOrder
    {
        BringToFront,
        BringForward,
        SendBackward,
        SendToBack,
    }
}
