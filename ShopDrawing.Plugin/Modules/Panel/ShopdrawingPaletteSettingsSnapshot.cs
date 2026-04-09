using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Modules.Panel
{
    internal sealed record ShopdrawingPaletteSettingsSnapshot(
        string DefaultSpecKey,
        string DefaultApplication,
        string DefaultWallTopPanelTreatment,
        string DefaultWallStartPanelTreatment,
        string DefaultWallEndPanelTreatment,
        bool DefaultWallBottomEdgeEnabled,
        string DefaultOpeningType,
        int DefaultThickness,
        double DefaultPanelWidth,
        double DefaultTextHeightMm,
        string DefaultAnnotationScaleLabel,
        double DefaultJointGap,
        bool EnableOpeningCut,
        DetailType CurrentDetailType,
        LayoutDirection DefaultDirection,
        StartEdge DefaultStartEdge,
        double DefaultCeilingCableDropMm);
}
