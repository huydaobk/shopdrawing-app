namespace ShopDrawing.Plugin.Modules.Accessories
{
    internal sealed record ShopdrawingAccessorySnapshot(
        string SpecKey,
        string Application,
        double CeilingTLineLengthM,
        double CeilingMushroomLineLengthM,
        int CeilingMushroomBoltCount,
        int CeilingTHangerPointCount,
        int CeilingMushroomHangerPointCount,
        double CeilingCableDropM);
}
