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
        }

        public void SetMojiDatas(IEnumerable<MojiData> mojiDatas)
        {
            if (mojiDatas == null) throw new ArgumentNullException(nameof(mojiDatas));
            _mojiDatas.Clear();
            _mojiDatas.AddRange(mojiDatas.Select(CloneMojiData));
        }

        public bool RemoveMojiData(MojiData mojiData)
        {
            if (mojiData == null) throw new ArgumentNullException(nameof(mojiData));
            return _mojiDatas.Remove(mojiData);
        }

        /// <summary>
        /// ページを別IDで複製します。キャンバス・文字データはdeep copyされます。
        /// </summary>
        public PageDocument Clone(Guid? pageId = null, string? name = null)
        {
            return new PageDocument(
                pageId ?? Guid.NewGuid(),
                name ?? Name,
                Canvas.LegacyData,
                _mojiDatas);
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
            };
            clone.Copy(source);
            return clone;
        }
    }
}
