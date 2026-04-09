using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Modules.Accessories
{
    internal sealed record ShopdrawingAccessorySummaryRow(
        string CategoryScope,
        string Application,
        string SpecKey,
        string Name,
        string Material,
        string Position,
        string Unit,
        AccessoryCalcRule Rule,
        double BasisValue,
        double Factor,
        double Quantity,
        string Note);
}
