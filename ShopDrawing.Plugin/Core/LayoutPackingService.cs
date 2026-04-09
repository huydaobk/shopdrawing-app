using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace ShopDrawing.Plugin.Core
{
    public class LayoutPackingService
    {
        private const double VIEWPORT_SPACING_MM = 10.0;
        private const double VIEW_TITLE_HEIGHT_MM = 18.0; // Space reserved for the title + scale below the viewport

        /// <summary>
        /// Reads all existing Viewports in the layout and their View Titles.
        /// Extracts their paper space bounding boxes.
        /// </summary>
        public static List<Extents2d> GetUsedRegions(Transaction tr, Layout layout)
        {
            const string SD_VIEWPORT_LAYER = "SD_VIEWPORT";
            var usedRegions = new List<Extents2d>();
            var psBtr = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);

            foreach (ObjectId id in psBtr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);

                // FIX: Use layer-based filter instead of vp.Number > 1.
                // Viewport.Number returns -1 when reading from a non-current layout,
                // causing ALL viewports to be rejected. Our custom viewports use
                // "SD_VIEWPORT" layer; the default PS viewport uses layer "0".
                if (ent is Viewport vp
                    && string.Equals(vp.Layer, SD_VIEWPORT_LAYER, StringComparison.OrdinalIgnoreCase))
                {
                    double halfW = vp.Width / 2.0;
                    double halfH = vp.Height / 2.0;
                    double minX = vp.CenterPoint.X - halfW;
                    double minY = vp.CenterPoint.Y - halfH;
                    double maxX = vp.CenterPoint.X + halfW;
                    double maxY = vp.CenterPoint.Y + halfH;

                    // Reserve space for the View Title BELOW the viewport
                    minY -= VIEW_TITLE_HEIGHT_MM;

                    usedRegions.Add(new Extents2d(new Point2d(minX, minY), new Point2d(maxX, maxY)));
                }
            }

            return usedRegions;
        }

        /// <summary>
        /// Attempts to find an empty spot on the layout for a new viewport.
        /// Tries placing to the right, flows down if horizontal space is exhausted.
        /// </summary>
        public static bool TryPackViewport(
            List<Extents2d> usedRegions,
            double usableMinX, double usableMaxX,
            double usableMinY, double usableMaxY,
            double reqWidth, double reqHeight,
            out Point2d newTopLeft)
        {
            bool packed = LayoutPackingMath.TryPackViewportBounds(
                usedRegions?
                    .Select(r => (r.MinPoint.X, r.MinPoint.Y, r.MaxPoint.X, r.MaxPoint.Y))
                    .ToList()
                    ?? new List<(double MinX, double MinY, double MaxX, double MaxY)>(),
                usableMinX,
                usableMaxX,
                usableMinY,
                usableMaxY,
                reqWidth,
                reqHeight,
                VIEWPORT_SPACING_MM,
                VIEW_TITLE_HEIGHT_MM,
                out var topLeft);

            newTopLeft = packed ? new Point2d(topLeft.LeftX, topLeft.TopY) : Point2d.Origin;
            return packed;
        }

        private static bool RegionsOverlap(Extents2d a, Extents2d b)
        {
            // Two rectangles don't overlap if one is completely to the left, right, top, or bottom of the other.
            if (a.MaxPoint.X <= b.MinPoint.X || a.MinPoint.X >= b.MaxPoint.X) return false;
            if (a.MaxPoint.Y <= b.MinPoint.Y || a.MinPoint.Y >= b.MaxPoint.Y) return false;
            return true; // Overlap
        }
    }
}
