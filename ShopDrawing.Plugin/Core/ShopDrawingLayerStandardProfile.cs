using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ShopDrawing.Plugin.Core
{
    internal static class ShopDrawingLayerStandardProfile
    {
        internal readonly record struct LayerStandard(short ColorAci, LineWeight LineWeight, bool IsPlottable);

        private static readonly IReadOnlyDictionary<string, LayerStandard> ExpectedLayers =
            new Dictionary<string, LayerStandard>(System.StringComparer.OrdinalIgnoreCase)
            {
                // BlockManager layer profile
                ["SD_PANEL"] = new LayerStandard(5, LineWeight.LineWeight030, true),
                ["SD_HATCH"] = new LayerStandard(253, LineWeight.LineWeight013, false),
                ["SD_JOINT_MALE"] = new LayerStandard(1, LineWeight.LineWeight025, true),
                ["SD_JOINT_FEMALE"] = new LayerStandard(4, LineWeight.LineWeight025, true),
                ["SD_TAG"] = new LayerStandard(3, LineWeight.ByLineWeightDefault, false),
                ["SD_OPENING"] = new LayerStandard(30, LineWeight.LineWeight050, true),
                ["SD_DETAIL"] = new LayerStandard(200, LineWeight.LineWeight018, true),
                ["SD_DIM"] = new LayerStandard(4, LineWeight.LineWeight013, true),
                ["SD_CEILING_T"] = new LayerStandard(150, LineWeight.LineWeight018, true),
                ["SD_CEILING_MUSHROOM"] = new LayerStandard(2, LineWeight.LineWeight018, true),
                ["SD_CEILING_BOLT"] = new LayerStandard(6, LineWeight.LineWeight025, true),
                ["SD_CEILING_T_HANGER"] = new LayerStandard(5, LineWeight.LineWeight025, true),
                ["SD_CEILING_MUSHROOM_HANGER"] = new LayerStandard(30, LineWeight.LineWeight025, true),
                ["SD_WALL_OUTSIDE_CORNER"] = new LayerStandard(1, LineWeight.LineWeight025, true),
                ["SD_WALL_INSIDE_CORNER"] = new LayerStandard(3, LineWeight.LineWeight025, true),

                // LayoutManager title/profile layer set
                [LayoutManagerEngine.TITLE_LAYER] = new LayerStandard(7, LineWeight.LineWeight050, true),
                [LayoutManagerEngine.TITLE_FRAME_LAYER] = new LayerStandard(7, LineWeight.LineWeight035, true),
                [LayoutManagerEngine.TITLE_TEXT_LAYER] = new LayerStandard(7, LineWeight.LineWeight018, true),
                [LayoutManagerEngine.VIEWPORT_LAYER] = new LayerStandard(3, LineWeight.LineWeight018, false),
                [LayoutManagerEngine.SHEET_FRAME_LAYER] = new LayerStandard(7, LineWeight.LineWeight018, true),
                [LayoutManagerEngine.DMBD_LAYER] = new LayerStandard(7, LineWeight.ByLayer, true),

                // SmartDim layer set
                [SmartDimEngine.DIM_LAYER_PANEL] = new LayerStandard(4, LineWeight.LineWeight018, true),
                [SmartDimEngine.DIM_LAYER_OPENING] = new LayerStandard(6, LineWeight.LineWeight018, true)
            };

        public static IReadOnlyDictionary<string, LayerStandard> GetExpectedLayers() => ExpectedLayers;
    }
}
