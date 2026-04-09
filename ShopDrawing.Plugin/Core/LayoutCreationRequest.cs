using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ShopDrawing.Plugin.Core
{
    public enum LayoutViewKind
    {
        Elevation,
        Plan
    }

    public sealed record LayoutViewRequest
    {
        public LayoutViewKind Kind { get; init; } = LayoutViewKind.Elevation;
        public string UserTitle { get; init; } = "";
        public Extents3d Region { get; init; }
    }

    public sealed record LayoutCreationRequest
    {
        public string UserTitle { get; init; } = "";
        public Extents3d Region { get; init; }
        public string PaperSize { get; init; } = "A3";
        public string ProjectName { get; init; } = "";
        public double MarginLeft { get; init; } = 25;
        public double MarginRight { get; init; } = 5;
        public double MarginTop { get; init; } = 5;
        public double MarginBot { get; init; } = 5;
        public LayoutTitleBlockConfig TitleBlockConfig { get; init; } = new();
        public IReadOnlyList<LayoutViewRequest> SecondaryViews { get; init; } = Array.Empty<LayoutViewRequest>();

        public LayoutViewRequest CreatePrimaryView()
        {
            return new LayoutViewRequest
            {
                Kind = LayoutViewKind.Elevation,
                UserTitle = UserTitle,
                Region = Region
            };
        }
    }
}
