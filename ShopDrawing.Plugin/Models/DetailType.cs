using Autodesk.AutoCAD.Geometry;

namespace ShopDrawing.Plugin.Models
{
    public enum DetailType { All, BaseU, CornerExternal, CornerInternal, TopCap }

    public enum EdgePosition { Top, Bottom, Left, Right }

    public class Edge
    {
        public Point3d Start { get; set; }
        public Point3d End { get; set; }
        public EdgePosition Position { get; set; }
    }
}
