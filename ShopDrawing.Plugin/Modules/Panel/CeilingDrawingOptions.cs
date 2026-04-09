using System.Collections.Generic;
using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Plugin.Modules.Panel
{
    internal sealed record CeilingDrawingOptions(
        LayoutDirection PanelDirection,
        StartEdge PanelStartEdge,
        LayoutDirection SuspensionDirection,
        bool DivideFromMaxSide,
        double TSpacingMm,
        double TClearGapMm,
        int MushroomDivisionCount,
        IReadOnlyList<double> BaySpansMm,
        IReadOnlyList<bool> BayHasMushroomFlags);
}
