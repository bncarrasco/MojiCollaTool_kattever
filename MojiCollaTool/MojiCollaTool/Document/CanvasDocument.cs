using System;
using System.Windows;
using System.Windows.Media;

namespace MojiCollaTool
{
    /// <summary>
    /// 既存CanvasDataを文書モデルから隔離する薄いadapterです。
    /// 保存形式は変更せず、後続taskでCanvasDataを置換できる境界を提供します。
    /// </summary>
    public sealed class CanvasDocument
    {
        private readonly CanvasData _data;

        public CanvasDocument()
            : this(new CanvasData())
        {
        }

        public CanvasDocument(CanvasData data)
        {
            _data = PageDocument.CloneCanvas(data ?? throw new ArgumentNullException(nameof(data)));
        }

        public int CanvasWidth { get => _data.CanvasWidth; set => _data.CanvasWidth = value; }

        public int CanvasHeight { get => _data.CanvasHeight; set => _data.CanvasHeight = value; }

        public ImageData ImageData1 { get => _data.ImageData1; set => _data.ImageData1 = value ?? throw new ArgumentNullException(nameof(value)); }

        public ImageData ImageData2 { get => _data.ImageData2; set => _data.ImageData2 = value ?? throw new ArgumentNullException(nameof(value)); }

        public LocatePosition Image2LocatePosition { get => _data.Image2LocatePosition; set => _data.Image2LocatePosition = value; }

        public int ImageMarginTop { get => _data.ImageMarginTop; set => _data.ImageMarginTop = value; }

        public int ImageMarginLeft { get => _data.ImageMarginLeft; set => _data.ImageMarginLeft = value; }

        public int ImageMarginBottom { get => _data.ImageMarginBottom; set => _data.ImageMarginBottom = value; }

        public int ImageMarginRight { get => _data.ImageMarginRight; set => _data.ImageMarginRight = value; }

        public Color CanvasColor { get => _data.CanvasColor; set => _data.CanvasColor = value; }

        public int ImageWidth => _data.ImageWidth;

        public int ImageHeight => _data.ImageHeight;

        public Thickness GetImage1Margin() => _data.GetImage1Margin();

        public Thickness GetImage2Margin() => _data.GetImage2Margin();

        public void UpdateCanvasSize() => _data.UpdateCanvasSize();

        public void ModifyImageSize() => _data.ModifyImageSize();

        /// <summary>
        /// 旧形式adapterへ渡すdeep copyを返します。
        /// </summary>
        public CanvasData ToLegacyData() => PageDocument.CloneCanvas(_data);

        /// <summary>
        /// 後続の移行処理用に、現在の旧データを参照する必要がある箇所だけで使用します。
        /// </summary>
        internal CanvasData LegacyData => _data;
    }
}
