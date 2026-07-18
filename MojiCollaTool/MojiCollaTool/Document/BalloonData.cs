using System;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;

namespace MojiCollaTool
{
    public static partial class DocumentObjectTypes
    {
        public const string Balloon = "Balloon";
    }

    /// <summary>
    /// Model-only balloon shapes. Rendering is intentionally owned by TASK-120.
    /// </summary>
    public enum BalloonShapeKind
    {
        Ellipse = 0,
        Oval = Ellipse,
        RoundedRectangle = 1,
        RoundRect = RoundedRectangle,
        Rectangle = 2,
        Square = Rectangle,
        Monologue = 3,
        Thought = Monologue,
        Unknown = 99,
    }

    public enum BalloonTextLayoutMode
    {
        FitTextToBalloon = 0,
        TextToBalloon = FitTextToBalloon,
        FitBalloonToText = 1,
        BalloonToText = FitBalloonToText,
    }

    public enum BalloonTextAlignment
    {
        Start = 0,
        Left = Start,
        Center = 1,
        End = 2,
        Right = End,
    }

    [Serializable]
    public sealed class BalloonTailData
    {
        public BalloonTailData()
        {
        }

        public BalloonTailData(Guid tailId)
        {
            if (tailId == Guid.Empty) throw new ArgumentException("Tail ID must not be empty.", nameof(tailId));
            TailId = tailId;
        }

        public Guid TailId { get; set; } = Guid.NewGuid();

        [XmlIgnore]
        public Point Tip
        {
            get => new Point(TipX, TipY);
            set
            {
                TipX = value.X;
                TipY = value.Y;
            }
        }

        public double TipX { get; set; }

        public double TipY { get; set; }

        /// <summary>0 is the left/top root and 1 is the right/bottom root.</summary>
        public double RootParameter { get; set; } = 0.5;

        public double Width { get; set; } = 24;

        public BalloonTailData Clone()
        {
            return new BalloonTailData
            {
                TailId = TailId,
                TipX = TipX,
                TipY = TipY,
                RootParameter = RootParameter,
                Width = Width,
            };
        }

        public void Validate()
        {
            if (TailId == Guid.Empty) throw new InvalidDataException("Balloon tail ID must not be empty.");
            RequireFinite(TipX, nameof(TipX));
            RequireFinite(TipY, nameof(TipY));
            RequireFinite(RootParameter, nameof(RootParameter));
            RequireFinite(Width, nameof(Width));
            if (RootParameter < 0 || RootParameter > 1) throw new InvalidDataException("Balloon tail root parameter is outside 0..1.");
            if (Width < 0) throw new InvalidDataException("Balloon tail width must not be negative.");
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidDataException($"Balloon tail {name} must be finite.");
        }
    }

    [Serializable]
    public sealed class TextLinkData
    {
        public Guid TextObjectId { get; set; }

        [XmlIgnore]
        public BalloonTextLayoutMode LayoutMode { get; set; }

        [XmlElement("LayoutMode")]
        public string LayoutModeValue
        {
            get => LayoutMode.ToString();
            set
            {
                LayoutMode = Enum.TryParse(value, ignoreCase: true, out BalloonTextLayoutMode parsed)
                    ? parsed
                    : BalloonTextLayoutMode.FitTextToBalloon;
            }
        }

        public double Padding { get; set; } = 8;

        public double MinimumFontSize { get; set; } = 8;

        [XmlIgnore]
        public BalloonTextAlignment Alignment { get; set; } = BalloonTextAlignment.Center;

        [XmlElement("Alignment")]
        public string AlignmentValue
        {
            get => Alignment.ToString();
            set
            {
                Alignment = Enum.TryParse(value, ignoreCase: true, out BalloonTextAlignment parsed)
                    ? parsed
                    : BalloonTextAlignment.Center;
            }
        }

        public TextLinkData Clone()
        {
            return new TextLinkData
            {
                TextObjectId = TextObjectId,
                LayoutMode = LayoutMode,
                Padding = Padding,
                MinimumFontSize = MinimumFontSize,
                Alignment = Alignment,
            };
        }

        public void Validate()
        {
            if (TextObjectId == Guid.Empty) throw new InvalidDataException("Balloon text link ID must not be empty.");
            RequireFinite(Padding, nameof(Padding));
            RequireFinite(MinimumFontSize, nameof(MinimumFontSize));
            if (Padding < 0) throw new InvalidDataException("Balloon text link padding must not be negative.");
            if (MinimumFontSize < 0) throw new InvalidDataException("Balloon minimum font size must not be negative.");
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidDataException($"Balloon text link {name} must be finite.");
        }
    }

    [Serializable]
    public sealed class BalloonData : IPageObjectData
    {
        public BalloonData()
        {
        }

        public Guid ObjectId { get; set; } = Guid.NewGuid();

