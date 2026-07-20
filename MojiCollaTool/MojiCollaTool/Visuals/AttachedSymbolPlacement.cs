using System;
using System.Windows;

namespace MojiCollaTool
{
    /// <summary>
    /// Small, direction-aware defaults for newly inserted symbols. Values are
    /// em units and remain editable through the symbol property controls.
    /// </summary>
    public static class AttachedSymbolPlacement
    {
        public static Point GetDefaultOffset(TextDirection direction, string symbolText)
        {
            if (string.IsNullOrEmpty(symbolText)) throw new ArgumentException("Symbol text is required.", nameof(symbolText));
            return direction == TextDirection.Tategaki
                ? new Point(-0.42, -0.32)
                : new Point(0.34, -0.42);
        }

        public static double DefaultScale => 0.6;
    }
}
