using System;
using System.IO;
using System.Xml.Serialization;

namespace MojiCollaTool
{
    public static partial class DocumentObjectTypes
    {
        public const string AttachedSymbol = "AttachedSymbol";
        public const string Symbol = AttachedSymbol;
    }

    [Flags]
    public enum AttachedSymbolInheritance
    {
        None = 0,
        Font = 1,
        ForeColor = 2,
        Border = 4,
        Decoration = 8,
        All = Font | ForeColor | Border | Decoration,
    }

    public enum AttachedSymbolOrphanPolicy
    {
        ReanchorToNearest = 0,
        Detach = 1,
        Remove = 2,
        Reject = 3,
    }

    /// <summary>
    /// A small independent child object anchored to a parent text grapheme.
    /// Offsets are measured in the parent's em coordinate system.
    /// </summary>
    [Serializable]
    public sealed class AttachedSymbolData : IPageObjectData
    {
        public Guid ObjectId { get; set; } = Guid.NewGuid();
        [XmlIgnore]
        public Guid SymbolId { get => ObjectId; set => ObjectId = value; }
        public string Type { get; set; } = DocumentObjectTypes.AttachedSymbol;
        public int ZIndex { get; set; }
        public bool IsLocked { get; set; }
        public bool IsVisible { get; set; } = true;

        public Guid? ParentId { get; set; }
        [XmlIgnore]
        public Guid? ParentObjectId
        {
            get => ParentId;
            set => ParentId = value;
        }
        [XmlIgnore]
        public Guid? TextObjectId
        {
            get => ParentId;
            set => ParentId = value;
        }
        [XmlIgnore]
        public Guid? ParentTextObjectId
        {
            get => ParentId;
            set => ParentId = value;
        }
        public Guid? GroupId { get; set; }

        public string Text { get; set; } = string.Empty;
        [XmlIgnore]
        public string SymbolText
        {
            get => Text;
            set => Text = value;
        }
        public int GraphemeAnchor { get; set; }
        [XmlIgnore]
        public int GraphemeIndex
        {
            get => GraphemeAnchor;
            set => GraphemeAnchor = value;
        }
        [XmlIgnore]
        public int AnchorGraphemeIndex
        {
            get => GraphemeAnchor;
            set => GraphemeAnchor = value;
        }
        [XmlIgnore]
        public int Anchor
        {
            get => GraphemeAnchor;
            set => GraphemeAnchor = value;
        }

