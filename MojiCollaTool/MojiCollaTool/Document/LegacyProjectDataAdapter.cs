using System;
using System.Collections.Generic;
using System.Linq;

namespace MojiCollaTool
{
    /// <summary>
    /// 現行単ページデータと新しい文書モデルの境界です。
    /// ここでは保存形式を変更せず、入出力時の参照共有だけを防ぎます。
    /// </summary>
    public static class LegacyProjectDataAdapter
    {
        public static ProjectDocument Import(
            string projectName,
            CanvasData canvas,
            IEnumerable<MojiData> mojiDatas)
        {
            if (projectName == null) throw new ArgumentNullException(nameof(projectName));
            var importedPage = ImportPage("01", canvas, mojiDatas);
            return new ProjectDocument(Guid.NewGuid(), projectName, new[] { importedPage });
        }

        public static ProjectDocument FromLegacy(
            string projectName,
            CanvasData canvas,
            IEnumerable<MojiData> mojiDatas)
        {
            return Import(projectName, canvas, mojiDatas);
        }

        public static PageDocument ImportPage(
            string pageName,
            CanvasData canvas,
            IEnumerable<MojiData> mojiDatas,
            Guid? pageId = null)
        {
            return new PageDocument(
                pageId ?? Guid.NewGuid(),
                pageName ?? throw new ArgumentNullException(nameof(pageName)),
                canvas ?? throw new ArgumentNullException(nameof(canvas)),
                mojiDatas ?? throw new ArgumentNullException(nameof(mojiDatas)));
        }

        public static LegacyPageData Export(PageDocument page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            return new LegacyPageData(page.Canvas.ToLegacyData(), page.MojiDatas);
        }

        public static LegacyPageData ToLegacy(PageDocument page)
        {
            return Export(page);
        }
    }

    /// <summary>
    /// 旧形式writerへ渡すためのdeep-copy済み単ページ値です。
    /// </summary>
    public sealed class LegacyPageData
    {
        public LegacyPageData(CanvasData canvas, IEnumerable<MojiData> mojiDatas)
        {
            Canvas = PageDocument.CloneCanvas(canvas ?? throw new ArgumentNullException(nameof(canvas)));
            MojiDatas = mojiDatas
                .Select(PageDocument.CloneMojiData)
                .ToArray();
        }

        public CanvasData Canvas { get; }

        public IReadOnlyList<MojiData> MojiDatas { get; }
    }
}
