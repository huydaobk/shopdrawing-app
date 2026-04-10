using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            public int ReducedPanels { get; set; }
            public int WastePieces { get; set; }
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

        private sealed record ColdStorageSuspensionSpec(double TSpacingMm, double MushroomOffsetMm);
        private sealed record SuspensionMetrics(int LineCount, double TotalSegmentLengthMm, double PointQty);

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
                    int reducedPanels = g.Sum(w => w
                        .GetPanelBreakdown()
                        .Where(entry => string.Equals(entry.Label, "Giảm (lỗ mở)", StringComparison.OrdinalIgnoreCase))
                        .Sum(entry => entry.Count));
                    int wastePieces = g.Sum(w => w
                        .GetPanelBreakdown()
                        .Where(entry => string.Equals(entry.Label, "Hao hụt", StringComparison.OrdinalIgnoreCase))
                        .Sum(entry => entry.Count));

                    return new PanelSummaryRow
                    {
                        Floor = g.Key.Floor,
                        SpecKey = g.Key.SpecKey,
                        Category = g.Key.Category,
                        WallCount = g.Count(),
                        TotalLengthM = g.Sum(w => w.Length) / 1000.0,
                        HeightMm = g.Average(w => w.RepresentativeHeightMm),
                        WallAreaM2 = g.Sum(w => w.WallAreaM2),
                        OpeningAreaM2 = g.Sum(w => w.OpeningAreaM2),
                        NetAreaM2 = netArea,
                        EstimatedPanels = g.Sum(w => w.EstimatedPanelCount),
                        ReducedPanels = reducedPanels,
                        WastePieces = wastePieces,
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
            var summaryCandidates = new List<AccessorySummaryRow>();

            foreach (var accessory in accessories)
            {
                if (!IsAccessoryRuleSupported(accessory.CalcRule))
                    continue;

                var applicableWalls = walls
                    .Where(w => IsAccessoryApplicable(accessory, w))
                    .ToList();

                if (applicableWalls.Count == 0)
                    continue;

                double totalBasis = 0;
                double totalAuto = 0;
                var summaryBuckets = new Dictionary<string, (string Name, string Material, List<(TenderWall Wall, double BasisValue, double AutoQuantity)> Items)>(StringComparer.OrdinalIgnoreCase);

                foreach (var wall in applicableWalls)
                {
                    double basisValue = GetBasisValue(accessory.CalcRule, wall);
                    double autoQuantity = accessory.IsManualOnly
                        ? 0
                        : Round2(basisValue * accessory.Factor);

                    if (basisValue <= 0 && autoQuantity <= 0)
                        continue;

                    totalBasis += basisValue;
                    totalAuto += autoQuantity;

                    string accessoryDisplayName = GetAccessoryDisplayName(accessory, wall);
                    string accessoryDisplayMaterial = GetAccessoryDisplayMaterial(accessory, wall);
                    string summaryBucketKey = $"{accessoryDisplayName}\u001F{accessoryDisplayMaterial}";

                    if (!summaryBuckets.TryGetValue(summaryBucketKey, out var bucket))
                    {
                        bucket = (accessoryDisplayName, accessoryDisplayMaterial, new List<(TenderWall Wall, double BasisValue, double AutoQuantity)>());
                        summaryBuckets[summaryBucketKey] = bucket;
                    }

                    bucket.Items.Add((wall, basisValue, autoQuantity));

                    report.BasisRows.Add(new AccessoryBasisRow
                    {
                        Category = wall.Category,
                        Floor = wall.Floor,
                        WallName = wall.Name,
                        Application = wall.Application,
                        SpecKey = wall.SpecKey,
                        AccessoryName = accessoryDisplayName,
                        Material = accessoryDisplayMaterial,
                        Position = accessory.Position,
                        Unit = accessory.Unit,
                        RuleLabel = TenderAccessoryRules.GetRuleLabel(accessory.CalcRule),
                        BasisLabel = TenderAccessoryRules.GetBasisLabel(accessory.CalcRule),
                        BasisValue = Round2(basisValue),
                        Factor = accessory.Factor,
                        AutoQuantity = autoQuantity,
                        Note = BuildBasisNote(accessory, wall, basisValue, autoQuantity)
                    });
                }

                if (totalAuto <= 0 && Math.Abs(accessory.Adjustment) <= 0)
                    continue;

                foreach (var pair in summaryBuckets)
                {
                    string summaryName = pair.Value.Name;
                    string summaryMaterial = pair.Value.Material;
                    var bucketItems = pair.Value.Items;
                    var bucketWalls = bucketItems.Select(x => x.Wall).ToList();
                    double bucketBasis = Round2(bucketItems.Sum(x => x.BasisValue));
                    double bucketAuto = Round2(bucketItems.Sum(x => x.AutoQuantity));
                    double bucketAdjustment = AllocateAccessoryAdjustment(
                        accessory.Adjustment,
                        bucketAuto,
                        totalAuto,
                        summaryBuckets.Count == 1);
                    double bucketFinal = CalculateFinalQuantity(accessory, bucketAuto, bucketAdjustment);

                    if (bucketAuto <= 0 && Math.Abs(bucketFinal) <= 0 && Math.Abs(bucketAdjustment) <= 0)
                        continue;

                    summaryCandidates.Add(new AccessorySummaryRow
                    {
                        CategoryScope = TenderAccessoryRules.NormalizeScope(accessory.CategoryScope),
                        Application = TenderAccessoryRules.NormalizeScope(accessory.Application),
                        SpecKey = TenderAccessoryRules.NormalizeScope(accessory.SpecKey),
                        Name = summaryName,
                        Material = summaryMaterial,
                        Position = accessory.Position,
                        Unit = accessory.Unit,
                        RuleLabel = TenderAccessoryRules.GetRuleLabel(accessory.CalcRule),
                        BasisLabel = TenderAccessoryRules.GetBasisLabel(accessory.CalcRule),
                        BasisValue = bucketBasis,
                        Factor = accessory.Factor,
                        WastePercent = accessory.WasteFactor,
                        AutoQuantity = bucketAuto,
                        Adjustment = bucketAdjustment,
                        FinalQuantity = bucketFinal,
                        Note = BuildSummarySeedNote(accessory, bucketWalls, bucketBasis, bucketAuto)
                    });
                }
            }

            report.BasisRows.Sort(CompareBasisRows);
            report.SummaryRows.AddRange(GroupSummaryRows(summaryCandidates));
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
                AccessoryCalcRule.PER_WALL_HEIGHT => wall.RepresentativeHeightMm / 1000.0,
                AccessoryCalcRule.PER_TOP_EDGE_LENGTH => wall.TopEdgeLength / 1000.0,
                AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH => wall.TopPanelCeilingCenterLength / 1000.0,
                AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH => wall.TopPanelCeilingPerimeterLength / 1000.0,
                AccessoryCalcRule.PER_TOP_PANEL_FREE_LENGTH => wall.TopPanelFreeLength / 1000.0,
                AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH => wall.BottomEdgeLength / 1000.0,
                AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH => wall.EndPanelCenterLength / 1000.0,
                AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH => wall.EndPanelPerimeterLength / 1000.0,
                AccessoryCalcRule.PER_END_PANEL_FREE_LENGTH => wall.EndPanelFreeLength / 1000.0,
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
                AccessoryCalcRule.PER_PANEL_SUPPORT_BRACKET_QTY => GetPanelSupportBracketQty(wall),
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

        /// <summary>
        /// Tính số bát thép chân panel: ceil(chiều dài vách / 1500mm).
        /// </summary>
        private static double GetPanelSupportBracketQty(TenderWall wall)
        {
            const double PurlinSpacingMm = 1500.0;
            if (wall.Length <= 0)
                return 0;

            return Math.Ceiling(wall.Length / PurlinSpacingMm);
        }

        private static int GetExposedEndCount(TenderWall wall)
            => (wall.StartEdgeExposed ? 1 : 0) + (wall.EndEdgeExposed ? 1 : 0);

        public static (double TSpacingMm, int TLineCount, double MushroomOffsetMm, int MushroomLineCount)?
            GetColdStorageCeilingPreviewData(TenderWall wall)
        {
            if (!IsSupportedSuspendedCeiling(wall))
                return null;

            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null)
                return null;

            var tMetrics = GetSuspensionMetrics(wall, spec.TSpacingMm, spec.TSpacingMm);
            var mushroomMetrics = spec.MushroomOffsetMm > 0
                ? GetSuspensionMetrics(wall, spec.TSpacingMm, spec.MushroomOffsetMm)
                : null;

            return (
                spec.TSpacingMm,
                tMetrics.LineCount,
                spec.MushroomOffsetMm,
                mushroomMetrics?.LineCount ?? 0);
        }

        private static bool IsColdStorageCeiling(TenderWall wall)
        {
            return string.Equals(wall.Category, "Trần", StringComparison.OrdinalIgnoreCase)
                && string.Equals(wall.Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSupportedSuspendedCeiling(TenderWall wall)
        {
            return string.Equals(wall.Category, "Trần", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(wall.Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(wall.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase));
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

        private static bool IsSuspensionRunAlongX(TenderWall wall)
            => !string.Equals(GetSuspensionLayoutDirection(wall), "Ngang", StringComparison.OrdinalIgnoreCase);

        private static string GetSuspensionLayoutDirection(TenderWall wall)
        {
            if (!string.IsNullOrWhiteSpace(wall.SuspensionLayoutDirection))
                return wall.SuspensionLayoutDirection;

            return wall.LayoutDirection;
        }

        private static List<double[]> GetSuspensionVertices(TenderWall wall)
        {
            if (wall.PolygonVertices != null && wall.PolygonVertices.Count >= 3)
                return wall.PolygonVertices;

            double length = Math.Max(0, wall.Length);
            double height = Math.Max(0, wall.Height);
            return new List<double[]>
            {
                new[] { 0.0, 0.0 },
                new[] { length, 0.0 },
                new[] { length, height },
                new[] { 0.0, height }
            };
        }

        private static List<double> BuildSuspensionLinePositions(
            List<double[]> vertices,
            bool runAlongX,
            bool divideFromMaxSide,
            double cycleSpacingMm,
            double initialOffsetMm)
        {
            var positions = new List<double>();
            if (vertices.Count < 3 || cycleSpacingMm <= 0 || initialOffsetMm <= 0)
                return positions;

            double min = runAlongX ? vertices.Min(v => v[1]) : vertices.Min(v => v[0]);
            double max = runAlongX ? vertices.Max(v => v[1]) : vertices.Max(v => v[0]);
            if (max - min <= initialOffsetMm)
                return positions;

            const double edgeOffset = 0.5;
            if (divideFromMaxSide)
            {
                for (double pos = max - initialOffsetMm; pos > min + edgeOffset - 1e-6; pos -= cycleSpacingMm)
                {
                    if (positions.Count == 0 || Math.Abs(positions[^1] - pos) > 1.0)
                        positions.Add(pos);
                }
            }
            else
            {
                for (double pos = min + initialOffsetMm; pos < max - edgeOffset + 1e-6; pos += cycleSpacingMm)
                {
                    if (positions.Count == 0 || Math.Abs(positions[^1] - pos) > 1.0)
                        positions.Add(pos);
                }
            }

            return positions;
        }

        private static List<(double Start, double End)> GetSuspensionScanSegments(
            List<double[]> vertices,
            double scanPos,
            bool horizontalLine)
        {
            var intersections = new List<double>();
            int count = vertices.Count;

            for (int i = 0; i < count; i++)
            {
                var p1 = vertices[i];
                var p2 = vertices[(i + 1) % count];

                double s1 = horizontalLine ? p1[1] : p1[0];
                double s2 = horizontalLine ? p2[1] : p2[0];
                double m1 = horizontalLine ? p1[0] : p1[1];
                double m2 = horizontalLine ? p2[0] : p2[1];

                if ((s1 <= scanPos && s2 > scanPos) || (s2 <= scanPos && s1 > scanPos))
                {
                    double t = (scanPos - s1) / (s2 - s1);
                    intersections.Add(m1 + t * (m2 - m1));
                }
            }

            intersections.Sort();
            var segments = new List<(double Start, double End)>();
            for (int i = 0; i + 1 < intersections.Count; i += 2)
            {
                double start = intersections[i];
                double end = intersections[i + 1];
                if (end - start > 1.0)
                    segments.Add((start, end));
            }

            return segments;
        }

        private static double GetSuspensionPointCountForSegment(double segmentLengthMm)
        {
            if (segmentLengthMm <= 1.0)
                return 0;

            return Math.Max(1, Math.Ceiling(segmentLengthMm / SuspensionPointSpacingMm));
        }

        private static SuspensionMetrics GetSuspensionMetrics(TenderWall wall, double cycleSpacingMm, double initialOffsetMm)
        {
            if (!IsSupportedSuspendedCeiling(wall) || cycleSpacingMm <= 0 || initialOffsetMm <= 0)
                return new SuspensionMetrics(0, 0, 0);

            var vertices = GetSuspensionVertices(wall);
            bool runAlongX = IsSuspensionRunAlongX(wall);
            var positions = BuildSuspensionLinePositions(
                vertices,
                runAlongX,
                wall.ColdStorageDivideFromMaxSide,
                cycleSpacingMm,
                initialOffsetMm);

            double totalSegmentLengthMm = 0;
            double pointQty = 0;

            foreach (double pos in positions)
            {
                foreach (var segment in GetSuspensionScanSegments(vertices, pos, runAlongX))
                {
                    double segmentLengthMm = Math.Max(0, segment.End - segment.Start);
                    if (segmentLengthMm <= 1.0)
                        continue;

                    totalSegmentLengthMm += segmentLengthMm;
                    pointQty += GetSuspensionPointCountForSegment(segmentLengthMm);
                }
            }

            return new SuspensionMetrics(positions.Count, totalSegmentLengthMm, pointQty);
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
            if (!IsSupportedSuspendedCeiling(wall))
                return 0;

            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null)
                return 0;

            return GetSuspensionMetrics(wall, spec.TSpacingMm, spec.TSpacingMm).TotalSegmentLengthMm / 1000.0;
        }

        private static double GetColdStorageMushroomSuspensionLength(TenderWall wall)
        {
            if (!IsSupportedSuspendedCeiling(wall))
                return 0;

            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null || spec.MushroomOffsetMm <= 0)
                return 0;

            return GetSuspensionMetrics(wall, spec.TSpacingMm, spec.MushroomOffsetMm).TotalSegmentLengthMm / 1000.0;
        }

        private static int GetColdStorageTSuspensionLineCount(TenderWall wall)
        {
            if (!IsSupportedSuspendedCeiling(wall))
                return 0;

            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null)
                return 0;

            return GetSuspensionMetrics(wall, spec.TSpacingMm, spec.TSpacingMm).LineCount;
        }

        private static int GetColdStorageMushroomSuspensionLineCount(TenderWall wall)
        {
            if (!IsSupportedSuspendedCeiling(wall))
                return 0;

            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null || spec.MushroomOffsetMm <= 0)
                return 0;

            return GetSuspensionMetrics(wall, spec.TSpacingMm, spec.MushroomOffsetMm).LineCount;
        }

        private static double GetSuspensionPointQty(double totalRunLengthM)
        {
            double totalRunLengthMm = totalRunLengthM * 1000.0;
            if (totalRunLengthMm <= 0)
                return 0;

            return Math.Ceiling(totalRunLengthMm / SuspensionPointSpacingMm);
        }

        private static double GetColdStorageTSuspensionPointQty(TenderWall wall)
        {
            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null)
                return 0;

            return GetSuspensionMetrics(wall, spec.TSpacingMm, spec.TSpacingMm).PointQty;
        }

        private static double GetColdStorageMushroomSuspensionPointQty(TenderWall wall)
        {
            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null || spec.MushroomOffsetMm <= 0)
                return 0;

            return GetSuspensionMetrics(wall, spec.TSpacingMm, spec.MushroomOffsetMm).PointQty;
        }

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
            if (!IsSupportedSuspendedCeiling(wall))
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

        private static double CalculateFinalQuantity(TenderAccessory accessory, double totalAuto, double adjustment)
        {
            double quantity = accessory.IsManualOnly
                ? adjustment
                : totalAuto * (1.0 + accessory.WasteFactor / 100.0) + adjustment;

            return Round2(Math.Max(0, quantity));
        }

        private static double AllocateAccessoryAdjustment(
            double totalAdjustment,
            double bucketAuto,
            double totalAuto,
            bool singleBucket)
        {
            if (Math.Abs(totalAdjustment) <= 0)
                return 0;

            if (singleBucket || totalAuto <= 0)
                return totalAdjustment;

            return Round2(totalAdjustment * bucketAuto / totalAuto);
        }

        private static string GetAccessoryDisplayName(TenderAccessory accessory, TenderWall wall)
        {
            if (accessory.Name == AccessoryDataManager.ExteriorTekScrewBaseName
                && accessory.CalcRule == AccessoryCalcRule.PER_TEK_SCREW_QTY
                && string.Equals(wall.Application, "Ngoài nhà", StringComparison.OrdinalIgnoreCase)
                && string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase)
                && wall.PanelThickness > 0)
            {
                return AccessoryDataManager.GetAutoSizedScrewName(accessory.Name, wall.PanelThickness);
            }

            if (accessory.Name == AccessoryDataManager.ColdStorageBottomUChannelBaseName
                && accessory.CalcRule == AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH
                && string.Equals(wall.Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase)
                && string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase)
                && wall.PanelThickness > 0)
            {
                return AccessoryDataManager.GetColdStorageBottomUChannelName(wall.PanelThickness);
            }

            if (accessory.Name == AccessoryDataManager.CleanroomBottomUChannelBaseName
                && (accessory.CalcRule == AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH
                    || accessory.CalcRule == AccessoryCalcRule.PER_OPENING_PERIMETER)
                && string.Equals(wall.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase)
                && string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase)
                && wall.PanelThickness > 0)
            {
                return AccessoryDataManager.GetCleanroomBottomUChannelName(wall.PanelThickness);
            }

            return accessory.Name;
        }

        private static string GetAccessoryDisplayMaterial(TenderAccessory accessory, TenderWall wall)
        {
            if (accessory.Name == AccessoryDataManager.ColdStorageBottomUChannelBaseName
                && accessory.CalcRule == AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH
                && string.Equals(wall.Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase)
                && string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase)
                && wall.PanelThickness > 0)
            {
                return AccessoryDataManager.GetColdStorageBottomUChannelMaterial(wall.PanelThickness);
            }

            if (accessory.Name == AccessoryDataManager.CleanroomBottomUChannelBaseName
                && (accessory.CalcRule == AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH
                    || accessory.CalcRule == AccessoryCalcRule.PER_OPENING_PERIMETER)
                && string.Equals(wall.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase)
                && string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase)
                && wall.PanelThickness > 0)
            {
                return AccessoryDataManager.GetCleanroomBottomUChannelMaterial(wall.PanelThickness);
            }

            return accessory.Material;
        }

        private static bool IsAccessoryRuleSupported(AccessoryCalcRule rule)
        {
            return rule switch
            {
                AccessoryCalcRule.PER_WALL_LENGTH => true,
                AccessoryCalcRule.PER_WALL_HEIGHT => true,
                AccessoryCalcRule.PER_TOP_EDGE_LENGTH => true,
                AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH => true,
                AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH => true,
                AccessoryCalcRule.PER_TOP_PANEL_FREE_LENGTH => true,
                AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH => true,
                AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH => true,
                AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH => true,
                AccessoryCalcRule.PER_END_PANEL_FREE_LENGTH => true,
                AccessoryCalcRule.PER_EXPOSED_END_LENGTH => true,
                AccessoryCalcRule.PER_TOTAL_EXPOSED_EDGE_LENGTH => true,
                AccessoryCalcRule.PER_OUTSIDE_CORNER_HEIGHT => true,
                AccessoryCalcRule.PER_INSIDE_CORNER_HEIGHT => true,
                AccessoryCalcRule.PER_PANEL_QTY => true,
                AccessoryCalcRule.PER_JOINT_LENGTH => true,
                AccessoryCalcRule.PER_OPENING_PERIMETER => true,
                AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES => true,
                AccessoryCalcRule.PER_DOOR_OPENING_PERIMETER => true,
                AccessoryCalcRule.PER_NON_DOOR_OPENING_PERIMETER => true,
                AccessoryCalcRule.PER_OPENING_QTY => true,
                AccessoryCalcRule.PER_DOOR_OPENING_QTY => true,
                AccessoryCalcRule.PER_NON_DOOR_OPENING_QTY => true,
                AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES => true,
                AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH => true,
                AccessoryCalcRule.PER_OPENING_SILL_LENGTH => true,
                AccessoryCalcRule.PER_NET_AREA => true,
                AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH => true,
                AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY => true,
                AccessoryCalcRule.PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH => true,
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH => true,
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY => true,
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH => true,
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_BOLT_QTY => true,
                AccessoryCalcRule.FIXED_PER_WALL => true,
                AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT => true,
                AccessoryCalcRule.PER_TEK_SCREW_QTY => true,
                AccessoryCalcRule.PER_PANEL_SUPPORT_BRACKET_QTY => true,
                _ => false
            };
        }

        private static string BuildBasisNote(
            TenderAccessory accessory,
            TenderWall wall,
            double basisValue,
            double autoQuantity)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(accessory.Position))
                parts.Add($"Vị trí {accessory.Position}");

            parts.Add($"{wall.Name} | {wall.Category} | {wall.Application}");
            parts.Add(BuildBasisMetricNote(accessory, wall, basisValue, autoQuantity));

            string staticNote = NormalizeNoteText(accessory.Note);
            if (!string.IsNullOrWhiteSpace(staticNote))
                parts.Add(staticNote);

            return string.Join("; ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        private static string BuildSummarySeedNote(
            TenderAccessory accessory,
            IReadOnlyList<TenderWall> applicableWalls,
            double totalBasis,
            double totalAuto)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(accessory.Position))
                parts.Add($"Vị trí {accessory.Position}");

            parts.Add(BuildWallScopeText(applicableWalls));
            parts.Add(BuildSummaryMetricNote(accessory, applicableWalls, totalBasis, totalAuto));

            string staticNote = NormalizeNoteText(accessory.Note);
            if (!string.IsNullOrWhiteSpace(staticNote))
                parts.Add(staticNote);

            return string.Join("; ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        private static List<AccessorySummaryRow> GroupSummaryRows(IEnumerable<AccessorySummaryRow> rows)
        {
            return rows
                .Where(r => r.AutoQuantity > 0 || Math.Abs(r.FinalQuantity) > 0 || Math.Abs(r.Adjustment) > 0)
                .GroupBy(r => new
                {
                    Name = NormalizeAccessoryGroupingName(r.Name),
                    Material = NormalizeAccessoryGroupingMaterial(r.Name, r.Material),
                    Unit = NormalizeAccessoryGroupingUnit(r.Unit)
                })
                .Select(g =>
                {
                    var items = g.ToList();
                    double basis = Round2(items.Sum(x => x.BasisValue));
                    double autoQty = Round2(items.Sum(x => x.AutoQuantity));
                    double adjustment = Round2(items.Sum(x => x.Adjustment));
                    double finalQty = Round2(items.Sum(x => x.FinalQuantity));
                    double factor = basis > 0
                        ? Round2(items.Sum(x => x.AutoQuantity) / basis)
                        : items.First().Factor;
                    double waste = autoQty > 0
                        ? Round2(items.Sum(x => x.AutoQuantity * x.WastePercent) / autoQty)
                        : items.Max(x => x.WastePercent);

                    return new AccessorySummaryRow
                    {
                        CategoryScope = BuildSummaryDisplayValue(items.Select(x => x.CategoryScope)),
                        Application = BuildSummaryDisplayValue(items.Select(x => x.Application)),
                        SpecKey = BuildSummaryDisplayValue(items.Select(x => x.SpecKey)),
                        Name = g.Key.Name,
                        Material = g.Key.Material,
                        Position = JoinDistinct(items.Select(x => x.Position), " + "),
                        Unit = g.Key.Unit,
                        RuleLabel = GetCommonValueOrDefault(items.Select(x => x.RuleLabel), "Tổng hợp nhiều quy tắc"),
                        BasisLabel = GetCommonValueOrDefault(items.Select(x => x.BasisLabel), "Tổng hợp nhiều cơ sở"),
                        BasisValue = basis,
                        Factor = factor,
                        WastePercent = waste,
                        AutoQuantity = autoQty,
                        Adjustment = adjustment,
                        FinalQuantity = finalQty,
                        Note = BuildGroupedSummaryNote(items)
                    };
                })
                .Where(r => r.AutoQuantity > 0 || Math.Abs(r.FinalQuantity) > 0 || Math.Abs(r.Adjustment) > 0)
                .ToList();
        }

        private static string BuildGroupedSummaryNote(IReadOnlyList<AccessorySummaryRow> rows)
        {
            var parts = rows
                .Select(r => NormalizeNoteText(r.Note))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (parts.Count == 0)
                return string.Empty;

            if (parts.Count == 1)
                return parts[0];

            if (parts.Count > 4)
            {
                int remain = parts.Count - 4;
                parts = parts.Take(4).ToList();
                parts.Add($"+ {remain} nhóm vị trí");
            }

            return string.Join(" | ", parts);
        }

        private static string BuildBasisMetricNote(
            TenderAccessory accessory,
            TenderWall wall,
            double basisValue,
            double autoQuantity)
        {
            if (TryBuildCleanroomTopMetricNote(accessory, wall, basisValue, autoQuantity, out string? cleanroomTopNote))
                return cleanroomTopNote ?? string.Empty;

            if (TryBuildCleanroomEndMetricNote(accessory, wall, basisValue, autoQuantity, out string? cleanroomEndNote))
                return cleanroomEndNote ?? string.Empty;

            if (TryBuildColdStorageTopMetricNote(accessory, wall, basisValue, autoQuantity, out string? coldStorageTopNote))
                return coldStorageTopNote ?? string.Empty;

            if (TryBuildColdStorageEndMetricNote(accessory, wall, basisValue, autoQuantity, out string? coldStorageEndNote))
                return coldStorageEndNote ?? string.Empty;

            if (TryBuildCleanroomBottomMetricNote(accessory, wall, basisValue, autoQuantity, out string? cleanroomBottomNote))
                return cleanroomBottomNote ?? string.Empty;

            if (TryBuildColdStorageBottomMetricNote(accessory, wall, basisValue, autoQuantity, out string? coldStorageBottomNote))
                return coldStorageBottomNote ?? string.Empty;

            if (accessory.Name == AccessoryDataManager.CleanroomBottomUChannelBaseName
                && accessory.CalcRule == AccessoryCalcRule.PER_OPENING_PERIMETER
                && string.Equals(wall.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase)
                && string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase))
            {
                string displayName = GetAccessoryDisplayName(accessory, wall);
                return $"lỗ mở phòng sạch: 4 cạnh dùng {displayName}, tổng chu vi = {basisValue:F2} md";
            }

            if (accessory.CalcRule == AccessoryCalcRule.PER_OPENING_PERIMETER
                && string.Equals(wall.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase)
                && string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase))
            {
                string displayName = GetAccessoryDisplayName(accessory, wall);
                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                    return $"lỗ mở phòng sạch: 2 tuyến silicone theo 4 cạnh U = {basisValue * 2.0:F2} md, quy đổi = {autoQuantity:F2} chai";

                if (string.Equals(displayName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                    return $"lỗ mở phòng sạch: rive theo 4 cạnh U @500 = {autoQuantity:F2} cái";
            }

            return accessory.CalcRule switch
            {
                AccessoryCalcRule.PER_WALL_LENGTH =>
                    $"lấy chiều dài {wall.Length / 1000.0:F2} md",
                AccessoryCalcRule.PER_WALL_HEIGHT =>
                    $"lấy chiều cao hiệu dụng {wall.RepresentativeHeightMm / 1000.0:F2} md",
                AccessoryCalcRule.PER_TOP_EDGE_LENGTH =>
                    $"đỉnh vách: {wall.ResolvedTopPanelTreatment}, dài {wall.TopEdgeLength / 1000.0:F2} md",
                AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH =>
                    $"đỉnh vách: giao trần giữa, dài {basisValue:F2} md",
                AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH =>
                    $"đỉnh vách: giao biên trần, dài {basisValue:F2} md",
                AccessoryCalcRule.PER_TOP_PANEL_FREE_LENGTH =>
                    $"đỉnh vách: mép tự do, dài {basisValue:F2} md",
                AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH =>
                    $"lấy chân vách {wall.BottomEdgeLength / 1000.0:F2} md",
                AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH =>
                    $"đầu/cuối vách: giao giữa, dài {basisValue:F2} md ({GetExposedEndCount(wall)} mép đứng xử lý)",
                AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH =>
                    $"đầu/cuối vách: giao biên, dài {basisValue:F2} md ({GetExposedEndCount(wall)} mép đứng xử lý)",
                AccessoryCalcRule.PER_END_PANEL_FREE_LENGTH =>
                    $"đầu/cuối vách: mép đứng tự do, dài {basisValue:F2} md ({GetExposedEndCount(wall)} mép đứng xử lý)",
                AccessoryCalcRule.PER_EXPOSED_END_LENGTH =>
                    $"lấy tổng chiều cao mép đứng xử lý {wall.ExposedEndLength / 1000.0:F2} md",
                AccessoryCalcRule.PER_TOTAL_EXPOSED_EDGE_LENGTH =>
                    $"tổng chiều dài biên vách xử lý {wall.TotalExposedEdgeLength / 1000.0:F2} md",
                AccessoryCalcRule.PER_OUTSIDE_CORNER_HEIGHT =>
                    $"{Math.Max(0, wall.OutsideCornerCount)} góc ngoài × cao hiệu dụng {wall.RepresentativeHeightMm / 1000.0:F2} m = {basisValue:F2} md",
                AccessoryCalcRule.PER_INSIDE_CORNER_HEIGHT =>
                    $"{Math.Max(0, wall.InsideCornerCount)} góc trong × cao hiệu dụng {wall.RepresentativeHeightMm / 1000.0:F2} m = {basisValue:F2} md",
                AccessoryCalcRule.PER_PANEL_QTY =>
                    $"{wall.EstimatedPanelCount} tấm sơ bộ | khổ {wall.PanelWidth} | span {wall.PanelSpan:F0} mm",
                AccessoryCalcRule.PER_JOINT_LENGTH =>
                    $"{Math.Max(0, wall.EstimatedPanelCount - 1)} khe × span {wall.PanelSpan:F0} mm = {basisValue:F2} md",
                AccessoryCalcRule.PER_OPENING_PERIMETER =>
                    $"chu vi lỗ mở 1 mặt = {basisValue:F2} md",
                AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES =>
                    $"chu vi lỗ mở 2 mặt = {basisValue:F2} md",
                AccessoryCalcRule.PER_DOOR_OPENING_PERIMETER =>
                    $"{wall.TotalDoorOpeningCount} cửa đi | chu vi tính = {basisValue:F2} md",
                AccessoryCalcRule.PER_NON_DOOR_OPENING_PERIMETER =>
                    $"{wall.TotalNonDoorOpeningCount} lỗ KT/cửa sổ | chu vi tính = {basisValue:F2} md",
                AccessoryCalcRule.PER_OPENING_QTY =>
                    $"số lỗ mở = {wall.TotalOpeningCount}",
                AccessoryCalcRule.PER_DOOR_OPENING_QTY =>
                    $"số cửa đi = {wall.TotalDoorOpeningCount}",
                AccessoryCalcRule.PER_NON_DOOR_OPENING_QTY =>
                    $"số lỗ KT/cửa sổ = {wall.TotalNonDoorOpeningCount}",
                AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES =>
                    $"2 cạnh đứng lỗ mở = {basisValue:F2} md",
                AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH =>
                    $"cạnh đầu lỗ mở = {basisValue:F2} md",
                AccessoryCalcRule.PER_OPENING_SILL_LENGTH =>
                    $"cạnh sill/ngưỡng dưới = {basisValue:F2} md",
                AccessoryCalcRule.PER_NET_AREA =>
                    $"DT net = {wall.NetAreaM2:F2} m²",
                AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH =>
                    BuildSuspensionLengthWallNote(accessory, wall, basisValue, autoQuantity, false, "chiều dài tuyến thanh T"),
                AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY =>
                    BuildSuspensionPointAccessoryWallNote(accessory, wall, basisValue, autoQuantity, false),
                AccessoryCalcRule.PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH =>
                    BuildWireRopeWallNote(wall, basisValue, false),
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH =>
                    BuildSuspensionLengthWallNote(accessory, wall, basisValue, autoQuantity, true, "chiều dài tuyến bu lông nấm/thanh C"),
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY =>
                    BuildSuspensionPointAccessoryWallNote(accessory, wall, basisValue, autoQuantity, true),
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH =>
                    BuildWireRopeWallNote(wall, basisValue, true),
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_BOLT_QTY =>
                    $"{GetColdStorageMushroomSuspensionLineCount(wall)} tuyến bu lông nấm × {wall.EstimatedPanelCount} tấm = {basisValue:F0} cái",
                AccessoryCalcRule.FIXED_PER_WALL =>
                    "1 bộ cho 1 vùng tính khối lượng",
                AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT =>
                    $"{Math.Max(0, wall.VerticalJointCount)} khe đứng × cao hiệu dụng {wall.RepresentativeHeightMm / 1000.0:F2} m = {basisValue:F2} md",
                AccessoryCalcRule.PER_TEK_SCREW_QTY =>
                    $"{wall.EstimatedPanelCount} tấm × {Math.Max(1, Math.Ceiling(wall.PanelSpan / 1500.0))} điểm/tấm × 2 vít = {basisValue:F0} cái",
                AccessoryCalcRule.PER_PANEL_SUPPORT_BRACKET_QTY =>
                    BuildPanelSupportBracketWallNote(accessory, wall, basisValue, autoQuantity),
                _ =>
                    $"cơ sở tính = {basisValue:F2}, KL tự động = {autoQuantity:F2}"
            };
        }

        private static string BuildSummaryMetricNote(
            TenderAccessory accessory,
            IReadOnlyList<TenderWall> applicableWalls,
            double totalBasis,
            double totalAuto)
        {
            if (TryBuildCleanroomTopSummaryNote(accessory, applicableWalls, totalBasis, totalAuto, out string? cleanroomTopNote))
                return cleanroomTopNote ?? string.Empty;

            if (TryBuildCleanroomEndSummaryNote(accessory, applicableWalls, totalBasis, totalAuto, out string? cleanroomEndNote))
                return cleanroomEndNote ?? string.Empty;

            if (TryBuildColdStorageTopSummaryNote(accessory, applicableWalls, totalBasis, totalAuto, out string? coldStorageTopNote))
                return coldStorageTopNote ?? string.Empty;

            if (TryBuildColdStorageEndSummaryNote(accessory, applicableWalls, totalBasis, totalAuto, out string? coldStorageEndNote))
                return coldStorageEndNote ?? string.Empty;

            if (TryBuildCleanroomBottomSummaryNote(accessory, applicableWalls, totalBasis, totalAuto, out string? cleanroomBottomNote))
                return cleanroomBottomNote ?? string.Empty;

            if (TryBuildColdStorageBottomSummaryNote(accessory, applicableWalls, totalBasis, totalAuto, out string? coldStorageBottomNote))
                return coldStorageBottomNote ?? string.Empty;

            if (accessory.Name == AccessoryDataManager.CleanroomBottomUChannelBaseName
                && accessory.CalcRule == AccessoryCalcRule.PER_OPENING_PERIMETER
                && applicableWalls.Count > 0
                && applicableWalls.All(w =>
                    string.Equals(w.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(w.Category, "Vách", StringComparison.OrdinalIgnoreCase)))
            {
                string displayName = GetAccessoryDisplayName(accessory, applicableWalls[0]);
                return $"lỗ mở phòng sạch: tổng chiều dài {displayName} cho 4 cạnh lỗ mở = {totalAuto:F2} md";
            }

            if (accessory.CalcRule == AccessoryCalcRule.PER_OPENING_PERIMETER
                && applicableWalls.Count > 0
                && applicableWalls.All(w =>
                    string.Equals(w.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(w.Category, "Vách", StringComparison.OrdinalIgnoreCase)))
            {
                string displayName = GetAccessoryDisplayName(accessory, applicableWalls[0]);
                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                    return $"lỗ mở phòng sạch: 2 tuyến silicone theo 4 cạnh U, tổng = {totalBasis * 2.0:F2} md, quy đổi = {totalAuto:F2} chai";

                if (string.Equals(displayName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                    return $"lỗ mở phòng sạch: rive theo 4 cạnh U @500, tổng = {totalAuto:F2} cái";
            }

            return accessory.CalcRule switch
            {
                AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH =>
                    BuildSuspensionLengthSummaryNote(accessory, applicableWalls, totalBasis, totalAuto, false, "thanh T"),
                AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY =>
                    BuildSuspensionPointSummaryNote(accessory, totalBasis, totalAuto, false),
                AccessoryCalcRule.PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH =>
                    $"cáp treo thanh T = {totalBasis:F2} md | thả cáp {JoinDistinct(applicableWalls.Select(w => $"{w.CableDropLengthMm:F0}"), "/")} mm",
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH =>
                    BuildSuspensionLengthSummaryNote(accessory, applicableWalls, totalBasis, totalAuto, true, "bu lông nấm"),
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY =>
                    BuildSuspensionPointSummaryNote(accessory, totalBasis, totalAuto, true),
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH =>
                    $"cáp treo bu lông nấm = {totalBasis:F2} md | thả cáp {JoinDistinct(applicableWalls.Select(w => $"{w.CableDropLengthMm:F0}"), "/")} mm",
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_BOLT_QTY =>
                    $"tổng {NormalizeAccessoryGroupingName(accessory.Name)} = {totalBasis:F0} cái",
                AccessoryCalcRule.PER_PANEL_QTY =>
                    $"tổng {totalBasis:F0} tấm sơ bộ",
                AccessoryCalcRule.PER_JOINT_LENGTH =>
                    $"tổng khe nối = {totalBasis:F2} md",
                AccessoryCalcRule.PER_OPENING_PERIMETER =>
                    $"tổng chu vi lỗ mở 1 mặt = {totalBasis:F2} md",
                AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES =>
                    $"tổng chu vi lỗ mở 2 mặt = {totalBasis:F2} md",
                AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES =>
                    $"tổng cạnh đứng lỗ mở = {totalBasis:F2} md",
                AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH =>
                    $"tổng cạnh đầu lỗ mở = {totalBasis:F2} md",
                AccessoryCalcRule.PER_OPENING_SILL_LENGTH =>
                    $"tổng cạnh sill/ngưỡng dưới = {totalBasis:F2} md",
                AccessoryCalcRule.PER_TOP_EDGE_LENGTH =>
                    $"tổng đỉnh vách = {totalBasis:F2} md",
                AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH =>
                    $"tổng đỉnh vách giao trần giữa = {totalBasis:F2} md",
                AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH =>
                    $"tổng đỉnh vách giao biên trần = {totalBasis:F2} md",
                AccessoryCalcRule.PER_TOP_PANEL_FREE_LENGTH =>
                    $"tổng đỉnh vách mép tự do = {totalBasis:F2} md",
                AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH =>
                    $"tổng chân vách = {totalBasis:F2} md",
                AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH =>
                    $"tổng đầu/cuối vách giao giữa = {totalBasis:F2} md",
                AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH =>
                    $"tổng đầu/cuối vách giao biên = {totalBasis:F2} md",
                AccessoryCalcRule.PER_END_PANEL_FREE_LENGTH =>
                    $"tổng đầu/cuối vách mép đứng tự do = {totalBasis:F2} md",
                AccessoryCalcRule.PER_EXPOSED_END_LENGTH =>
                    $"tổng chiều cao mép đứng xử lý = {totalBasis:F2} md",
                AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT =>
                    $"tổng khe đứng x chiều cao = {totalBasis:F2} md",
                AccessoryCalcRule.PER_NET_AREA =>
                    $"tổng DT net = {totalBasis:F2} m²",
                AccessoryCalcRule.PER_TEK_SCREW_QTY =>
                    $"tổng vít tự tính = {totalAuto:F0} cái",
                AccessoryCalcRule.PER_PANEL_SUPPORT_BRACKET_QTY =>
                    BuildPanelSupportBracketSummaryNote(accessory, totalBasis, totalAuto),
                _ =>
                    $"tổng cơ sở {totalBasis:F2}, KL tự động {totalAuto:F2}"
            };
        }

        private static bool TryBuildColdStorageTopMetricNote(
            TenderAccessory accessory,
            TenderWall wall,
            double basisValue,
            double autoQuantity,
            out string? note)
        {
            note = null;

            if (!string.Equals(wall.Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string displayName = GetAccessoryDisplayName(accessory, wall);
            if (accessory.CalcRule == AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName))
                {
                    note = $"đỉnh vách giao trần giữa: 2 tuyến V bo R40 hai bên vách × {basisValue:F2} md = {autoQuantity:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Foam PU bơm tại chỗ", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao trần giữa: 1 tuyến foam PU theo chiều dài {basisValue:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao trần giữa: 2 tuyến silicone = {basisValue * 2.0:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }
            }

            if (accessory.CalcRule == AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName))
                {
                    note = $"đỉnh vách giao biên trần: 1 tuyến V bo R40 phía trong, dài {basisValue:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "V 80x80", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao biên trần: 1 tuyến V 80x80 phía ngoài, dài {basisValue:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Foam PU bơm tại chỗ", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao biên trần: 1 tuyến foam PU theo chiều dài {basisValue:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao biên trần: 2 tuyến silicone hoàn thiện = {basisValue * 2.0:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }
            }

            if (accessory.CalcRule == AccessoryCalcRule.PER_TOP_PANEL_FREE_LENGTH)
            {
                if (string.Equals(displayName, "Diềm 01", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách mép tự do: 1 tuyến diềm tôn, dài {basisValue:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách mép tự do: 1 tuyến silicone hoàn thiện = {basisValue:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildCleanroomTopMetricNote(
            TenderAccessory accessory,
            TenderWall wall,
            double basisValue,
            double autoQuantity,
            out string? note)
        {
            note = null;

            if (!string.Equals(wall.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string displayName = GetAccessoryDisplayName(accessory, wall);
            if (accessory.CalcRule == AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName))
                {
                    note = $"đỉnh vách giao trần giữa phòng sạch: 2 tuyến V bo R40 hai bên vách × {basisValue:F2} md = {autoQuantity:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao trần giữa phòng sạch: 2 tuyến silicone hoàn thiện = {basisValue * 2.0:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao trần giữa phòng sạch: 2 tuyến rive theo 2 V bo R40 @500 = {autoQuantity:F2} cái";
                    return true;
                }
            }

            if (accessory.CalcRule == AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName))
                {
                    note = $"đỉnh vách giao biên trần phòng sạch: 1 tuyến V bo R40 phía trong, dài {basisValue:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "V 40x80", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao biên trần phòng sạch: 1 tuyến V 40x80 nhôm sơn tĩnh điện phía ngoài, dài {basisValue:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao biên trần phòng sạch: 2 tuyến silicone hoàn thiện = {basisValue * 2.0:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao biên trần phòng sạch: 2 tuyến rive theo V 40x80 + V bo R40 @500 = {autoQuantity:F2} cái";
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildColdStorageTopSummaryNote(
            TenderAccessory accessory,
            IReadOnlyList<TenderWall> applicableWalls,
            double totalBasis,
            double totalAuto,
            out string? note)
        {
            note = null;

            if (applicableWalls.Count == 0
                || applicableWalls.Any(w =>
                    !string.Equals(w.Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(w.Category, "Vách", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string displayName = GetAccessoryDisplayName(accessory, applicableWalls[0]);
            if (accessory.CalcRule == AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName))
                {
                    note = $"đỉnh vách giao trần giữa: tổng {totalBasis:F2} md, quy đổi 2 tuyến V bo R40 = {totalAuto:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Foam PU bơm tại chỗ", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao trần giữa: tổng {totalBasis:F2} md foam PU, quy đổi = {totalAuto:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao trần giữa: 2 tuyến silicone, tổng = {totalBasis * 2.0:F2} md, quy đổi = {totalAuto:F2} chai";
                    return true;
                }
            }

            if (accessory.CalcRule == AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName)
                    || string.Equals(displayName, "V 80x80", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao biên trần: tổng chiều dài {displayName} = {totalAuto:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Foam PU bơm tại chỗ", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao biên trần: tổng {totalBasis:F2} md foam PU, quy đổi = {totalAuto:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao biên trần: 2 tuyến silicone hoàn thiện, tổng = {totalBasis * 2.0:F2} md, quy đổi = {totalAuto:F2} chai";
                    return true;
                }
            }

            if (accessory.CalcRule == AccessoryCalcRule.PER_TOP_PANEL_FREE_LENGTH)
            {
                if (string.Equals(displayName, "Diềm 01", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách mép tự do: tổng chiều dài diềm tôn = {totalAuto:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách mép tự do: 1 tuyến silicone hoàn thiện, tổng = {totalBasis:F2} md, quy đổi = {totalAuto:F2} chai";
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildCleanroomTopSummaryNote(
            TenderAccessory accessory,
            IReadOnlyList<TenderWall> applicableWalls,
            double totalBasis,
            double totalAuto,
            out string? note)
        {
            note = null;

            if (applicableWalls.Count == 0
                || applicableWalls.Any(w =>
                    !string.Equals(w.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(w.Category, "Vách", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string displayName = GetAccessoryDisplayName(accessory, applicableWalls[0]);
            if (accessory.CalcRule == AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName))
                {
                    note = $"đỉnh vách giao trần giữa phòng sạch: tổng {totalBasis:F2} md, quy đổi 2 tuyến V bo R40 = {totalAuto:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao trần giữa phòng sạch: 2 tuyến silicone, tổng = {totalBasis * 2.0:F2} md, quy đổi = {totalAuto:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao trần giữa phòng sạch: rive theo 2 tuyến V bo R40 @500, tổng = {totalAuto:F2} cái";
                    return true;
                }
            }

            if (accessory.CalcRule == AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName)
                    || string.Equals(displayName, "V 40x80", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao biên trần phòng sạch: tổng chiều dài {displayName} = {totalAuto:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao biên trần phòng sạch: 2 tuyến silicone, tổng = {totalBasis * 2.0:F2} md, quy đổi = {totalAuto:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đỉnh vách giao biên trần phòng sạch: rive theo 2 tuyến V 40x80 + V bo R40 @500, tổng = {totalAuto:F2} cái";
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildCleanroomEndMetricNote(
            TenderAccessory accessory,
            TenderWall wall,
            double basisValue,
            double autoQuantity,
            out string? note)
        {
            note = null;

            if (!string.Equals(wall.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string displayName = GetAccessoryDisplayName(accessory, wall);
            if (accessory.CalcRule == AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName))
                {
                    note = $"đầu/cuối vách giao giữa phòng sạch: 2 tuyến V bo R40 hai bên vách × {basisValue:F2} md = {autoQuantity:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao giữa phòng sạch: 2 tuyến silicone hoàn thiện = {basisValue * 2.0:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao giữa phòng sạch: 2 tuyến rive theo 2 V bo R40 @500 = {autoQuantity:F2} cái";
                    return true;
                }
            }

            if (accessory.CalcRule == AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName))
                {
                    note = $"đầu/cuối vách giao biên phòng sạch: 1 tuyến V bo R40 phía trong, dài {basisValue:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "V 40x80", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao biên phòng sạch: 1 tuyến V 40x80 nhôm sơn tĩnh điện phía ngoài, dài {basisValue:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao biên phòng sạch: 2 tuyến silicone hoàn thiện = {basisValue * 2.0:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao biên phòng sạch: 2 tuyến rive theo V 40x80 + V bo R40 @500 = {autoQuantity:F2} cái";
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildCleanroomEndSummaryNote(
            TenderAccessory accessory,
            IReadOnlyList<TenderWall> applicableWalls,
            double totalBasis,
            double totalAuto,
            out string? note)
        {
            note = null;

            if (applicableWalls.Count == 0
                || applicableWalls.Any(w =>
                    !string.Equals(w.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(w.Category, "Vách", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string displayName = GetAccessoryDisplayName(accessory, applicableWalls[0]);
            if (accessory.CalcRule == AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName))
                {
                    note = $"đầu/cuối vách giao giữa phòng sạch: tổng {totalBasis:F2} md, quy đổi 2 tuyến V bo R40 = {totalAuto:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao giữa phòng sạch: 2 tuyến silicone, tổng = {totalBasis * 2.0:F2} md, quy đổi = {totalAuto:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao giữa phòng sạch: rive theo 2 tuyến V bo R40 @500, tổng = {totalAuto:F2} cái";
                    return true;
                }
            }

            if (accessory.CalcRule == AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName)
                    || string.Equals(displayName, "V 40x80", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao biên phòng sạch: tổng chiều dài {displayName} = {totalAuto:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao biên phòng sạch: 2 tuyến silicone, tổng = {totalBasis * 2.0:F2} md, quy đổi = {totalAuto:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao biên phòng sạch: rive theo 2 tuyến V 40x80 + V bo R40 @500, tổng = {totalAuto:F2} cái";
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildColdStorageEndMetricNote(
            TenderAccessory accessory,
            TenderWall wall,
            double basisValue,
            double autoQuantity,
            out string? note)
        {
            note = null;

            if (!string.Equals(wall.Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string displayName = GetAccessoryDisplayName(accessory, wall);
            if (accessory.CalcRule == AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName))
                {
                    note = $"đầu/cuối vách giao giữa: 2 tuyến V bo R40 hai bên panel × {basisValue:F2} md = {autoQuantity:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Foam PU bơm tại chỗ", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao giữa: 1 tuyến foam PU theo chiều cao mép xử lý {basisValue:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao giữa: 2 tuyến silicone hoàn thiện = {basisValue * 2.0:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }
            }

            if (accessory.CalcRule == AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName))
                {
                    note = $"đầu/cuối vách giao biên: 1 tuyến V bo R40 phía trong, dài {basisValue:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "V 80x80", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao biên: 1 tuyến V 80x80 phía ngoài, dài {basisValue:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Foam PU bơm tại chỗ", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao biên: 1 tuyến foam PU theo chiều cao mép xử lý {basisValue:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao biên: 2 tuyến silicone hoàn thiện = {basisValue * 2.0:F2} md, quy đổi = {autoQuantity:F2} chai";
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildColdStorageEndSummaryNote(
            TenderAccessory accessory,
            IReadOnlyList<TenderWall> applicableWalls,
            double totalBasis,
            double totalAuto,
            out string? note)
        {
            note = null;

            if (applicableWalls.Count == 0
                || applicableWalls.Any(w =>
                    !string.Equals(w.Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(w.Category, "Vách", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string displayName = GetAccessoryDisplayName(accessory, applicableWalls[0]);
            if (accessory.CalcRule == AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName))
                {
                    note = $"đầu/cuối vách giao giữa: tổng {totalBasis:F2} md, quy đổi 2 tuyến V bo R40 = {totalAuto:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Foam PU bơm tại chỗ", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao giữa: tổng {totalBasis:F2} md foam PU, quy đổi = {totalAuto:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao giữa: 2 tuyến silicone hoàn thiện, tổng = {totalBasis * 2.0:F2} md, quy đổi = {totalAuto:F2} chai";
                    return true;
                }
            }

            if (accessory.CalcRule == AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH)
            {
                if (IsVBoR40DisplayName(displayName)
                    || string.Equals(displayName, "V 80x80", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao biên: tổng chiều dài {displayName} = {totalAuto:F2} md";
                    return true;
                }

                if (string.Equals(displayName, "Foam PU bơm tại chỗ", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao biên: tổng {totalBasis:F2} md foam PU, quy đổi = {totalAuto:F2} chai";
                    return true;
                }

                if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
                {
                    note = $"đầu/cuối vách giao biên: 2 tuyến silicone hoàn thiện, tổng = {totalBasis * 2.0:F2} md, quy đổi = {totalAuto:F2} chai";
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildCleanroomBottomMetricNote(
            TenderAccessory accessory,
            TenderWall wall,
            double basisValue,
            double autoQuantity,
            out string? note)
        {
            note = null;

            if (accessory.CalcRule != AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH
                || !string.Equals(wall.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string displayName = GetAccessoryDisplayName(accessory, wall);
            if (IsVBoR40DisplayName(displayName))
            {
                note = $"chân vách phòng sạch: 2 tuyến V bo R40 hai bên vách × {basisValue:F2} md = {autoQuantity:F2} md";
                return true;
            }

            if (IsCleanroomBottomUChannelDisplayName(displayName))
            {
                string webThicknessText = wall.PanelThickness > 0
                    ? $", bụng U = {wall.PanelThickness} mm"
                    : string.Empty;
                note = $"chân vách phòng sạch: 1 tuyến {displayName} dài {basisValue:F2} md{webThicknessText}";
                return true;
            }

            if (string.Equals(displayName, "Vít No.5 + tắc kê nhựa", StringComparison.OrdinalIgnoreCase))
            {
                note = $"cố định U chân vách phòng sạch xuống sàn: {basisValue:F2} md @500 = {autoQuantity:F2} cái";
                return true;
            }

            if (string.Equals(displayName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
            {
                note = $"cố định 2 tuyến V bo R40 chân vách: {basisValue:F2} md × 2 tuyến @500 = {autoQuantity:F2} cái";
                return true;
            }

            if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
            {
                note = $"chân vách phòng sạch: 2 tuyến silicone hoàn thiện theo V bo R40 = {basisValue * 2.0:F2} md, quy đổi = {autoQuantity:F2} chai";
                return true;
            }

            return false;
        }

        private static bool TryBuildCleanroomBottomSummaryNote(
            TenderAccessory accessory,
            IReadOnlyList<TenderWall> applicableWalls,
            double totalBasis,
            double totalAuto,
            out string? note)
        {
            note = null;

            if (accessory.CalcRule != AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH
                || applicableWalls.Count == 0
                || applicableWalls.Any(w =>
                    !string.Equals(w.Application, "Phòng sạch", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(w.Category, "Vách", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string displayName = GetAccessoryDisplayName(accessory, applicableWalls[0]);
            if (IsVBoR40DisplayName(displayName))
            {
                note = $"tổng chân vách phòng sạch = {totalBasis:F2} md, quy đổi 2 tuyến V bo R40 = {totalAuto:F2} md";
                return true;
            }

            if (IsCleanroomBottomUChannelDisplayName(displayName))
            {
                note = $"tổng chiều dài {displayName} tại chân vách phòng sạch = {totalAuto:F2} md";
                return true;
            }

            if (string.Equals(displayName, "Vít No.5 + tắc kê nhựa", StringComparison.OrdinalIgnoreCase))
            {
                note = $"cố định U chân vách phòng sạch xuống sàn: tổng {totalBasis:F2} md @500 = {totalAuto:F2} cái";
                return true;
            }

            if (string.Equals(displayName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
            {
                note = $"rive chân vách phòng sạch: 2 tuyến theo V bo R40, tổng {totalBasis:F2} md @500 = {totalAuto:F2} cái";
                return true;
            }

            if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
            {
                note = $"silicone SN-505 chân vách phòng sạch: 2 tuyến theo V bo R40, tổng = {totalBasis * 2.0:F2} md, quy đổi = {totalAuto:F2} chai";
                return true;
            }

            return false;
        }

        private static bool IsCleanroomBottomUChannelDisplayName(string? displayName)
        {
            string normalized = (displayName ?? string.Empty).Trim();
            return normalized.StartsWith("U 40x", StringComparison.OrdinalIgnoreCase)
                && normalized.EndsWith("x40", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVBoR40DisplayName(string? displayName)
        {
            string normalized = (displayName ?? string.Empty).Trim();
            return string.Equals(normalized, "V bo R40", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Nẹp bo cong R40", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryBuildColdStorageBottomMetricNote(
            TenderAccessory accessory,
            TenderWall wall,
            double basisValue,
            double autoQuantity,
            out string? note)
        {
            note = null;

            if (accessory.CalcRule != AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH
                || !string.Equals(wall.Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(wall.Category, "Vách", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string displayName = GetAccessoryDisplayName(accessory, wall);
            if (string.Equals(displayName, "V bo R40", StringComparison.OrdinalIgnoreCase))
            {
                note = $"chân vách trên bệ chân (curb): 2 tuyến V bo R40 hai bên panel × {basisValue:F2} md = {autoQuantity:F2} md";
                return true;
            }

            if (IsColdStorageBottomUChannelDisplayName(displayName))
            {
                string webThicknessText = wall.PanelThickness > 0
                    ? $", bụng U = {wall.PanelThickness} + 3 = {wall.PanelThickness + 3} mm"
                    : string.Empty;
                note = $"chân vách trên bệ chân (curb): 1 tuyến {displayName} dài {basisValue:F2} md{webThicknessText}";
                return true;
            }

            if (string.Equals(displayName, "Vít No.5 + tắc kê nhựa", StringComparison.OrdinalIgnoreCase))
            {
                note = $"cố định U chân vách xuống bệ chân (curb): {basisValue:F2} md @500 = {autoQuantity:F2} cái";
                return true;
            }

            if (string.Equals(displayName, "Foam PU bơm tại chỗ", StringComparison.OrdinalIgnoreCase))
            {
                note = $"chân vách trên bệ chân (curb): 1 tuyến foam PU theo chiều dài {basisValue:F2} md, quy đổi = {autoQuantity:F2} chai";
                return true;
            }

            if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
            {
                double siliconeLength = basisValue * 2.0;
                note = $"chân vách trên bệ chân (curb): bắn theo 2 tuyến V bo R40 = {siliconeLength:F2} md, quy đổi = {autoQuantity:F2} chai";
                return true;
            }

            return false;
        }

        private static bool TryBuildColdStorageBottomSummaryNote(
            TenderAccessory accessory,
            IReadOnlyList<TenderWall> applicableWalls,
            double totalBasis,
            double totalAuto,
            out string? note)
        {
            note = null;

            if (accessory.CalcRule != AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH
                || applicableWalls.Count == 0
                || applicableWalls.Any(w =>
                    !string.Equals(w.Application, "Kho lạnh", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(w.Category, "Vách", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string displayName = GetAccessoryDisplayName(accessory, applicableWalls[0]);
            if (string.Equals(displayName, "V bo R40", StringComparison.OrdinalIgnoreCase))
            {
                note = $"tổng chân vách trên bệ chân (curb) = {totalBasis:F2} md, quy đổi 2 tuyến V bo R40 = {totalAuto:F2} md";
                return true;
            }

            if (IsColdStorageBottomUChannelDisplayName(displayName))
            {
                note = $"tổng chiều dài {displayName} tại chân vách trên bệ chân (curb) = {totalAuto:F2} md";
                return true;
            }

            if (string.Equals(displayName, "Vít No.5 + tắc kê nhựa", StringComparison.OrdinalIgnoreCase))
            {
                note = $"cố định U chân vách xuống bệ chân (curb): tổng {totalBasis:F2} md @500 = {totalAuto:F2} cái";
                return true;
            }

            if (string.Equals(displayName, "Foam PU bơm tại chỗ", StringComparison.OrdinalIgnoreCase))
            {
                note = $"foam PU chân vách trên bệ chân (curb): tổng {totalBasis:F2} md, quy đổi = {totalAuto:F2} chai";
                return true;
            }

            if (string.Equals(displayName, "Silicone SN-505", StringComparison.OrdinalIgnoreCase))
            {
                note = $"silicone SN-505 chân vách trên bệ chân (curb): 2 tuyến theo V bo R40, tổng = {totalBasis * 2.0:F2} md, quy đổi = {totalAuto:F2} chai";
                return true;
            }

            return false;
        }

        private static bool IsColdStorageBottomUChannelDisplayName(string? displayName)
        {
            string normalized = (displayName ?? string.Empty).Trim();
            return normalized.StartsWith("U 40x", StringComparison.OrdinalIgnoreCase)
                && normalized.EndsWith("x40", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPanelSupportBracketWallNote(
            TenderAccessory accessory,
            TenderWall wall,
            double basisValue,
            double autoQuantity)
        {
            if (string.Equals((accessory.Name ?? string.Empty).Trim(), "B2S-TEK 15-15×20 HWFS", StringComparison.OrdinalIgnoreCase))
                return $"{basisValue:F0} bát thép chân panel × 2 vít/bát = {autoQuantity:F0} cái";

            return $"dài vách {wall.Length / 1000.0:F2} m / nhịp xà gồ 1.50 m = {basisValue:F0} cái";
        }

        private static string BuildPanelSupportBracketSummaryNote(
            TenderAccessory accessory,
            double totalBasis,
            double totalAuto)
        {
            if (string.Equals((accessory.Name ?? string.Empty).Trim(), "B2S-TEK 15-15×20 HWFS", StringComparison.OrdinalIgnoreCase))
                return $"tổng vít bắt bát chân panel = {totalBasis:F0} bát × 2 = {totalAuto:F0} cái";

            return $"tổng bát thép chân panel = {totalAuto:F0} cái";
        }

        private static string BuildSuspensionWallNote(TenderWall wall, double basisValue, bool mushroom, string metricLabel)
        {
            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null)
                return $"{metricLabel} = {basisValue:F2} md";

            string direction = string.IsNullOrWhiteSpace(wall.SuspensionLayoutDirection)
                ? "chưa chọn hướng phụ kiện"
                : $"hướng phụ kiện {wall.SuspensionLayoutDirection}";
            string origin = wall.ColdStorageDivideFromMaxSide ? "chia từ cạnh dài" : "chia từ cạnh ngắn";

            if (mushroom)
            {
                return $"{direction}, {origin}, offset bu lông nấm {spec.MushroomOffsetMm:F0} mm, {GetColdStorageMushroomSuspensionLineCount(wall)} tuyến, {metricLabel} = {basisValue:F2} md";
            }

            return $"{direction}, {origin}, bước thanh T {spec.TSpacingMm:F0} mm, {GetColdStorageTSuspensionLineCount(wall)} tuyến, {metricLabel} = {basisValue:F2} md";
        }

        private static string BuildSuspensionLengthWallNote(
            TenderAccessory accessory,
            TenderWall wall,
            double basisValue,
            double autoQuantity,
            bool mushroom,
            string metricLabel)
        {
            string note = BuildSuspensionWallNote(wall, basisValue, mushroom, metricLabel);
            if (string.Equals(accessory.Unit, "cây", StringComparison.OrdinalIgnoreCase))
                return $"{note}, quy đổi = {autoQuantity:F2} cây";

            return note;
        }

        private static string BuildSuspensionPointWallNote(TenderWall wall, bool mushroom)
        {
            var spec = ResolveColdStorageSuspensionSpec(wall.PanelThickness);
            if (spec == null)
                return $"số điểm treo = 0";

            string direction = string.IsNullOrWhiteSpace(wall.SuspensionLayoutDirection)
                ? "chưa chọn hướng phụ kiện"
                : $"hướng phụ kiện {wall.SuspensionLayoutDirection}";
            string origin = wall.ColdStorageDivideFromMaxSide ? "chia từ cạnh dài" : "chia từ cạnh ngắn";

            if (mushroom)
            {
                return $"{direction}, {origin}, offset {spec.MushroomOffsetMm:F0} mm, {GetColdStorageMushroomSuspensionLineCount(wall)} tuyến, bước điểm 1450 mm = {GetColdStorageMushroomSuspensionPointQty(wall):F0} điểm";
            }

                return $"{direction}, {origin}, bước thanh T {spec.TSpacingMm:F0} mm, {GetColdStorageTSuspensionLineCount(wall)} tuyến, bước điểm 1450 mm = {GetColdStorageTSuspensionPointQty(wall):F0} điểm";
        }

        private static string BuildSuspensionPointAccessoryWallNote(
            TenderAccessory accessory,
            TenderWall wall,
            double basisValue,
            double autoQuantity,
            bool mushroom)
        {
            string note = BuildSuspensionPointWallNote(wall, mushroom);
            if (TryBuildSuspensionPointStockConversionNote(accessory, basisValue, autoQuantity, mushroom, out string? stockNote))
                return $"{note}, {stockNote}";

            return note;
        }

        private static string BuildWireRopeWallNote(TenderWall wall, double basisValue, bool mushroom)
        {
            double pointQty = mushroom
                ? GetColdStorageMushroomSuspensionPointQty(wall)
                : GetColdStorageTSuspensionPointQty(wall);
            string systemLabel = mushroom ? "bu lông nấm" : "thanh T";
            return $"{pointQty:F0} điểm treo {systemLabel} × thả cáp {wall.CableDropLengthMm:F0} mm = {basisValue:F2} md";
        }

        private static string BuildSuspensionLengthSummaryNote(
            TenderAccessory accessory,
            IReadOnlyList<TenderWall> applicableWalls,
            double totalBasis,
            double totalAuto,
            bool mushroom,
            string systemLabel)
        {
            int lineCount = mushroom
                ? applicableWalls.Sum(GetColdStorageMushroomSuspensionLineCount)
                : applicableWalls.Sum(GetColdStorageTSuspensionLineCount);

            string note = $"tổng {lineCount} tuyến {systemLabel}, chiều dài {totalBasis:F2} md";
            if (string.Equals(accessory.Unit, "cây", StringComparison.OrdinalIgnoreCase))
                return $"{note}, quy đổi = {totalAuto:F2} cây";

            return note;
        }

        private static string BuildSuspensionPointSummaryNote(
            TenderAccessory accessory,
            double totalBasis,
            double totalAuto,
            bool mushroom)
        {
            string systemLabel = mushroom ? "bu lông nấm" : "thanh T";
            string note = $"tổng {totalBasis:F0} điểm treo {systemLabel}, bước điểm 1450 mm";

            if (TryBuildSuspensionPointStockConversionNote(accessory, totalBasis, totalAuto, mushroom, out string? stockNote))
                return $"{note}, {stockNote}";

            return note;
        }

        private static bool IsPipeStockAccessory(TenderAccessory accessory)
        {
            return string.Equals(NormalizeAccessoryGroupingName(accessory.Name), "Ống Ø114", StringComparison.OrdinalIgnoreCase)
                && string.Equals(NormalizeAccessoryGroupingUnit(accessory.Unit), "cây", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsThreadedRodStockAccessory(TenderAccessory accessory)
        {
            return string.Equals(NormalizeAccessoryGroupingName(accessory.Name), "Ty ren M12", StringComparison.OrdinalIgnoreCase)
                && string.Equals(NormalizeAccessoryGroupingUnit(accessory.Unit), "cây", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryBuildSuspensionPointStockConversionNote(
            TenderAccessory accessory,
            double pointQty,
            double autoQuantity,
            bool mushroom,
            out string? note)
        {
            note = null;

            if (IsPipeStockAccessory(accessory))
            {
                double pipeLengthPerPointMm = mushroom ? 200.0 : 500.0;
                double totalPipeLengthMd = pointQty * pipeLengthPerPointMm / 1000.0;
                note = $"quy đổi {pipeLengthPerPointMm:F0} mm/điểm = {totalPipeLengthMd:F2} md ≈ {autoQuantity:F2} cây (1 cây = 4m)";
                return true;
            }

            if (mushroom && IsThreadedRodStockAccessory(accessory))
            {
                const double threadedRodLengthPerPointMm = 350.0;
                const double threadedRodStockLengthM = 3.0;
                double totalThreadedRodLengthMd = pointQty * threadedRodLengthPerPointMm / 1000.0;
                note = $"quy đổi {threadedRodLengthPerPointMm:F0} mm/điểm = {totalThreadedRodLengthMd:F2} md ≈ {autoQuantity:F2} cây (1 cây = {threadedRodStockLengthM:F0}m)";
                return true;
            }

            return false;
        }

        private static string NormalizeAccessoryGroupingName(string? name)
        {
            string normalized = (name ?? string.Empty).Trim();
            if (string.Equals(normalized, "Nẹp bo cong R40", StringComparison.OrdinalIgnoreCase))
                return "V bo R40";
            if (string.Equals(normalized, "Rivet inox Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                return "Rive Ø4.2×12";
            if (string.Equals(normalized, "Rive inox Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                return "Rive Ø4.2×12";
            if (string.Equals(normalized, "Rivet Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                return "Rive Ø4.2×12";
            if (string.Equals(normalized, "B2S-TEK inox", StringComparison.OrdinalIgnoreCase))
                return "B2S-TEK";
            if (string.Equals(normalized, "Ống PVC Ø114", StringComparison.OrdinalIgnoreCase))
                return "Ống Ø114";
            if (string.Equals(normalized, "Ống phi 114", StringComparison.OrdinalIgnoreCase))
                return "Ống Ø114";
            if (string.Equals(normalized, "Ống PVC Ø114", StringComparison.OrdinalIgnoreCase))
                return "Ống Ø114";
            if (string.Equals(normalized, "Ống phi 114", StringComparison.OrdinalIgnoreCase))
                return "Ống Ø114";

            return normalized;
        }

        private static string NormalizeAccessoryGroupingMaterial(string? name, string? material)
        {
            string normalizedName = NormalizeAccessoryGroupingName(name);
            string normalizedMaterial = (material ?? string.Empty).Trim();
            if (string.Equals(normalizedName, "Ống Ø114", StringComparison.OrdinalIgnoreCase))
                return "PVC";
            if (string.Equals(normalizedName, "Rive Ø4.2×12", StringComparison.OrdinalIgnoreCase))
                return "Nhôm";
            if (string.Equals(normalizedName, "B2S-TEK", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(normalizedMaterial))
                return "Inox";

            if (string.Equals(normalizedName, "Ống Ø114", StringComparison.OrdinalIgnoreCase))
                return "PVC";

            return normalizedMaterial;
        }

        private static string NormalizeAccessoryGroupingUnit(string? unit)
        {
            string normalized = (unit ?? string.Empty).Trim();
            if (string.Equals(normalized, "thanh", StringComparison.OrdinalIgnoreCase))
                return "cây";

            return normalized;
        }

        private static string BuildSummaryDisplayValue(IEnumerable<string> values)
        {
            var items = values
                .Select(TenderAccessoryRules.NormalizeScope)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (items.Count == 0)
                return string.Empty;

            if (items.Count <= 3)
                return string.Join(" + ", items);

            return string.Join(" + ", items.Take(3)) + $" +{items.Count - 3}";
        }

        private static string BuildWallScopeText(IReadOnlyList<TenderWall> walls)
        {
            if (walls.Count == 0)
                return string.Empty;

            var names = walls
                .Select(w => w.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
                return $"{walls.Count} vùng";

            if (names.Count <= 3)
                return $"vùng {string.Join(", ", names)}";

            return $"vùng {string.Join(", ", names.Take(3))} +{names.Count - 3} vùng";
        }

        private static string GetCommonValueOrDefault(IEnumerable<string> values, string defaultValue)
        {
            var items = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return items.Count == 1 ? items[0] : defaultValue;
        }

        private static string JoinDistinct(IEnumerable<string> values, string separator)
        {
            return string.Join(
                separator,
                values
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string NormalizeNoteText(string? note)
        {
            if (string.IsNullOrWhiteSpace(note))
                return string.Empty;

            return note.Trim().TrimEnd('.', ';');
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
            int result = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            result = string.Compare(left.Material, right.Material, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            result = string.Compare(left.Unit, right.Unit, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            result = TenderAccessoryRules.CompareApplications(left.Application, right.Application);
            if (result != 0) return result;

            result = TenderAccessoryRules.CompareScopes(left.CategoryScope, right.CategoryScope);
            if (result != 0) return result;

            result = TenderAccessoryRules.CompareScopes(left.SpecKey, right.SpecKey);
            if (result != 0) return result;

            result = string.Compare(left.Position, right.Position, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;

            return string.Compare(left.RuleLabel, right.RuleLabel, StringComparison.OrdinalIgnoreCase);
        }
    }
}
