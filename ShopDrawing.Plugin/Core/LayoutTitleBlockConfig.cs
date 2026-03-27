using System;
using System.Collections.Generic;
using System.Linq;

namespace ShopDrawing.Plugin.Core
{
    public enum LayoutTitleBlockMode
    {
        BuiltIn,
        External
    }

    /// <summary>
    /// Where the info strip sits within a full-frame title block.
    /// Bottom = horizontal strip at page bottom (subtract from height).
    /// Right = vertical strip at page right (subtract from width).
    /// </summary>
    public enum TitleBlockStripPosition
    {
        Bottom,
        Right
    }

    /// <summary>
    /// Maps an attribute tag (from the DWG block) to a plugin field name (e.g., "ProjectName").
    /// Uses init properties (not positional constructor) so System.Text.Json can deserialize it without extra attributes.
    /// </summary>
    public sealed record LayoutTitleBlockAttributeMapping
    {
        public string AttributeTag { get; init; } = string.Empty;
        public string PluginField { get; init; } = string.Empty;

        // Convenience constructor for in-code usage
        public LayoutTitleBlockAttributeMapping() { }
        public LayoutTitleBlockAttributeMapping(string attributeTag, string pluginField)
        {
            AttributeTag = attributeTag;
            PluginField = pluginField;
        }
    }

    public sealed record LayoutTitleBlockSource
    {
        public string Name { get; init; } = "";
        public List<string> AttributeTags { get; init; } = [];
        public double Width { get; init; }
        public double Height { get; init; }
        public TitleBlockStripPosition StripPosition { get; init; } = TitleBlockStripPosition.Bottom;
        public double StripSize { get; init; }

        public string DisplayName =>
            Name == LayoutTitleBlockConfig.ModelSpaceSourceName ? "(Model Space)" : Name;
    }

    public sealed record LayoutTitleBlockConfig
    {
        public const string ModelSpaceSourceName = "__MODELSPACE__";

        public LayoutTitleBlockMode Mode { get; init; } = LayoutTitleBlockMode.BuiltIn;
        public string DwgPath { get; init; } = "";
        public string SourceName { get; init; } = "";
        public List<LayoutTitleBlockAttributeMapping> AttributeMappings { get; init; } = [];
        public double BlockWidth { get; init; }
        public double BlockHeight { get; init; }
        public TitleBlockStripPosition StripPosition { get; init; } = TitleBlockStripPosition.Bottom;
        public double StripSize { get; init; }

        public bool IsConfiguredForExternal =>
            !string.IsNullOrWhiteSpace(DwgPath)
            && !string.IsNullOrWhiteSpace(SourceName)
            && AttributeMappings.Any(mapping =>
                !string.IsNullOrWhiteSpace(mapping.AttributeTag)
                && !string.IsNullOrWhiteSpace(mapping.PluginField));

        public string DisplaySourceName =>
            SourceName == ModelSpaceSourceName ? "(Model Space)" : SourceName;

        public string? GetPluginField(string attributeTag)
        {
            return AttributeMappings
                .FirstOrDefault(mapping =>
                    string.Equals(mapping.AttributeTag, attributeTag, StringComparison.OrdinalIgnoreCase))
                ?.PluginField;
        }
    }

    public sealed record LayoutTitleBlockFieldOption(string Value, string Label);

    public static class LayoutTitleBlockFields
    {
        public const string None = "";
        public const string ProjectName = "ProjectName";
        public const string DrawingTitle = "DrawingTitle";
        public const string DrawingNumber = "DrawingNumber";
        public const string Scale = "Scale";
        public const string Date = "Date";
        public const string Revision = "Revision";
        public const string RevisionContent = "RevisionContent";

        public static IReadOnlyList<LayoutTitleBlockFieldOption> Options { get; } = new[]
        {
            new LayoutTitleBlockFieldOption(None, "Khong gan"),
            new LayoutTitleBlockFieldOption(ProjectName, "Ten du an"),
            new LayoutTitleBlockFieldOption(DrawingTitle, "Ten ban ve"),
            new LayoutTitleBlockFieldOption(DrawingNumber, "So ban ve"),
            new LayoutTitleBlockFieldOption(Scale, "Ty le"),
            new LayoutTitleBlockFieldOption(Date, "Ngay"),
            new LayoutTitleBlockFieldOption(Revision, "Revision"),
            new LayoutTitleBlockFieldOption(RevisionContent, "Noi dung revision"),
        };

        public static string GetLabel(string value) =>
            Options.FirstOrDefault(option =>
                string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))?.Label
            ?? value;

        public static string ResolveValue(string field, LayoutTitleBlockValueSet values) => field switch
        {
            ProjectName => values.ProjectName,
            DrawingTitle => values.DrawingTitle,
            DrawingNumber => values.DrawingNumber,
            Scale => values.Scale,
            Date => values.Date,
            Revision => values.Revision,
            RevisionContent => values.RevisionContent,
            _ => string.Empty
        };
    }

    public sealed record LayoutTitleBlockValueSet
    {
        public string ProjectName { get; init; } = "";
        public string DrawingTitle { get; init; } = "";
        public string DrawingNumber { get; init; } = "";
        public string Scale { get; init; } = "";
        public string Date { get; init; } = "";
        public string Revision { get; init; } = "";
        public string RevisionContent { get; init; } = "";
    }
}
