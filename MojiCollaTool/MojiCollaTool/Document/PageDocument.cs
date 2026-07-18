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

        public PageDocument(string name)
            : this(Guid.NewGuid(), name, new CanvasData(), Enumerable.Empty<MojiData>())
        {
        }

        public PageDocument(string name, IEnumerable<MojiData> mojiDatas)
            : this(Guid.NewGuid(), name, new CanvasData(), mojiDatas)
        {
        }

        public PageDocument(
            Guid pageId,
            string name,
            CanvasData canvas,
            IEnumerable<MojiData> mojiDatas)
        {
            if (pageId == Guid.Empty) throw new ArgumentException("Page ID must not be empty.", nameof(pageId));
            PageId = pageId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Canvas = new CanvasDocument(canvas ?? throw new ArgumentNullException(nameof(canvas)));
            _mojiDatas = (mojiDatas ?? throw new ArgumentNullException(nameof(mojiDatas)))
                .Select(CloneMojiData)
                .ToList();
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

        public void Rename(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public void AddMojiData(MojiData mojiData)
        {
            if (mojiData == null) throw new ArgumentNullException(nameof(mojiData));
            _mojiDatas.Add(CloneMojiData(mojiData));
            NormalizeObjectOrder();
        }

        public void SetMojiDatas(IEnumerable<MojiData> mojiDatas)
        {
            if (mojiDatas == null) throw new ArgumentNullException(nameof(mojiDatas));
            _mojiDatas.Clear();
            _mojiDatas.AddRange(mojiDatas.Select(CloneMojiData));
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

            if (removed) NormalizeObjectOrder();
            return removed;
        }

        /// <summary>
        /// Makes list order the canonical drawing order and repairs IDs from
        /// legacy XML that did not contain the new identity fields.
        /// </summary>
        public void NormalizeObjectOrder()
        {
            var objectIds = new HashSet<Guid>();
            for (var index = 0; index < _mojiDatas.Count; index++)
            {
                var mojiData = _mojiDatas[index];
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

                mojiData.ZIndex = index;
            }
        }

        public bool ContainsObject(Guid objectId)
        {
            return _mojiDatas.Any(mojiData => mojiData.ObjectId == objectId);
        }

        public MojiData GetObject(Guid objectId)
        {
            return _mojiDatas.SingleOrDefault(mojiData => mojiData.ObjectId == objectId)
                ?? throw new KeyNotFoundException($"Object was not found: {objectId}");
        }

        internal IReadOnlyList<MojiData> CreateObjectSnapshot()
        {
            var snapshot = _mojiDatas.Select(CloneMojiData).ToList();
            for (var index = 0; index < snapshot.Count; index++)
            {
                snapshot[index].ZIndex = index;
            }

            return snapshot;
        }

        /// <summary>
        /// ページを別IDで複製します。キャンバス・文字データはdeep copyされます。
        /// </summary>
        public PageDocument Clone(Guid? pageId = null, string? name = null, bool preserveObjectIds = false)
        {
            var objects = preserveObjectIds
                ? _mojiDatas
                : CloneObjectsWithRemappedRelationships();
            return new PageDocument(
                pageId ?? Guid.NewGuid(),
                name ?? Name,
                Canvas.LegacyData,
                objects);
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

        private IEnumerable<MojiData> CloneObjectsWithRemappedRelationships()
        {
            var clones = _mojiDatas.Select(mojiData => mojiData.CloneAsNewObject()).ToArray();
            var objectIds = _mojiDatas
                .Select((mojiData, index) => new { mojiData.ObjectId, CloneId = clones[index].ObjectId })
                .ToDictionary(item => item.ObjectId, item => item.CloneId);

            foreach (var clone in clones)
            {
                if (clone.ParentId.HasValue && objectIds.TryGetValue(clone.ParentId.Value, out var parentId))
                {
                    clone.ParentId = parentId;
                }

                if (clone.GroupId.HasValue && objectIds.TryGetValue(clone.GroupId.Value, out var groupId))
                {
                    clone.GroupId = groupId;
                }
            }

            return clones;
        }
    }
}
