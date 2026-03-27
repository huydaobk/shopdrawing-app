using System;
using System.Collections.Generic;
using System.Linq;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    /// <summary>
    /// Tính bảng khối lượng sơ bộ cho giai đoạn đấu thầu.
    /// Bao gồm: tổng hợp panel, cơ sở tính phụ kiện và bảng phụ kiện chốt.
    /// </summary>
    public class TenderBomCalculator
    {
        public class PanelSummaryRow
        {
            public string Floor { get; set; } = "";
            public string SpecKey { get; set; } = "";
            public string Category { get; set; } = "";
            public int WallCount { get; set; }
            public double TotalLengthM { get; set; }
            public double HeightMm { get; set; }
            public double WallAreaM2 { get; set; }
            public double OpeningAreaM2 { get; set; }
            public double NetAreaM2 { get; set; }
            public int EstimatedPanels { get; set; }
            public double OrderedAreaM2 { get; set; }
            public double WasteAreaM2 { get; set; }
            public double WastePercent { get; set; }
        }

        public class AccessoryBasisRow
        {
            public string Category { get; set; } = "";
            public string Floor { get; set; } = "";
            public string WallName { get; set; } = "";
            public string Application { get; set; } = "";
            public string SpecKey { get; set; } = "";
            public string AccessoryName { get; set; } = "";
            public string Material { get; set; } = "";
            public string Position { get; set; } = "";
            public string Unit { get; set; } = "";
            public string RuleLabel { get; set; } = "";
            public string BasisLabel { get; set; } = "";
            public double BasisValue { get; set; }
            public double Factor { get; set; }
            public double AutoQuantity { get; set; }
            public string Note { get; set; } = "";
        }

        public class AccessorySummaryRow
        {
            public string CategoryScope { get; set; } = "";
            public string Application { get; set; } = "";
            public string SpecKey { get; set; } = "";
            public string Name { get; set; } = "";
            public string Material { get; set; } = "";
            public string Position { get; set; } = "";
            public string Unit { get; set; } = "";
            public string RuleLabel { get; set; } = "";
            public string BasisLabel { get; set; } = "";
            public double BasisValue { get; set; }
            public double Factor { get; set; }
            public double WastePercent { get; set; }
            public double AutoQuantity { get; set; }
            public double Adjustment { get; set; }
            public double FinalQuantity { get; set; }
            public string Note { get; set; } = "";
        }

        public class AccessoryReport
        {
            public List<AccessoryBasisRow> BasisRows { get; } = new();
            public List<AccessorySummaryRow> SummaryRows { get; } = new();
        }

        private sealed record ColdStorageSuspensionSpec(double TSpacingMm, double MushroomSpacingMm);

        private static readonly SortedDictionary<int, ColdStorageSuspensionSpec> ColdStorageSuspensionMap = new()
        {
            { 50,  new ColdStorageSuspensionSpec(3000, 0) },
            { 75,  new ColdStorageSuspensionSpec(6000, 3000) },
            { 100, new ColdStorageSuspensionSpec(7000, 3500) },
            { 125, new ColdStorageSuspensionSpec(8000, 4000) },
            { 150, new ColdStorageSuspensionSpec(9000, 4500) },
            { 180, new ColdStorageSuspensionSpec(10000, 5000) },
            { 200, new ColdStorageSuspensionSpec(11000, 5500) }
        };

        private const double SuspensionPointSpacingMm = 1450.0;

        public static double ColdStorageSuspensionPointSpacingMm => SuspensionPointSpacingMm;

        public List<PanelSummaryRow> CalculatePanelSummary(List<TenderWall> walls)
        {
            return walls
                .GroupBy(w => new { w.Floor, w.SpecKey, w.Category })
                .Select(g =>
                {
                    double netArea = g.Sum(w => w.NetAreaM2);
                    double orderedArea = g.Sum(w => w.OrderedAreaM2);
                    double wasteArea = Math.Max(0, orderedArea - netArea);
                    double wastePct = orderedArea > 0 ? wasteArea / orderedArea * 100.0 : 0;

                    return new PanelSummaryRow
                    {
                        Floor = g.Key.Floor,
                        SpecKey = g.Key.SpecKey,
                        Category = g.Key.Category,
                        WallCount = g.Count(),
                        TotalLengthM = g.Sum(w => w.Length) / 1000.0,
                        HeightMm = g.Average(w => w.Height),
                        WallAreaM2 = g.Sum(w => w.WallAreaM2),
                        OpeningAreaM2 = g.Sum(w => w.OpeningAreaM2),
                        NetAreaM2 = netArea,
                        EstimatedPanels = g.Sum(w => w.EstimatedPanelCount),
                        OrderedAreaM2 = orderedArea,
                        WasteAreaM2 = wasteArea,
                        WastePercent = wastePct
                    };
                })
                .OrderBy(r => r.Floor)
                .ThenBy(r => r.Category)
                .ThenBy(r => r.SpecKey)
                .ToList();
        }

        public AccessoryReport CalculateAccessoryReport(
            List<TenderWall> walls, List<TenderAccessory> accessories)
        {
            var report = new AccessoryReport();

            foreach (var accessory in accessories)
            {
                double totalBasis = 0;
                double totalAuto = 0;

                foreach (var wall in walls.Where(w => IsAccessoryApplicable(accessory, w)))
                {
                    double basisValue = GetBasisValue(accessory.CalcRule, wall);
                    double autoQuantity = accessory.IsManualOnly
                        ? 0
                        : Round2(basisValue * accessory.Factor);

                    if (basisValue <= 0 && autoQuantity <= 0)
                        continue;

                    totalBasis += basisValue;
                    totalAuto += autoQuantity;

                    // Auto-size tên vít TEK chỉ cho Vách + Ngoài nhà
                    string accessoryDisplayName = accessory.Name;
                    if (accessory.Name == AccessoryDataManager.ExteriorTekScrewBaseName
                        && accessory.CalcRule == AccessoryCalcRule.PER_TEK_SCREW_QTY
                        && string.Equals(wall.Application, "Ngoài nhà", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase)
                        && wall.PanelThickness > 0)
                    {
                        accessoryDisplayName = AccessoryDataManager.GetAutoSizedScrewName(
                            accessory.Name, wall.PanelThickness);
                    }

                    report.BasisRows.Add(new AccessoryBasisRow
                    {
                        Category = wall.Category,
                        Floor = wall.Floor,
                        WallName = wall.Name,
                        Application = wall.Application,
                        SpecKey = wall.SpecKey,
                        AccessoryName = accessoryDisplayName,
                        Material = accessory.Material,
                        Position = accessory.Position,
                        Unit = accessory.Unit,
                        RuleLabel = TenderAccessoryRules.GetRuleLabel(accessory.CalcRule),
                        BasisLabel = TenderAccessoryRules.GetBasisLabel(accessory.CalcRule),
                        BasisValue = Round2(basisValue),
                        Factor = accessory.Factor,
                        AutoQuantity = autoQuantity,
                        Note = accessory.Note
                    });
                }

                double finalQuantity = CalculateFinalQuantity(accessory, totalAuto);
                if (totalAuto <= 0 && Math.Abs(finalQuantity) <= 0)
                    continue;

                // Auto-size tên vít TEK cho Summary nếu tất cả vách cùng chiều dày
                string summaryName = accessory.Name;
                if (accessory.Name == AccessoryDataManager.ExteriorTekScrewBaseName
                    && accessory.CalcRule == AccessoryCalcRule.PER_TEK_SCREW_QTY)
                {
                    var thicknesses = walls
                        .Where(w => IsAccessoryApplicable(accessory, w)
                            && string.Equals(w.Application, "Ngoài nhà", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(w.Category, "Vách", StringComparison.OrdinalIgnoreCase)
                            && w.PanelThickness > 0)
                        .Select(w => w.PanelThickness)
                        .Distinct()
                        .ToList();

                    if (thicknesses.Count == 1)
                    {
                        summaryName = AccessoryDataManager.GetAutoSizedScrewName(
                            accessory.Name, thicknesses[0]);
                    }
                }

                report.SummaryRows.Add(new AccessorySummaryRow
                {
                    CategoryScope = TenderAccessoryRules.NormalizeScope(accessory.CategoryScope),
                    Application = TenderAccessoryRules.NormalizeScope(accessory.Application),
                    SpecKey = TenderAccessoryRules.NormalizeScope(accessory.SpecKey),
                    Name = summaryName,
                    Material = accessory.Material,
                    Position = accessory.Position,
                    Unit = accessory.Unit,
                    RuleLabel = TenderAccessoryRules.GetRuleLabel(accessory.CalcRule),
                    BasisLabel = TenderAccessoryRules.GetBasisLabel(accessory.CalcRule),
                    BasisValue = Round2(totalBasis),
                    Factor = accessory.Factor,
                    WastePercent = accessory.WasteFactor,
                    AutoQuantity = Round2(totalAuto),
                    Adjustment = accessory.Adjustment,
                    FinalQuantity = finalQuantity,
                    Note = accessory.Note
                });
            }

            report.BasisRows.Sort(CompareBasisRows);
            report.SummaryRows.Sort(CompareSummaryRows);
            return report;
        }

        public List<AccessoryBasisRow> CalculateAccessoryBasis(
            List<TenderWall> walls, List<TenderAccessory> accessories)
        {
            return CalculateAccessoryReport(walls, accessories).BasisRows;
        }

        public List<AccessorySummaryRow> CalculateAccessorySummary(
            List<TenderWall> walls, List<TenderAccessory> accessories)
        {
            return CalculateAccessoryReport(walls, accessories).SummaryRows;
        }

        public double CalculateAccessoryForWall(TenderAccessory accessory, TenderWall wall)
        {
            if (!IsAccessoryApplicable(accessory, wall))
                return 0;

            return Round2(GetBasisValue(accessory.CalcRule, wall) * accessory.Factor);
        }

        public static double GetBasisValue(AccessoryCalcRule rule, TenderWall wall)
        {
            return rule switch
            {
                AccessoryCalcRule.PER_WALL_LENGTH => wall.Length / 1000.0,
                AccessoryCalcRule.PER_WALL_HEIGHT => wall.Height / 1000.0,
                AccessoryCalcRule.PER_TOP_EDGE_LENGTH => wall.TopEdgeLength / 1000.0,
                AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH => wall.BottomEdgeLength / 1000.0,
                AccessoryCalcRule.PER_EXPOSED_END_LENGTH => wall.ExposedEndLength / 1000.0,
                AccessoryCalcRule.PER_TOTAL_EXPOSED_EDGE_LENGTH => wall.TotalExposedEdgeLength / 1000.0,
                AccessoryCalcRule.PER_OUTSIDE_CORNER_HEIGHT => wall.OutsideCornerHeight / 1000.0,
                AccessoryCalcRule.PER_INSIDE_CORNER_HEIGHT => wall.InsideCornerHeight / 1000.0,
                AccessoryCalcRule.PER_PANEL_QTY => wall.EstimatedPanelCount,
                AccessoryCalcRule.PER_JOINT_LENGTH =>
                    Math.Max(0, wall.EstimatedPanelCount - 1) * wall.PanelSpan / 1000.0,
                AccessoryCalcRule.PER_OPENING_PERIMETER => wall.TotalOpeningPerimeter / 1000.0,
                AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES => wall.TotalOpeningPerimeterTwoFaces / 1000.0,
                AccessoryCalcRule.PER_DOOR_OPENING_PERIMETER => wall.TotalDoorOpeningPerimeter / 1000.0,
                AccessoryCalcRule.PER_NON_DOOR_OPENING_PERIMETER => wall.TotalNonDoorOpeningPerimeter / 1000.0,
                AccessoryCalcRule.PER_OPENING_QTY => wall.TotalOpeningCount,
                AccessoryCalcRule.PER_DOOR_OPENING_QTY => wall.TotalDoorOpeningCount,
                AccessoryCalcRule.PER_NON_DOOR_OPENING_QTY => wall.TotalNonDoorOpeningCount,
                AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES => wall.TotalOpeningVerticalEdges / 1000.0,
                AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH => wall.TotalOpeningHorizontalTopLength / 1000.0,
                AccessoryCalcRule.PER_OPENING_SILL_LENGTH => wall.TotalOpeningSillLength / 1000.0,
                AccessoryCalcRule.PER_NET_AREA => wall.NetAreaM2,
                AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH => GetColdStorageTSuspensionLength(wall),
                AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY => GetColdStorageTSuspensionPointQty(wall),
                AccessoryCalcRule.PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH => GetColdStorageTWireRopeLength(wall),
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH => GetColdStorageMushroomSuspensionLength(wall),
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY => GetColdStorageMushroomSuspensionPointQty(wall),
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH => GetColdStorageMushroomWireRopeLength(wall),
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_BOLT_QTY => GetColdStorageMushroomBoltQty(wall),
                AccessoryCalcRule.FIXED_PER_WALL => 1.0,
                // Chỉ tính khi xếp Ngang — trả về 0 khi xếp Dọc
                AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT => wall.VerticalJointTotalLength / 1000.0,
                // PanelCount × ceil(PanelSpan/1500mm) × 2 vít/điểm
                AccessoryCalcRule.PER_TEK_SCREW_QTY => GetTekScrewQty(wall),
                _ => 0
            };
        }

        /// <summary>
        /// Tính số vít B2S-TEK: PanelCount × ceil(PanelSpan / 1500mm) × 2 vít/điểm.
        /// PanelSpan = Height (xếp Dọc) hoặc Length (xếp Ngang).
        /// </summary>
        private static double GetTekScrewQty(TenderWall wall)
        {
            const double PurlinSpacingMm = 1500.0;
            const int ScrewsPerPoint = 2;
            if (wall.PanelSpan <= 0)
                return 0;
            int pointCount = (int)Math.Ceiling(wall.PanelSpan / PurlinSpacingMm);
            return wall.EstimatedPanelCount * pointCount * ScrewsPerPoint;
        }

        public static (double TSpacingMm, int TLineCount, double MushroomSpacingMm, int MushroomLineCount)?
            GetColdStorageCeilingPreviewData(TenderWall wall)
        {
            if (!IsColdStorageCeiling(wall))
                return null;

            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null)
                return null;

            return (
                spec.TSpacingMm,
                GetColdStorageTSuspensionLineCount(wall),
                spec.MushroomSpacingMm,
                GetColdStorageMushroomSuspensionLineCount(wall));
        }

        private static bool IsColdStorageCeiling(TenderWall wall)
        {
            return string.Equals(wall.Category, "Trần", StringComparison.OrdinalIgnoreCase)
                && string.Equals(wall.Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase);
        }

        private static ColdStorageSuspensionSpec? ResolveColdStorageSuspensionSpec(int thicknessMm)
        {
            if (thicknessMm <= 0 || ColdStorageSuspensionMap.Count == 0)
                return null;

            if (ColdStorageSuspensionMap.TryGetValue(thicknessMm, out var exact))
                return exact;

            foreach (var kv in ColdStorageSuspensionMap)
            {
                if (kv.Key >= thicknessMm)
                    return kv.Value;
            }

            return ColdStorageSuspensionMap.Values.Last();
        }

        private static double GetSuspensionRunLengthMm(TenderWall wall)
            => Math.Max(0, wall.DivisionSpan);

        private static double GetSuspensionRunWidthMm(TenderWall wall)
            => Math.Max(0, wall.PanelSpan);

        private static int GetSuspensionLineCount(double runWidthMm, double spacingMm)
        {
            if (runWidthMm <= 0 || spacingMm <= 0)
                return 0;

            return (int)Math.Ceiling(runWidthMm / spacingMm);
        }

        private static double GetColdStorageTSuspensionLength(TenderWall wall)
        {
            if (!IsColdStorageCeiling(wall))
                return 0;

            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null)
                return 0;

            int lineCount = GetColdStorageTSuspensionLineCount(wall);
            return lineCount * GetSuspensionRunLengthMm(wall) / 1000.0;
        }

        private static double GetColdStorageMushroomSuspensionLength(TenderWall wall)
        {
            if (!IsColdStorageCeiling(wall))
                return 0;

            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null || spec.MushroomSpacingMm <= 0)
                return 0;

            int lineCount = GetColdStorageMushroomSuspensionLineCount(wall);
            return lineCount * GetSuspensionRunLengthMm(wall) / 1000.0;
        }

        private static int GetColdStorageTSuspensionLineCount(TenderWall wall)
        {
            if (!IsColdStorageCeiling(wall))
                return 0;

            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null)
                return 0;

            return GetSuspensionLineCount(GetSuspensionRunWidthMm(wall), spec.TSpacingMm);
        }

        private static int GetColdStorageMushroomSuspensionLineCount(TenderWall wall)
        {
            if (!IsColdStorageCeiling(wall))
                return 0;

            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null || spec.MushroomSpacingMm <= 0)
                return 0;

            return GetSuspensionLineCount(GetSuspensionRunWidthMm(wall), spec.MushroomSpacingMm);
        }

        private static double GetSuspensionPointQty(double totalRunLengthM)
        {
            double totalRunLengthMm = totalRunLengthM * 1000.0;
            if (totalRunLengthMm <= 0)
                return 0;

            return Math.Ceiling(totalRunLengthMm / SuspensionPointSpacingMm);
        }

        private static double GetColdStorageTSuspensionPointQty(TenderWall wall)
            => GetSuspensionPointQty(GetColdStorageTSuspensionLength(wall));

        private static double GetColdStorageMushroomSuspensionPointQty(TenderWall wall)
            => GetSuspensionPointQty(GetColdStorageMushroomSuspensionLength(wall));

        private static double GetWireRopeLength(double pointQty, double cableDropLengthMm)
        {
            if (pointQty <= 0 || cableDropLengthMm <= 0)
                return 0;

            return pointQty * cableDropLengthMm / 1000.0;
        }

        private static double GetColdStorageTWireRopeLength(TenderWall wall)
            => GetWireRopeLength(GetColdStorageTSuspensionPointQty(wall), wall.CableDropLengthMm);

        private static double GetColdStorageMushroomWireRopeLength(TenderWall wall)
            => GetWireRopeLength(GetColdStorageMushroomSuspensionPointQty(wall), wall.CableDropLengthMm);

        private static double GetColdStorageMushroomBoltQty(TenderWall wall)
        {
            if (!IsColdStorageCeiling(wall))
                return 0;

            return GetColdStorageMushroomSuspensionLineCount(wall) * Math.Max(0, wall.EstimatedPanelCount);
        }

        private static bool IsAccessoryApplicable(TenderAccessory accessory, TenderWall wall)
        {
            if (!TenderAccessoryRules.IsAllScope(accessory.CategoryScope) &&
                !string.Equals(accessory.CategoryScope, wall.Category, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!TenderAccessoryRules.IsAllScope(accessory.SpecKey) &&
                !string.Equals(accessory.SpecKey, wall.SpecKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!TenderAccessoryRules.IsAllScope(accessory.Application) &&
                !string.Equals(accessory.Application, wall.Application, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static double CalculateFinalQuantity(TenderAccessory accessory, double totalAuto)
        {
            double quantity = accessory.IsManualOnly
                ? accessory.Adjustment
                : totalAuto * (1.0 + accessory.WasteFactor / 100.0) + accessory.Adjustment;

            return Round2(Math.Max(0, quantity));
        }

        private static double Round2(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private static int CompareBasisRows(AccessoryBasisRow left, AccessoryBasisRow right)
        {
            int result = TenderAccessoryRules.CompareApplications(left.Application, right.Application);
            if (result != 0) return result;

            result = string.Compare(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            result = string.Compare(left.SpecKey, right.SpecKey, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            result = string.Compare(left.Floor, right.Floor, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            result = string.Compare(left.WallName, right.WallName, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            result = string.Compare(left.AccessoryName, right.AccessoryName, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            result = string.Compare(left.Position, right.Position, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            return string.Compare(left.RuleLabel, right.RuleLabel, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareSummaryRows(AccessorySummaryRow left, AccessorySummaryRow right)
        {
            int result = TenderAccessoryRules.CompareApplications(left.Application, right.Application);
            if (result != 0) return result;

            result = TenderAccessoryRules.CompareScopes(left.CategoryScope, right.CategoryScope);
            if (result != 0) return result;

            result = TenderAccessoryRules.CompareScopes(left.SpecKey, right.SpecKey);
            if (result != 0) return result;

            result = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            result = string.Compare(left.Position, right.Position, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            return string.Compare(left.RuleLabel, right.RuleLabel, StringComparison.OrdinalIgnoreCase);
        }
    }
}