        public string Type { get; set; } = DocumentObjectTypes.Balloon;

        public int ZIndex { get; set; }

        public bool IsLocked { get; set; }

        public bool IsVisible { get; set; } = true;

        public Guid? ParentId { get; set; }

        public Guid? GroupId { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Rotation { get; set; }

        public double RotateAngle
        {
            get => Rotation;
            set => Rotation = value;
        }

        [XmlIgnore]
        public Point Position
        {
            get => new Point(X, Y);
            set
            {
                X = value.X;
                Y = value.Y;
            }
        }

        public Rect Bounds { get; set; } = new Rect(0, 0, 240, 140);

        [XmlIgnore]
        public BalloonShapeKind ShapeKind { get; set; } = BalloonShapeKind.Ellipse;

        [XmlIgnore]
        public BalloonShapeKind Shape
        {
            get => ShapeKind;
            set => ShapeKind = value;
        }

        [XmlElement("ShapeKind")]
        public string ShapeKindValue
        {
            get => ShapeKind == BalloonShapeKind.Unknown ? (UnknownShapeKind ?? nameof(BalloonShapeKind.Ellipse)) : ShapeKind.ToString();
            set
            {
                if (Enum.TryParse(value, ignoreCase: true, out BalloonShapeKind parsed) && parsed != BalloonShapeKind.Unknown)
                {
                    ShapeKind = parsed;
                    UnknownShapeKind = null;
                }
                else
                {
                    ShapeKind = BalloonShapeKind.Unknown;
                    UnknownShapeKind = value;
                }
            }
        }

        [XmlIgnore]
        public string? UnknownShapeKind { get; private set; }

        public Color Fill { get; set; } = Colors.White;

        public Color Stroke { get; set; } = Colors.Black;

        public double StrokeThickness { get; set; } = 2;

        public BalloonTailData? Tail { get; set; }

        [XmlIgnore]
        public BalloonTailData? TailData
        {
            get => Tail;
            set => Tail = value;
        }

        [XmlIgnore]
        public BalloonTailData? BalloonTail
        {
            get => Tail;
            set => Tail = value;
        }

        public TextLinkData? TextLink { get; set; }

        [XmlIgnore]
        public TextLinkData? TextLinkData
        {
            get => TextLink;
            set => TextLink = value;
        }

        [XmlIgnore]
        public TextLinkData? Link
        {
            get => TextLink;
            set => TextLink = value;
        }

        public BalloonData Clone()
        {
            var clone = new BalloonData();
            clone.Copy(this);
            clone.ObjectId = ObjectId;
            return clone;
        }

        public BalloonData CloneAsNewObject()
        {
            var clone = Clone();
            clone.ObjectId = Guid.NewGuid();
            if (clone.Tail != null) clone.Tail.TailId = Guid.NewGuid();
            return clone;
        }

        public void Copy(BalloonData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Type = string.IsNullOrWhiteSpace(source.Type) ? DocumentObjectTypes.Balloon : source.Type;
            ZIndex = source.ZIndex;
            IsLocked = source.IsLocked;
            IsVisible = source.IsVisible;
            ParentId = source.ParentId;
            GroupId = source.GroupId;
            X = source.X;
            Y = source.Y;
            Rotation = source.Rotation;
            Bounds = source.Bounds;
            ShapeKind = source.ShapeKind;
            UnknownShapeKind = source.UnknownShapeKind;
            Fill = source.Fill;
            Stroke = source.Stroke;
            StrokeThickness = source.StrokeThickness;
            Tail = source.Tail?.Clone();
            TextLink = source.TextLink?.Clone();
        }

        public void Validate()
        {
            if (ObjectId == Guid.Empty) throw new InvalidDataException("Balloon object ID must not be empty.");
            RequireFinite(X, nameof(X));
            RequireFinite(Y, nameof(Y));
            RequireFinite(Rotation, nameof(Rotation));
            RequireFinite(Bounds.X, "Bounds.X");
            RequireFinite(Bounds.Y, "Bounds.Y");
            RequireFinite(Bounds.Width, "Bounds.Width");
            RequireFinite(Bounds.Height, "Bounds.Height");
            RequireFinite(StrokeThickness, nameof(StrokeThickness));
            if (Bounds.Width < 0 || Bounds.Height < 0) throw new InvalidDataException("Balloon bounds must not be negative.");
            if (StrokeThickness < 0) throw new InvalidDataException("Balloon stroke thickness must not be negative.");
            Tail?.Validate();
            TextLink?.Validate();
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidDataException($"Balloon {name} must be finite.");
        }
    }

    public interface IPageObjectData
    {
        Guid ObjectId { get; set; }
        string Type { get; set; }
        int ZIndex { get; set; }
        bool IsLocked { get; set; }
        bool IsVisible { get; set; }
        Guid? ParentId { get; set; }
        Guid? GroupId { get; set; }
    }
}
