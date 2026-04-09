using System;
using System.Collections.Generic;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    public static class TenderAccessoryRules
    {
        public sealed class RuleOption
        {
            public RuleOption(AccessoryCalcRule value, string label)
            {
                Value = value;
                Label = label;
            }

            public AccessoryCalcRule Value { get; }
            public string Label { get; }
        }

        private static readonly IReadOnlyList<RuleOption> Options = new List<RuleOption>
        {
            new(AccessoryCalcRule.PER_WALL_LENGTH, "Theo chi\u1ec1u d\u00e0i v\u00e1ch"),
            new(AccessoryCalcRule.PER_WALL_HEIGHT, "Theo chi\u1ec1u cao v\u00e1ch"),
            new(AccessoryCalcRule.PER_TOP_EDGE_LENGTH, "Theo đỉnh vách"),
            new(AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH, "Theo đỉnh vách giao trần giữa"),
            new(AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH, "Theo đỉnh vách giao biên trần"),
            new(AccessoryCalcRule.PER_TOP_PANEL_FREE_LENGTH, "Theo đỉnh vách mép tự do"),
            new(AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH, "Theo chân vách"),
            new(AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH, "Theo đầu/cuối vách giao giữa"),
            new(AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH, "Theo đầu/cuối vách giao biên"),
            new(AccessoryCalcRule.PER_END_PANEL_FREE_LENGTH, "Theo đầu/cuối vách mép đứng tự do"),
            new(AccessoryCalcRule.PER_EXPOSED_END_LENGTH, "Theo tổng chiều cao mép đứng xử lý"),
            new(AccessoryCalcRule.PER_TOTAL_EXPOSED_EDGE_LENGTH, "Theo t\u1ed5ng c\u1ea1nh l\u1ed9 thi\u00ean"),
            new(AccessoryCalcRule.PER_OUTSIDE_CORNER_HEIGHT, "Theo chi\u1ec1u cao g\u00f3c ngo\u00e0i"),
            new(AccessoryCalcRule.PER_INSIDE_CORNER_HEIGHT, "Theo chi\u1ec1u cao g\u00f3c trong"),
            new(AccessoryCalcRule.PER_PANEL_QTY, "Theo s\u1ed1 t\u1ea5m"),
            new(AccessoryCalcRule.PER_JOINT_LENGTH, "Theo chi\u1ec1u d\u00e0i m\u1ed1i n\u1ed1i"),
            new(AccessoryCalcRule.PER_OPENING_PERIMETER, "Theo chu vi l\u1ed7 m\u1edf"),
            new(AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES, "Theo chu vi l\u1ed7 m\u1edf 2 m\u1eb7t"),
            new(AccessoryCalcRule.PER_DOOR_OPENING_PERIMETER, "Theo chu vi c\u1eeda \u0111i"),
            new(AccessoryCalcRule.PER_NON_DOOR_OPENING_PERIMETER, "Theo chu vi c\u1eeda s\u1ed5/l\u1ed7 k\u1ef9 thu\u1eadt"),
            new(AccessoryCalcRule.PER_OPENING_QTY, "Theo s\u1ed1 l\u01b0\u1ee3ng l\u1ed7 m\u1edf"),
            new(AccessoryCalcRule.PER_DOOR_OPENING_QTY, "Theo s\u1ed1 l\u01b0\u1ee3ng c\u1eeda \u0111i"),
            new(AccessoryCalcRule.PER_NON_DOOR_OPENING_QTY, "Theo s\u1ed1 l\u01b0\u1ee3ng c\u1eeda s\u1ed5/l\u1ed7 k\u1ef9 thu\u1eadt"),
            new(AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES, "Theo c\u1ea1nh \u0111\u1ee9ng l\u1ed7 m\u1edf"),
            new(AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH, "Theo c\u1ea1nh \u0111\u1ea7u l\u1ed7 m\u1edf"),
            new(AccessoryCalcRule.PER_OPENING_SILL_LENGTH, "Theo c\u1ea1nh sill l\u1ed7 m\u1edf"),
            new(AccessoryCalcRule.PER_NET_AREA, "Theo di\u1ec7n t\u00edch net"),
            new(AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH, "Theo chiều dài tuyến treo thanh T trần"),
            new(AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY, "Theo số điểm treo thanh T trần"),
            new(AccessoryCalcRule.PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH, "Theo chiều dài cáp treo thanh T trần"),
            new(AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH, "Theo chiều dài tuyến treo bu lông nấm/thanh C trần"),
            new(AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY, "Theo số điểm treo bu lông nấm/thanh C trần"),
            new(AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH, "Theo chiều dài cáp treo bu lông nấm/thanh C trần"),
            new(AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_BOLT_QTY, "Theo số lượng bu lông nấm trần"),
            new(AccessoryCalcRule.FIXED_PER_WALL, "C\u1ed1 \u0111\u1ecbnh theo t\u1eebng v\u00e1ch"),
            new(AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT, "Theo khe n\u1ed1i \u0111\u1ee9ng \u00d7 chi\u1ec1u cao (ch\u1ec9 x\u1ebfp Ngang)"),
            new(AccessoryCalcRule.PER_TEK_SCREW_QTY, "S\u1ed1 v\u00edt B2S-TEK (t\u1ef1 \u0111\u1ed9ng theo nh\u1ecbp x\u00e0 g\u1ed3 1500mm)"),
            new(AccessoryCalcRule.PER_PANEL_SUPPORT_BRACKET_QTY, "S\u1ed1 b\u00e1t th\u00e9p ch\u00e2n panel (chi\u1ec1u d\u00e0i / nh\u1ecbp x\u00e0 g\u1ed3 1500mm)")
        };

        public static IReadOnlyList<RuleOption> GetRuleOptions() => Options;

        public static string GetRuleLabel(AccessoryCalcRule rule)
        {
            foreach (var option in Options)
            {
                if (option.Value == rule)
                    return option.Label;
            }

            return rule.ToString();
        }

        public static string GetBasisLabel(AccessoryCalcRule rule)
        {
            return rule switch
            {
                AccessoryCalcRule.PER_WALL_LENGTH => "Chi\u1ec1u d\u00e0i v\u00e1ch",
                AccessoryCalcRule.PER_WALL_HEIGHT => "Chi\u1ec1u cao v\u00e1ch",
                AccessoryCalcRule.PER_TOP_EDGE_LENGTH => "Đỉnh vách",
                AccessoryCalcRule.PER_TOP_PANEL_CEILING_CENTER_LENGTH => "Đỉnh vách giao trần giữa",
                AccessoryCalcRule.PER_TOP_PANEL_CEILING_PERIMETER_LENGTH => "Đỉnh vách giao biên trần",
                AccessoryCalcRule.PER_TOP_PANEL_FREE_LENGTH => "Đỉnh vách mép tự do",
                AccessoryCalcRule.PER_BOTTOM_EDGE_LENGTH => "Chân vách",
                AccessoryCalcRule.PER_END_PANEL_CENTER_LENGTH => "Đầu/cuối vách giao giữa",
                AccessoryCalcRule.PER_END_PANEL_PERIMETER_LENGTH => "Đầu/cuối vách giao biên",
                AccessoryCalcRule.PER_END_PANEL_FREE_LENGTH => "Đầu/cuối vách mép đứng tự do",
                AccessoryCalcRule.PER_EXPOSED_END_LENGTH => "Tổng chiều cao mép đứng xử lý",
                AccessoryCalcRule.PER_TOTAL_EXPOSED_EDGE_LENGTH => "T\u1ed5ng c\u1ea1nh l\u1ed9 thi\u00ean",
                AccessoryCalcRule.PER_OUTSIDE_CORNER_HEIGHT => "Chi\u1ec1u cao g\u00f3c ngo\u00e0i",
                AccessoryCalcRule.PER_INSIDE_CORNER_HEIGHT => "Chi\u1ec1u cao g\u00f3c trong",
                AccessoryCalcRule.PER_PANEL_QTY => "S\u1ed1 t\u1ea5m \u01b0\u1edbc t\u00ednh",
                AccessoryCalcRule.PER_JOINT_LENGTH => "Chi\u1ec1u d\u00e0i m\u1ed1i n\u1ed1i",
                AccessoryCalcRule.PER_OPENING_PERIMETER => "Chu vi l\u1ed7 m\u1edf",
                AccessoryCalcRule.PER_OPENING_PERIMETER_TWO_FACES => "Chu vi l\u1ed7 m\u1edf 2 m\u1eb7t",
                AccessoryCalcRule.PER_DOOR_OPENING_PERIMETER => "Chu vi c\u1eeda \u0111i",
                AccessoryCalcRule.PER_NON_DOOR_OPENING_PERIMETER => "Chu vi c\u1eeda s\u1ed5/l\u1ed7 k\u1ef9 thu\u1eadt",
                AccessoryCalcRule.PER_OPENING_QTY => "S\u1ed1 l\u01b0\u1ee3ng l\u1ed7 m\u1edf",
                AccessoryCalcRule.PER_DOOR_OPENING_QTY => "S\u1ed1 l\u01b0\u1ee3ng c\u1eeda \u0111i",
                AccessoryCalcRule.PER_NON_DOOR_OPENING_QTY => "S\u1ed1 l\u01b0\u1ee3ng c\u1eeda s\u1ed5/l\u1ed7 k\u1ef9 thu\u1eadt",
                AccessoryCalcRule.PER_OPENING_VERTICAL_EDGES => "C\u1ea1nh \u0111\u1ee9ng l\u1ed7 m\u1edf",
                AccessoryCalcRule.PER_OPENING_HORIZONTAL_TOP_LENGTH => "C\u1ea1nh \u0111\u1ea7u l\u1ed7 m\u1edf",
                AccessoryCalcRule.PER_OPENING_SILL_LENGTH => "C\u1ea1nh sill l\u1ed7 m\u1edf",
                AccessoryCalcRule.PER_NET_AREA => "Di\u1ec7n t\u00edch net",
                AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_LENGTH => "Chiều dài tuyến treo thanh T",
                AccessoryCalcRule.PER_COLD_STORAGE_T_SUSPENSION_POINT_QTY => "Số điểm treo thanh T",
                AccessoryCalcRule.PER_COLD_STORAGE_T_WIRE_ROPE_LENGTH => "Chiều dài cáp treo thanh T",
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_LENGTH => "Chiều dài tuyến treo thanh C",
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_SUSPENSION_POINT_QTY => "Số điểm treo thanh C",
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_WIRE_ROPE_LENGTH => "Chiều dài cáp treo thanh C",
                AccessoryCalcRule.PER_COLD_STORAGE_MUSHROOM_BOLT_QTY => "Số lượng bu lông nấm",
                AccessoryCalcRule.FIXED_PER_WALL => "S\u1ed1 v\u00e1ch",
                AccessoryCalcRule.PER_VERTICAL_JOINT_HEIGHT => "S\u1ed1 khe \u00d7 Cao v\u00e1ch",
                AccessoryCalcRule.PER_TEK_SCREW_QTY => "V\u00edt TEK (PanelCount\u00d7Nh\u1ecbp\u00d72)",
                AccessoryCalcRule.PER_PANEL_SUPPORT_BRACKET_QTY => "B\u00e1t th\u00e9p ch\u00e2n panel",
                _ => "C\u01a1 s\u1edf t\u00ednh"
            };
        }

        public static string NormalizeScope(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "T\u1ea5t c\u1ea3" : value.Trim();
        }

        public static bool IsAllScope(string? value)
        {
            string normalized = NormalizeScope(value);
            return normalized.Equals("T\u1ea5t c\u1ea3", StringComparison.OrdinalIgnoreCase);
        }

        public static int CompareApplications(string? left, string? right)
        {
            int leftRank = GetApplicationRank(left);
            int rightRank = GetApplicationRank(right);
            int result = leftRank.CompareTo(rightRank);
            if (result != 0)
                return result;

            return string.Compare(NormalizeScope(left), NormalizeScope(right), StringComparison.OrdinalIgnoreCase);
        }

        public static int CompareScopes(string? left, string? right)
        {
            int leftRank = IsAllScope(left) ? 99 : 0;
            int rightRank = IsAllScope(right) ? 99 : 0;
            int result = leftRank.CompareTo(rightRank);
            if (result != 0)
                return result;

            return string.Compare(NormalizeScope(left), NormalizeScope(right), StringComparison.OrdinalIgnoreCase);
        }

        private static int GetApplicationRank(string? value)
        {
            string normalized = NormalizeScope(value);
            if (normalized.Equals("Ngo\u00e0i nh\u00e0", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (normalized.Equals("Ph\u00f2ng s\u1ea1ch", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (normalized.Equals("Kho l\u1ea1nh", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (normalized.Equals("T\u1ea5t c\u1ea3", StringComparison.OrdinalIgnoreCase))
                return 99;
            return 50;
        }
    }
}
