using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace ShopDrawing.Plugin.Core
{
    public sealed record CeilingSuspensionPreview(
        double TSpacingMm,
        double TClearGapMm,
        int TLineCount,
        int MushroomDivisionCount,
        int MushroomLineCount,
        double WidthMm,
        double HeightMm,
        IReadOnlyList<double> BaySpansMm,
        IReadOnlyList<double> PanelSpansMm,
        IReadOnlyList<bool> BayHasMushroomFlags);

    public sealed record CeilingSuspensionLayoutData(
        bool RunAlongX,
        IReadOnlyList<double> TPositions,
        IReadOnlyList<double> MushroomPositions,
        IReadOnlyList<double> BaySpansMm,
        IReadOnlyList<bool> BayHasMushroomFlags);

    internal static class CeilingSuspensionPreviewService
    {
        public static CeilingSuspensionPreview? Calculate(
            Polyline boundary,
            LayoutDirection suspensionDirection,
            bool divideFromMaxSide,
            double tSpacingMm,
            int mushroomDivisionCount,
            double tClearGapMm = 0,
            IReadOnlyList<double>? baySpansMm = null,
            IReadOnlyList<bool>? bayHasMushroomFlags = null)
        {
            var layout = BuildLayout(
                boundary,
                suspensionDirection,
                divideFromMaxSide,
                tSpacingMm,
                mushroomDivisionCount,
                baySpansMm,
                bayHasMushroomFlags);

            if (layout == null)
            {
                return null;
            }

            var ext = boundary.GeometricExtents;
            double min = layout.RunAlongX ? ext.MinPoint.Y : ext.MinPoint.X;
            double max = layout.RunAlongX ? ext.MaxPoint.Y : ext.MaxPoint.X;

            return new CeilingSuspensionPreview(
                tSpacingMm,
                Math.Max(0, tClearGapMm),
                layout.TPositions.Count,
                mushroomDivisionCount,
                layout.MushroomPositions.Count,
                Math.Max(0, ext.MaxPoint.X - ext.MinPoint.X),
                Math.Max(0, ext.MaxPoint.Y - ext.MinPoint.Y),
                layout.BaySpansMm,
                BuildPanelSpans(min, max, layout.TPositions, tClearGapMm),
                layout.BayHasMushroomFlags);
        }

        public static CeilingSuspensionLayoutData? BuildLayout(
            Polyline boundary,
            LayoutDirection suspensionDirection,
            bool divideFromMaxSide,
            double tSpacingMm,
            int mushroomDivisionCount,
            IReadOnlyList<double>? baySpansMm = null,
            IReadOnlyList<bool>? bayHasMushroomFlags = null)
        {
            var vertices = GetPolylineVertices(boundary);
            if (vertices.Count < 3)
            {
                return null;
            }

            bool runAlongX = suspensionDirection != LayoutDirection.Horizontal;
            double min = runAlongX ? vertices.Min(v => v[1]) : vertices.Min(v => v[0]);
            double max = runAlongX ? vertices.Max(v => v[1]) : vertices.Max(v => v[0]);

            var normalizedBaySpans = NormalizeBaySpans(baySpansMm);
            if (normalizedBaySpans.Count == 0)
            {
                normalizedBaySpans = BuildRepeatedBaySpans(min, max, divideFromMaxSide, tSpacingMm);
            }

            var normalizedBayHasMushroomFlags = NormalizeBayHasMushroomFlags(bayHasMushroomFlags, normalizedBaySpans.Count);

            var tPositions = BuildTPositions(min, max, divideFromMaxSide, normalizedBaySpans);
            var mushroomPositions = BuildMushroomPositions(
                min,
                max,
                divideFromMaxSide,
                normalizedBaySpans,
                normalizedBayHasMushroomFlags,
                mushroomDivisionCount);

            return new CeilingSuspensionLayoutData(runAlongX, tPositions, mushroomPositions, normalizedBaySpans, normalizedBayHasMushroomFlags);
        }

        public static IReadOnlyList<(double Start, double End)> GetScanSegments(
            Polyline boundary,
            double position,
            bool runAlongX)
        {
            var vertices = GetPolylineVertices(boundary);
            return GetScanSegments(vertices, position, runAlongX);
        }

        public static IReadOnlyList<double> NormalizeBaySpans(IReadOnlyList<double>? baySpansMm)
        {
            if (baySpansMm == null || baySpansMm.Count == 0)
            {
                return Array.Empty<double>();
            }

            return baySpansMm
                .Where(span => span > 1.0)
                .Select(span => Math.Round(span, 3))
                .ToList();
        }

        public static IReadOnlyList<bool> NormalizeBayHasMushroomFlags(IReadOnlyList<bool>? flags, int targetCount)
        {
            if (targetCount <= 0)
            {
                return Array.Empty<bool>();
            }

            var result = Enumerable.Repeat(true, targetCount).ToList();
            if (flags == null || flags.Count == 0)
            {
                return result;
            }

            for (int i = 0; i < Math.Min(targetCount, flags.Count); i++)
            {
                result[i] = flags[i];
            }

            return result;
        }

        public static IReadOnlyList<double> GenerateSymmetricBaySpans(double axisSpanMm, double tSpacingMm)
        {
            var spans = new List<double>();
            if (axisSpanMm <= 1.0 || tSpacingMm <= 1.0)
            {
                return spans;
            }

            int fullSpacingCount = (int)Math.Floor(axisSpanMm / tSpacingMm);
            if (fullSpacingCount <= 0)
            {
                return spans;
            }

            double remainder = axisSpanMm - (fullSpacingCount * tSpacingMm);
            if (remainder <= 1.0)
            {
                for (int i = 0; i < fullSpacingCount - 1; i++)
                {
                    spans.Add(tSpacingMm);
                }

                return spans;
            }

            double edgeSpan = remainder / 2.0;
            spans.Add(edgeSpan);
            for (int i = 0; i < fullSpacingCount; i++)
            {
                spans.Add(tSpacingMm);
            }

            return spans;
        }

        private static List<double> BuildRepeatedBaySpans(
            double min,
            double max,
            bool divideFromMaxSide,
            double tSpacingMm)
        {
            return GenerateSymmetricBaySpans(max - min, tSpacingMm).ToList();
        }

        private static List<double> BuildTPositions(
            double min,
            double max,
            bool divideFromMaxSide,
            IReadOnlyList<double> baySpansMm)
        {
            var positions = new List<double>();
            if (baySpansMm.Count == 0)
            {
                return positions;
            }

            const double edgeOffset = 0.5;
            double current = divideFromMaxSide ? max : min;

            foreach (double baySpan in baySpansMm)
            {
                double next = divideFromMaxSide
                    ? current - baySpan
                    : current + baySpan;

                bool isInside = divideFromMaxSide
                    ? next > min + edgeOffset - 1e-6
                    : next < max - edgeOffset + 1e-6;

                if (!isInside)
                {
                    break;
                }

                positions.Add(next);
                current = next;
            }

            positions.Sort();
            return positions;
        }

        private static List<double> BuildMushroomPositions(
            double min,
            double max,
            bool divideFromMaxSide,
            IReadOnlyList<double> baySpansMm,
            IReadOnlyList<bool> bayHasMushroomFlags,
            int mushroomDivisionCount)
        {
            var positions = new List<double>();
            if (mushroomDivisionCount <= 0 || baySpansMm.Count == 0)
            {
                return positions;
            }

            const double edgeOffset = 0.5;
            double current = divideFromMaxSide ? max : min;
            int bayIndex = 0;

            foreach (double baySpan in baySpansMm)
            {
                bool hasMushroom = bayIndex < bayHasMushroomFlags.Count ? bayHasMushroomFlags[bayIndex] : true;
                double next = divideFromMaxSide
                    ? current - baySpan
                    : current + baySpan;

                bool isInside = divideFromMaxSide
                    ? next > min + edgeOffset - 1e-6
                    : next < max - edgeOffset + 1e-6;

                if (!isInside)
                {
                    break;
                }

                if (hasMushroom)
                {
                    double part = baySpan / (mushroomDivisionCount + 1);
                    for (int i = 1; i <= mushroomDivisionCount; i++)
                    {
                        double position = divideFromMaxSide
                            ? current - (part * i)
                            : current + (part * i);

                        if (position > min + edgeOffset - 1e-6 && position < max - edgeOffset + 1e-6)
                        {
                            positions.Add(position);
                        }
                    }
                }

                current = next;
                bayIndex++;
            }

            positions.Sort();
            return positions
                .Where((pos, index) => index == 0 || Math.Abs(pos - positions[index - 1]) > 1.0)
                .ToList();
        }

        private static IReadOnlyList<double> BuildPanelSpans(
            double min,
            double max,
            IReadOnlyList<double> tPositions,
            double tClearGapMm)
        {
            var panelSpans = new List<double>();
            double cursor = min;
            double halfGap = Math.Max(0, tClearGapMm) / 2.0;

            foreach (double tPosition in tPositions.OrderBy(pos => pos))
            {
                double segmentEnd = Math.Min(max, tPosition - halfGap);
                if (segmentEnd - cursor > 1.0)
                {
                    panelSpans.Add(segmentEnd - cursor);
                }

                cursor = Math.Max(cursor, Math.Min(max, tPosition + halfGap));
            }

            if (max - cursor > 1.0)
            {
                panelSpans.Add(max - cursor);
            }

            return panelSpans;
        }

        private static List<double[]> GetPolylineVertices(Polyline polyline)
        {
            var vertices = new List<double[]>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                var pt = polyline.GetPoint2dAt(i);
                vertices.Add(new[] { pt.X, pt.Y });
            }

            if (vertices.Count >= 2)
            {
                var first = vertices[0];
                var last = vertices[^1];
                if (Math.Abs(last[0] - first[0]) < 1e-6 && Math.Abs(last[1] - first[1]) < 1e-6)
                {
                    vertices.RemoveAt(vertices.Count - 1);
                }
            }

            return vertices;
        }

        private static IReadOnlyList<(double Start, double End)> GetScanSegments(
            List<double[]> vertices,
            double scanPos,
            bool runAlongX)
        {
            bool horizontalLine = runAlongX;
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
                {
                    segments.Add((start, end));
                }
            }

            return segments;
        }
    }
}
