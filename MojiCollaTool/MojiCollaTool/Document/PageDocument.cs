using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            var allObjects = _mojiDatas.Cast<IPageObjectData>().Concat(_balloons).ToArray();
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
            NormalizeObjectOrder();
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
            EnsureNewObjectId(clone.ObjectId);
            _mojiDatas.Add(clone);
            _objectOrder.Add(clone.ObjectId);
            NormalizeObjectOrder();
        }

        public void SetMojiDatas(IEnumerable<MojiData> mojiDatas)
        {
            if (mojiDatas == null) throw new ArgumentNullException(nameof(mojiDatas));
            _mojiDatas.Clear();
            _mojiDatas.AddRange(mojiDatas.Select(CloneMojiData));
            RebuildObjectOrder();
            NormalizeObjectOrder();
        }

        public void AddBalloon(BalloonData balloon)
        {
            if (balloon == null) throw new ArgumentNullException(nameof(balloon));
            var clone = CloneBalloonData(balloon);
            EnsureNewObjectId(clone.ObjectId);
            _balloons.Add(clone);
            _objectOrder.Add(clone.ObjectId);
            NormalizeObjectOrder();
        }

        public void AddBalloonData(BalloonData balloon) => AddBalloon(balloon);

        public void SetBalloons(IEnumerable<BalloonData> balloons)
        {
            if (balloons == null) throw new ArgumentNullException(nameof(balloons));
            _balloons.Clear();
            _balloons.AddRange(balloons.Select(CloneBalloonData));
            RebuildObjectOrder();
            NormalizeObjectOrder();
        }

        public bool RemoveMojiData(MojiData mojiData)
        {
            if (mojiData == null) throw new ArgumentNullException(nameof(mojiData));
            var removed = _mojiDatas.Remove(mojiData);
            if (!removed && mojiData.ObjectId != Guid.Empty)
            {
                var matching = _mojiDatas.FirstOrDefault(candidate => candidate.ObjectId == mojiData.ObjectId);
                if (matching != null) removed = _mojiDatas.Remove(matching);
            }

            if (removed)
            {
                _objectOrder.Remove(mojiData.ObjectId);
                foreach (var balloon in _balloons.Where(balloon => balloon.TextLink?.TextObjectId == mojiData.ObjectId))
                {
                    // Deleting text detaches the composition link but keeps the balloon.
                    balloon.TextLink = null;
                }
                NormalizeObjectOrder();
            }
            return removed;
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
            candidate.Validate();
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
        /// Makes list order the canonical drawing order and repairs IDs from
        /// legacy XML that did not contain the new identity fields.
        /// </summary>
        public void NormalizeObjectOrder()
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

            ReconcileRelationships(objectIds);
            var knownIds = new HashSet<Guid>(_mojiDatas.Select(item => item.ObjectId).Concat(_balloons.Select(item => item.ObjectId)));
            _objectOrder.RemoveAll(id => !knownIds.Contains(id));
            foreach (var id in _mojiDatas.Select(item => item.ObjectId).Concat(_balloons.Select(item => item.ObjectId)))
            {
                if (!_objectOrder.Contains(id)) _objectOrder.Add(id);
            }
            for (var index = 0; index < _objectOrder.Count; index++)
            {
                GetDocumentObject(_objectOrder[index]).ZIndex = index;
            }
        }

        public bool ContainsObject(Guid objectId)
        {
            return _mojiDatas.Any(mojiData => mojiData.ObjectId == objectId) ||
                _balloons.Any(balloon => balloon.ObjectId == objectId);
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
            if (preserveObjectIds)
            {
                objects = _mojiDatas;
                balloons = _balloons;
            }
            else
            {
                (objects, balloons) = CloneObjectsWithRemappedRelationships();
            }
            return new PageDocument(
                pageId ?? Guid.NewGuid(),
                name ?? Name,
                Canvas.LegacyData,
                objects,
                balloons);
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

        internal static IPageObjectData ClonePageObject(IPageObjectData source)
        {
            return source switch
            {
                MojiData mojiData => CloneMojiData(mojiData),
                BalloonData balloon => CloneBalloonData(balloon),
                _ => throw new InvalidOperationException($"Unsupported page object type: {source.GetType().Name}"),
            };
        }

        private (IEnumerable<MojiData> MojiDatas, IEnumerable<BalloonData> Balloons) CloneObjectsWithRemappedRelationships()
        {
            var clones = _mojiDatas.Select(mojiData => mojiData.CloneAsNewObject()).ToArray();
            var balloonClones = _balloons.Select(balloon => balloon.CloneAsNewObject()).ToArray();
            var objectIds = _mojiDatas.Cast<IPageObjectData>().Concat(_balloons)
                .Select((item, index) => new { item.ObjectId, CloneId = index < clones.Length ? clones[index].ObjectId : balloonClones[index - clones.Length].ObjectId })
                .ToDictionary(item => item.ObjectId, item => item.CloneId);

            foreach (var clone in clones.Cast<IPageObjectData>().Concat(balloonClones))
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

            return (clones, balloonClones);
        }

        private void RebuildObjectOrder()
        {
            _objectOrder.Clear();
            _objectOrder.AddRange(_mojiDatas.Select(item => item.ObjectId));
            _objectOrder.AddRange(_balloons.Select(item => item.ObjectId));
        }

        private void ReconcileRelationships(HashSet<Guid> objectIds)
        {
            foreach (var item in _mojiDatas.Cast<IPageObjectData>().Concat(_balloons))
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
        }

        private void EnsureNewObjectId(Guid objectId)
        {
            if (objectId == Guid.Empty || _mojiDatas.Any(item => item.ObjectId == objectId) || _balloons.Any(item => item.ObjectId == objectId))
            {
                throw new InvalidOperationException($"Duplicate or empty object ID: {objectId}");
            }
        }
    }
}
