using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MojiCollaTool
{
    /// <summary>
    /// Persisted object kinds. More kinds can be added without changing the
    /// common object state carried by <see cref="MojiData"/>.
    /// </summary>
    public static partial class DocumentObjectTypes
    {
        public const string Text = "Text";
    }

    public enum TextDirection : int
    {
        Yokogaki = 0,
        Tategaki = 1,
    }

    /// <summary>
    /// 文字色（前面）or背景色
    /// </summary>
    public enum ForeBack
    {
        Fore,
        Back,
    }

    [Serializable]
    public class MojiData : IPageObjectData
    {
        /// <summary>
        /// Legacy page-local integer ID. New code should use <see cref="ObjectId"/>.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Stable project object identity. Legacy data receives a new value on import.
        /// </summary>
        public Guid ObjectId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Discriminator for the shared object model.
        /// </summary>
        public string Type { get; set; } = DocumentObjectTypes.Text;

        /// <summary>
        /// Canonical page-local drawing order. PageDocument normalizes this to 0..N-1.
        /// </summary>
        public int ZIndex { get; set; }

        public bool IsLocked { get; set; }

        public bool IsVisible { get; set; } = true;

        public Guid? ParentId { get; set; }

        public Guid? GroupId { get; set; }

        public string FullText { get; set; } = "サンプル";

        /// <summary>
        /// Grapheme-aware view of <see cref="FullText"/>. The returned ranges
        /// use UTF-16 offsets for compatibility with WPF text APIs.
        /// </summary>
        [System.Xml.Serialization.XmlIgnore]
        public IReadOnlyList<GraphemeCluster> Graphemes => GraphemeService.Segment(FullText);

        [System.Xml.Serialization.XmlIgnore]
        public int GraphemeCount => GraphemeService.Count(FullText);

        public GraphemeCluster GetGrapheme(int graphemeIndex) => GraphemeService.GetAt(FullText, graphemeIndex);

        public string ExampleText => new string(FullText.Replace(Environment.NewLine, string.Empty).Take(10).ToArray());

        public double X { get; set; }

        public double Y { get; set; }

        public int FontSize { get; set; } = 50;

        public string FontFamilyName { get; set; } = "ＭＳ ゴシック";

        public TextDirection TextDirection { get; set; } = TextDirection.Yokogaki;

        public bool IsBold { get; set; } = false;

        public bool IsItalic { get; set; } = false;

        public double CharacterMargin { get; set; } = 0;

        public double LineMargin { get; set; } = 0;

        public Color ForeColor { get; set; } = Colors.Black;

        public Color BorderColor { get; set; } = Colors.White;

        public bool IsBorderExists { get => BorderThickness > 0; }

        public double BorderThickness { get; set; } = 0;

        public double BorderBlurrRadius { get; set; } = 0;

        public Color SecondBorderColor { get; set; } = Colors.Black;

        public bool IsSecondBorderExists { get => SecondBorderThickness > 0; }

        public double SecondBorderThickness { get; set; } = 0;

        public double SecondBorderBlurrRadius { get; set; } = 0;

        public bool IsBackgroundBoxExists { get; set; } = true;

        public Color BackgroundBoxColor { get; set; } = Colors.White;

        public double BackgroundBoxPadding { get; set; } = 0;

        public double BackgroundBoxBorderThickness { get; set; } = 0;

        public Color BackgroundBoxBorderColor { get; set; } = Colors.Black;

        public double BackgroundBoxCornerRadius { get; set; } = 0;

        public bool IsRotateActive { get => RotateAngle != 0; }

        public double RotateAngle { get; set; } = 0;

        public MojiData()
        {
            //  何もしない
        }

        public MojiData(int id)
        {
            Id = id;
            FullText = $"サンプル{id}";
        }

        /// <summary>
        /// 改行ごとの文字列リストを返す
        /// </summary>
        /// <returns></returns>
        public string[] GetTextLines()
        {
            return FullText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
        }

        /// <summary>
        /// 文字装飾が同じかどうかを返す
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool IsDecorationSame(MojiData other)
        {
            return
                (FontSize == other.FontSize) &&
                (FontFamilyName == other.FontFamilyName) &&
                (TextDirection == other.TextDirection) &&
                (IsBold == other.IsBold) &&
                (IsItalic == other.IsItalic) &&
                (CharacterMargin == other.CharacterMargin) &&
                (LineMargin == other.LineMargin) &&
                (ForeColor == other.ForeColor) &&
                (BorderColor == other.BorderColor) &&
                (BorderThickness == other.BorderThickness) &&
                (BorderBlurrRadius == other.BorderBlurrRadius) &&
                (SecondBorderColor == other.SecondBorderColor) &&
                (SecondBorderThickness == other.SecondBorderThickness) &&
                (SecondBorderBlurrRadius == other.SecondBorderBlurrRadius) &&
                (IsBackgroundBoxExists == other.IsBackgroundBoxExists) &&
                (BackgroundBoxColor == other.BackgroundBoxColor) &&
                (BackgroundBoxPadding == other.BackgroundBoxPadding) &&
                (BackgroundBoxBorderThickness == other.BackgroundBoxBorderThickness) &&
                (BackgroundBoxBorderColor == other.BackgroundBoxBorderColor) &&
                (BackgroundBoxCornerRadius == other.BackgroundBoxCornerRadius);
        }

        public void Copy(MojiData source)
        {
            // Legacy integer ID is intentionally not copied by this method.
            FullText = source.FullText;
            Type = source.Type;
            ZIndex = source.ZIndex;
            IsLocked = source.IsLocked;
            IsVisible = source.IsVisible;
            ParentId = source.ParentId;
            GroupId = source.GroupId;
            X = source.X;
            Y = source.Y;
            FontSize = source.FontSize;
            FontFamilyName = source.FontFamilyName;
            TextDirection = source.TextDirection;
            IsBold = source.IsBold;
            IsItalic = source.IsItalic;
            CharacterMargin = source.CharacterMargin;
            LineMargin = source.LineMargin;
            ForeColor = source.ForeColor;
            BorderColor = source.BorderColor;
            BorderThickness = source.BorderThickness;
            BorderBlurrRadius = source.BorderBlurrRadius;
            SecondBorderColor = source.SecondBorderColor;
            SecondBorderThickness = source.SecondBorderThickness;
            SecondBorderBlurrRadius = source.SecondBorderBlurrRadius;
            IsBackgroundBoxExists = source.IsBackgroundBoxExists;
            BackgroundBoxColor = source.BackgroundBoxColor;
            BackgroundBoxPadding = source.BackgroundBoxPadding;
            BackgroundBoxBorderThickness = source.BackgroundBoxBorderThickness;
            BackgroundBoxBorderColor = source.BackgroundBoxBorderColor;
            BackgroundBoxCornerRadius = source.BackgroundBoxCornerRadius;
            RotateAngle = source.RotateAngle;
        }

        public MojiData Clone()
        {
            MojiData clone = new MojiData();
            clone.Id = Id;
            clone.ObjectId = ObjectId;
            clone.Copy(this);
            return clone;
        }

        /// <summary>
        /// Creates a deep copy that represents a newly inserted object.
        /// </summary>
        public MojiData CloneAsNewObject()
        {
            MojiData clone = Clone();
            clone.ObjectId = Guid.NewGuid();
            return clone;
        }

        /// <summary>
        /// 文字を複製する
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public MojiData Reproduct(int id)
        {
            var reproductionMojiData = new MojiData(id);

            reproductionMojiData.Copy(this);

            //  わかりやすくするために、座標をデフォルトに戻す
            reproductionMojiData.X = 0;
            reproductionMojiData.Y = 0;

            return reproductionMojiData;
        }

        /// <summary>
        /// 改行コードがLFだけの場合、CR+LFに復元する
        /// </summary>
        public void RestoreFullTextNewLine()
        {
            if(FullText.Contains("\r") == false)
            {
                FullText = FullText.Replace("\n", Environment.NewLine);
            }
        }
    }
}