        public string? AnchorText { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        [XmlIgnore]
        public double XOffset
        {
            get => OffsetX;
            set => OffsetX = value;
        }
        [XmlIgnore]
        public double YOffset
        {
            get => OffsetY;
            set => OffsetY = value;
        }
        [XmlIgnore]
        public double EmOffsetX
        {
            get => OffsetX;
            set => OffsetX = value;
        }
        [XmlIgnore]
        public double EmOffsetY
        {
            get => OffsetY;
            set => OffsetY = value;
        }
        public double Scale { get; set; } = 1;
        public double Rotation { get; set; }
        [XmlIgnore]
        public double RotateAngle
        {
            get => Rotation;
            set => Rotation = value;
        }
        public AttachedSymbolInheritance Inherit { get; set; } = AttachedSymbolInheritance.All;
        [XmlIgnore]
        public AttachedSymbolInheritance Inheritance
        {
            get => Inherit;
            set => Inherit = value;
        }
        [XmlIgnore]
        public AttachedSymbolInheritance InheritFlags
        {
            get => Inherit;
            set => Inherit = value;
        }
        public bool IncludeInCharacterSpacing { get; set; }
        [XmlIgnore]
        public bool AffectsCharacterSpacing
        {
            get => IncludeInCharacterSpacing;
            set => IncludeInCharacterSpacing = value;
        }
        public bool IsDetached { get; set; }

        [XmlIgnore]
        public bool InheritFont
        {
            get => Inherit.HasFlag(AttachedSymbolInheritance.Font);
            set => Inherit = value ? Inherit | AttachedSymbolInheritance.Font : Inherit & ~AttachedSymbolInheritance.Font;
        }
        [XmlIgnore]
        public bool InheritColor
        {
            get => Inherit.HasFlag(AttachedSymbolInheritance.ForeColor);
            set => Inherit = value ? Inherit | AttachedSymbolInheritance.ForeColor : Inherit & ~AttachedSymbolInheritance.ForeColor;
        }
        [XmlIgnore]
        public bool InheritDecoration
        {
            get => Inherit.HasFlag(AttachedSymbolInheritance.Decoration);
            set => Inherit = value ? Inherit | AttachedSymbolInheritance.Decoration : Inherit & ~AttachedSymbolInheritance.Decoration;
        }

        // Optional explicit style values used when inheritance is disabled.
        public int FontSize { get; set; }
        public string? FontFamilyName { get; set; }
        /// <summary>
        /// Explicit ARGB color used when ForeColor inheritance is disabled.
        /// This keeps the document model independent from WPF.
        /// </summary>
        public uint ForeColorArgb { get; set; } = 0xFF000000;

        public AttachedSymbolData Clone()
        {
            return new AttachedSymbolData
            {
                ObjectId = ObjectId,
                Type = Type,
                ZIndex = ZIndex,
                IsLocked = IsLocked,
                IsVisible = IsVisible,
                ParentId = ParentId,
                GroupId = GroupId,
                Text = Text,
                GraphemeAnchor = GraphemeAnchor,
                AnchorText = AnchorText,
                OffsetX = OffsetX,
                OffsetY = OffsetY,
                Scale = Scale,
                Rotation = Rotation,
                Inherit = Inherit,
                IncludeInCharacterSpacing = IncludeInCharacterSpacing,
                IsDetached = IsDetached,
                FontSize = FontSize,
                FontFamilyName = FontFamilyName,
                ForeColorArgb = ForeColorArgb,
            };
        }

        public AttachedSymbolData CloneAsNewObject()
        {
            var clone = Clone();
            clone.ObjectId = Guid.NewGuid();
            return clone;
        }

        public void Copy(AttachedSymbolData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var id = ObjectId;
            var clone = source.Clone();
            ObjectId = id;
            Type = clone.Type;
            ZIndex = clone.ZIndex;
            IsLocked = clone.IsLocked;
            IsVisible = clone.IsVisible;
            ParentId = clone.ParentId;
            GroupId = clone.GroupId;
            Text = clone.Text;
            GraphemeAnchor = clone.GraphemeAnchor;
            AnchorText = clone.AnchorText;
            OffsetX = clone.OffsetX;
            OffsetY = clone.OffsetY;
            Scale = clone.Scale;
            Rotation = clone.Rotation;
            Inherit = clone.Inherit;
            IncludeInCharacterSpacing = clone.IncludeInCharacterSpacing;
            IsDetached = clone.IsDetached;
            FontSize = clone.FontSize;
            FontFamilyName = clone.FontFamilyName;
            ForeColorArgb = clone.ForeColorArgb;
        }

        public void Validate()
        {
            if (ObjectId == Guid.Empty) throw new InvalidDataException("Attached symbol ID must not be empty.");
            if (string.IsNullOrEmpty(Text)) throw new InvalidDataException("Attached symbol text must not be empty.");
            if (Text.Length > 256) throw new InvalidDataException("Attached symbol text is too long.");
            if (GraphemeAnchor < 0) throw new InvalidDataException("Attached symbol grapheme anchor must not be negative.");
            RequireFinite(OffsetX, nameof(OffsetX));
            RequireFinite(OffsetY, nameof(OffsetY));
            RequireFinite(Scale, nameof(Scale));
            RequireFinite(Rotation, nameof(Rotation));
            if (Scale <= 0) throw new InvalidDataException("Attached symbol scale must be positive.");
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidDataException($"Attached symbol {name} must be finite.");
        }
    }
}
