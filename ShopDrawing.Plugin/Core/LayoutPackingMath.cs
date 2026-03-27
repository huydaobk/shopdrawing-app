using System.Collections.Generic;
using System.Linq;

namespace ShopDrawing.Plugin.Core
{
    public static class LayoutPackingMath
    {
        public static bool TryPackViewportBounds(
            IReadOnlyCollection<(double MinX, double MinY, double MaxX, double MaxY)> usedRegions,
            double usableMinX,
            double usableMaxX,
            double usableMinY,
            double usableMaxY,
            double reqWidth,
            double reqHeight,
            double spacing,
            double titleHeight,
            out (double LeftX, double TopY) newTopLeft)
        {
            double totalReqHeight = reqHeight + titleHeight;
            if (reqWidth <= 0 || totalReqHeight <= 0)
            {
                newTopLeft = default;
                return false;
            }

            var regions = usedRegions?.ToList() ?? new List<(double MinX, double MinY, double MaxX, double MaxY)>();
            if (regions.Count == 0)
            {
                if (FitsInsideUsableArea(
                    usableMinX,
                    usableMaxY - totalReqHeight,
                    usableMinX + reqWidth,
                    usableMaxY,
                    usableMinX,
                    usableMaxX,
                    usableMinY,
                    usableMaxY))
                {
                    newTopLeft = (usableMinX, usableMaxY);
                    return true;
                }

                newTopLeft = default;
                return false;
            }

            var candidateLefts = regions
                .Select(r => r.MaxX + spacing)
                .Append(usableMinX)
                .Where(x => x >= usableMinX && x <= usableMaxX)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var candidateTops = regions
                .Select(r => r.MaxY)
                .Concat(regions.Select(r => r.MinY - spacing))
                .Append(usableMaxY)
                .Where(y => y >= usableMinY && y <= usableMaxY)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            foreach (double top in candidateTops)
            {
                foreach (double left in candidateLefts)
                {
                    double minX = left;
                    double minY = top - totalReqHeight;
                    double maxX = left + reqWidth;
                    double maxY = top;

                    if (!FitsInsideUsableArea(minX, minY, maxX, maxY, usableMinX, usableMaxX, usableMinY, usableMaxY))
                    {
                        continue;
                    }

                    bool overlaps = regions.Any(r => RegionsOverlap(r.MinX, r.MinY, r.MaxX, r.MaxY, minX, minY, maxX, maxY));
                    if (overlaps)
                    {
                        continue;
                    }

                    newTopLeft = (left, top);
                    return true;
                }
            }

            newTopLeft = default;
            return false;
        }

        private static bool FitsInsideUsableArea(
            double minX,
            double minY,
            double maxX,
            double maxY,
            double usableMinX,
            double usableMaxX,
            double usableMinY,
            double usableMaxY)
        {
            return minX >= usableMinX
                && maxX <= usableMaxX
                && minY >= usableMinY
                && maxY <= usableMaxY;
        }

        private static bool RegionsOverlap(
            double aMinX,
            double aMinY,
            double aMaxX,
            double aMaxY,
            double bMinX,
            double bMinY,
            double bMaxX,
            double bMaxY)
        {
            if (aMaxX <= bMinX || aMinX >= bMaxX) return false;
            if (aMaxY <= bMinY || aMinY >= bMaxY) return false;
            return true;
        }
    }
}
