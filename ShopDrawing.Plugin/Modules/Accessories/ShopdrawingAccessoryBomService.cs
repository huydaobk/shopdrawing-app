using System;
using System.Collections.Generic;
using System.Linq;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Modules.Accessories
{
    internal sealed class ShopdrawingAccessoryBomService
    {
        private const string CeilingCategory = "Tr\u1ea7n";
        private const string WallCategory = "V\u00e1ch";

        public IReadOnlyList<ShopdrawingAccessorySummaryRow> BuildCeilingSummary(ShopdrawingAccessorySnapshot snapshot)
        {
            var rows = new List<ShopdrawingAccessorySummaryRow>();
            foreach (TenderAccessory accessory in AccessoryDataManager.GetDefaults())
            {
                if (!IsMatchingScope(accessory.CategoryScope, CeilingCategory)
                    || !IsMatchingScope(accessory.Application, snapshot.Application)
                    || !IsMatchingScope(accessory.SpecKey, snapshot.SpecKey))
                {
                    continue;
                }

                if (!TryGetCeilingBasisValue(snapshot, accessory.CalcRule, out double basisValue) || basisValue <= 0)
                {
                    continue;
                }

                rows.Add(new ShopdrawingAccessorySummaryRow(
                    accessory.CategoryScope,
                    accessory.Application,
                    accessory.SpecKey,
                    accessory.Name,
                    accessory.Material,
                    accessory.Position,
                    accessory.Unit,
                    accessory.CalcRule,
                    basisValue,
                    accessory.Factor,
                    basisValue * accessory.Factor,
                    accessory.Note));
            }

            return AggregateRows(rows);
        }

        public IReadOnlyList<ShopdrawingAccessorySummaryRow> BuildWallSummary(IReadOnlyList<ShopdrawingWallAccessorySnapshot> snapshots)
        {
            var rows = new List<ShopdrawingAccessorySummaryRow>();
            foreach (ShopdrawingWallAccessorySnapshot snapshot in snapshots)
            {
                foreach (TenderAccessory accessory in AccessoryDataManager.GetDefaults())
                {
                    if (!IsMatchingScope(accessory.CategoryScope, WallCategory)
                        || !IsMatchingScope(accessory.Application, snapshot.Application)
                        || !IsMatchingScope(accessory.SpecKey, snapshot.SpecKey))
                    {
                        continue;
                    }

                    if (!TryGetWallBasisValue(snapshot, accessory.CalcRule, out double basisValue) || basisValue <= 0)
                    {
                        continue;
                    }

                    rows.Add(new ShopdrawingAccessorySummaryRow(
                        accessory.CategoryScope,
                        accessory.Application,
                        snapshot.SpecKey,
                        accessory.Name,
                        accessory.Material,
                        accessory.Position,
                        accessory.Unit,
                        accessory.CalcRule,
                        basisValue,
                        accessory.Factor,
                        basisValue * accessory.Factor,
                        $"{snapshot.WallCode}: {accessory.Note}"));
                }
            }

            return AggregateRows(rows);
        }

        public IReadOnlyList<ShopdrawingAccessorySummaryRow> BuildPlanCornerSummary(IReadOnlyList<ShopdrawingPlanAccessorySnapshot> snapshots)
        {
            var rows = new List<ShopdrawingAccessorySummaryRow>();
            foreach (ShopdrawingPlanAccessorySnapshot snapshot in snapshots)
            {
                foreach (TenderAccessory accessory in AccessoryDataManager.GetDefaults())
                {
                    if (!IsMatchingScope(accessory.CategoryScope, WallCategory)
                        || !IsMatchingScope(accessory.Application, snapshot.Application)
                        || !IsMatchingScope(accessory.SpecKey, snapshot.SpecKey))
                    {
                        continue;
                    }

                    if (!TryGetPlanCornerBasisValue(snapshot, accessory.CalcRule, out double basisValue) || basisValue <= 0)
                    {
                        continue;
                    }

                    rows.Add(new ShopdrawingAccessorySummaryRow(
                        accessory.CategoryScope,
                        accessory.Application,
                        snapshot.SpecKey,
                        accessory.Name,
                        accessory.Material,
                        accessory.Position,
                        accessory.Unit,
                        accessory.CalcRule,
                        basisValue,
                        accessory.Factor,
                        basisValue * accessory.Factor,
                        $"PLAN: {accessory.Note}"));
                }
            }

            return AggregateRows(rows);
        }

        private static bool TryGetCeilingBasisValue(
            ShopdrawingAccessorySnapshot snapshot,
            AccessoryCalcRule rule,
            out double basisValue)
        {
            basisValue = rule switch
            {
                AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH => snapshot.CeilingTLineLengthM,
                AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY => snapshot.CeilingTHangerPointCount,
                AccessoryCalcRule.PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH => snapshot.CeilingTHangerPointCount * snapshot.CeilingCableDropM,
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH => snapshot.CeilingMushroomLineLengthM,
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY => snapshot.CeilingMushroomHangerPointCount,
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH => snapshot.CeilingMushroomHangerPointCount * snapshot.CeilingCableDropM,
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_BOLT_QTY => snapshot.CeilingMushroomBoltCount,
                _ => 0
            };

            return basisValue > 0;
        }

        private static bool TryGetWallBasisValue(
            ShopdrawingWallAccessorySnapshot snapshot,
            AccessoryCalcRule rule,
            out double basisValue)
        {
            basisValue = rule switch
            {
                AccessoryCalcRule.PER_WALL_LENGTH => snapshot.WallLengthM,
                AccessoryCalcRule.PER_WALL_HEIGHT => snapshot.WallHeightM,
                AccessoryCalcRule.PER_TOP_EDGE_LENGTH => snapshot.TopEdgeLengthM,
                AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH => snapshot.TopCenterLengthM,
                AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH => snapshot.TopPerimeterLengthM,
                AccessoryCalcRule.PER_TOP_PANEL_FREE_LENGTH => snapshot.TopFreeLengthM,
                AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH => snapshot.BottomEdgeLengthM,
                AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH => snapshot.EndCenterLengthM,
                AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH => snapshot.EndPerimeterLengthM,
                AccessoryCalcRule.PER_END_PANEL_FREE_LENGTH => snapshot.EndFreeLengthM,
                AccessoryCalcRule.PER_EXPOSED_END_LENGTH => snapshot.ExposedEndLengthM,
                AccessoryCalcRule.PER_TOTAL_EXPOSED_EDGE_LENGTH => snapshot.TotalExposedEdgeLengthM,
                AccessoryCalcRule.PER_PANEL_QTY => snapshot.PanelCount,
                AccessoryCalcRule.PER_JOINT_LENGTH => snapshot.JointLengthM,
                AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT => snapshot.VerticalJointLengthM,
                AccessoryCalcRule.PER_OPENING_PERIMETER => snapshot.OpeningPerimeterM,
                AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES => snapshot.OpeningPerimeterTwoFacesM,
                AccessoryCalcRule.PER_OPENING_QTY => snapshot.OpeningCount,
                AccessoryCalcRule.PER_DOOR_OPENING_PERIMETER => snapshot.DoorOpeningPerimeterM,
                AccessoryCalcRule.PER_NON_DOOR_OPENING_PERIMETER => snapshot.NonDoorOpeningPerimeterM,
                AccessoryCalcRule.PER_DOOR_OPENING_QTY => snapshot.DoorOpeningCount,
                AccessoryCalcRule.PER_NON_DOOR_OPENING_QTY => snapshot.NonDoorOpeningCount,
                AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES => snapshot.OpeningVerticalEdgesM,
                AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH => snapshot.OpeningTopLengthM,
                AccessoryCalcRule.PER_OPENING_SILL_LENGTH => snapshot.OpeningSillLengthM,
                AccessoryCalcRule.PER_NET_AREA => snapshot.NetAreaM2,
                AccessoryCalcRule.FIXED_PER_WALL => 1.0,
                AccessoryCalcRule.PER_TEK_SCREW_QTY => snapshot.TekScrewQty,
                AccessoryCalcRule.PER_PANEL_SUPPORT_BRACKET_QTY => snapshot.PanelSupportBracketQty,
                _ => 0
            };

            return basisValue > 0;
        }

        private static bool TryGetPlanCornerBasisValue(
            ShopdrawingPlanAccessorySnapshot snapshot,
            AccessoryCalcRule rule,
            out double basisValue)
        {
            basisValue = rule switch
            {
                AccessoryCalcRule.PER_OUTSIDE_CORNER_HEIGHT => snapshot.OutsideCornerHeightM,
                AccessoryCalcRule.PER_INSIDE_CORNER_HEIGHT => snapshot.InsideCornerHeightM,
                _ => 0
            };

            return basisValue > 0;
        }

        private static IReadOnlyList<ShopdrawingAccessorySummaryRow> AggregateRows(IEnumerable<ShopdrawingAccessorySummaryRow> sourceRows)
        {
            return sourceRows
                .GroupBy(
                    row => new
                    {
                        row.CategoryScope,
                        row.Application,
                        row.SpecKey,
                        row.Name,
                        row.Material,
                        row.Position,
                        row.Unit,
                        row.Rule,
                        row.Factor
                    })
                .Select(group => new ShopdrawingAccessorySummaryRow(
                    group.Key.CategoryScope,
                    group.Key.Application,
                    group.Key.SpecKey,
                    group.Key.Name,
                    group.Key.Material,
                    group.Key.Position,
                    group.Key.Unit,
                    group.Key.Rule,
                    group.Sum(x => x.BasisValue),
                    group.Key.Factor,
                    group.Sum(x => x.Quantity),
                    string.Join(" | ", group.Select(x => x.Note).Distinct(StringComparer.OrdinalIgnoreCase))))
                .OrderBy(row => row.Application, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Position, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsMatchingScope(string configuredValue, string currentValue)
        {
            string normalizedConfigured = TenderAccessoryRules.NormalizeScope(configuredValue);
            string normalizedCurrent = TenderAccessoryRules.NormalizeScope(currentValue);
            return TenderAccessoryRules.IsAllScope(normalizedConfigured)
                || string.Equals(normalizedConfigured, normalizedCurrent, StringComparison.OrdinalIgnoreCase);
        }
    }
}
