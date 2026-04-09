namespace ShopDrawing.Plugin.Modules.Accessories
{
    internal sealed record ShopdrawingPlanAccessorySnapshot(
        string Application,
        string SpecKey,
        double OutsideCornerHeightM,
        double InsideCornerHeightM,
        int OutsideCornerCount,
        int InsideCornerCount);
}
