using Autodesk.AutoCAD.DatabaseServices;

namespace ShopDrawing.Plugin.Core
{
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
    }
}
