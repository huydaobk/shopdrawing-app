using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    public enum LayoutDirection { Horizontal, Vertical }
    public enum StartEdge { Left, Right }

    public class LayoutRequest
    {
        public Polyline? BoundaryPolyline { get; set; }
        public LayoutDirection Direction { get; set; }
        public StartEdge StartEdge { get; set; }
        public double PanelWidthMm { get; set; }
        public int ThicknessMm { get; set; }
        public string Spec { get; set; } = string.Empty;
        public double JointGapMm { get; set; }
        public string WallCode { get; set; } = string.Empty;
        public List<Opening> Openings { get; set; } = new();
        public string Application { get; set; } = string.Empty;
        public string TopPanelTreatment { get; set; } = string.Empty;
        public string StartPanelTreatment { get; set; } = string.Empty;
        public string EndPanelTreatment { get; set; } = string.Empty;
        public bool BottomEdgeEnabled { get; set; }
        public bool IsCeilingLayout { get; set; }
        public LayoutDirection CeilingSuspensionDirection { get; set; } = LayoutDirection.Vertical;
        public bool CeilingDivideFromMaxSide { get; set; }
        public double CeilingTSpacingMm { get; set; }
        public double CeilingTClearGapMm { get; set; }
        public int CeilingMushroomDivisionCount { get; set; }
        public List<double> CeilingBaySpansMm { get; set; } = new();
        public List<bool> CeilingBayHasMushroomFlags { get; set; } = new();
    }

    public class LayoutEngine
    {
        public LayoutResult Calculate(LayoutRequest request)
        {
            if (request.BoundaryPolyline == null)
            {
                throw new ArgumentException("Boundary polyline is required.");
            }

            var result = new LayoutResult();
            var polyline = request.BoundaryPolyline;
            var extents = polyline.GeometricExtents;

            double totalWidth = extents.MaxPoint.X - extents.MinPoint.X;
            double totalHeight = extents.MaxPoint.Y - extents.MinPoint.Y;
            double divisionSpan = request.Direction == LayoutDirection.Horizontal ? totalHeight : totalWidth;

            double slotWidth = request.PanelWidthMm + request.JointGapMm;
            int fullPanelCount = (int)Math.Floor(divisionSpan / slotWidth);
            double remnantWidth = divisionSpan - (fullPanelCount * slotWidth);

            if (Math.Abs(remnantWidth - request.PanelWidthMm) < 1.0)
            {
                fullPanelCount++;
                remnantWidth = 0;
            }

            double currentOffset = 0;
            for (int i = 0; i < fullPanelCount; i++)
            {
                var divisionWindow = GetDivisionWindow(currentOffset, request.PanelWidthMm, divisionSpan, request.StartEdge);
                var panel = BuildPanel(request, polyline, extents, divisionWindow, isRemnant: false);
                result.FullPanels.Add(panel);
                currentOffset += slotWidth;
            }

            if (remnantWidth > 1.0)
            {
                var divisionWindow = GetDivisionWindow(currentOffset, remnantWidth, divisionSpan, request.StartEdge);
                result.RemnantPanel = BuildPanel(request, polyline, extents, divisionWindow, isRemnant: true);
            }

            if (request.IsCeilingLayout)
            {
                SplitCeilingPanelsByTLines(request, result);
            }

            if (request.Openings.Count > 0)
            {
                var cutter = new OpeningCutter();
                cutter.ProcessCuts(result, request.Openings, request);
            }

            PanelIdGenerator.AssignIds(result.AllPanels, request.WallCode);
            return result;
        }

        private Panel BuildPanel(
            LayoutRequest request,
            Polyline polyline,
            Extents3d extents,
            (double Min, double Max) divisionWindow,
            bool isRemnant)
        {
            double leftEdge = divisionWindow.Min + 1.0;
            double rightEdge = divisionWindow.Max - 1.0;
            if (rightEdge < leftEdge)
            {
                rightEdge = leftEdge;
            }

            var spanLeft = GetPolylineSpanAt(polyline, leftEdge, request.Direction, extents);
            var spanRight = GetPolylineSpanAt(polyline, rightEdge, request.Direction, extents);
            var spanResult = spanLeft.Span >= spanRight.Span ? spanLeft : spanRight;

            var (jointLeft, jointRight) = ResolvePanelJoints(request.StartEdge, isRemnant);

            var panel = new Panel
            {
                WallCode = request.WallCode,
                ThickMm = request.ThicknessMm,
                Spec = request.Spec,
                WidthMm = Math.Max(0, divisionWindow.Max - divisionWindow.Min),
                LengthMm = spanResult.Span,
                IsReused = false,
                IsHorizontal = request.Direction == LayoutDirection.Horizontal,
                JointLeft = jointLeft,
                JointRight = jointRight,
                IsCutPanel = isRemnant,
                Application = request.Application,
                TopPanelTreatment = request.TopPanelTreatment,
                StartPanelTreatment = request.StartPanelTreatment,
                EndPanelTreatment = request.EndPanelTreatment,
                BottomEdgeEnabled = request.BottomEdgeEnabled
            };

            if (request.Direction == LayoutDirection.Horizontal)
            {
                panel.X = spanResult.Origin;
                panel.Y = extents.MinPoint.Y + divisionWindow.Min;
            }
            else
            {
                panel.X = extents.MinPoint.X + divisionWindow.Min;
                panel.Y = spanResult.Origin;
            }

            double spanDiff = Math.Abs(spanLeft.Span - spanRight.Span);
            if (spanDiff > 1.0)
            {
                double stepPos = BinarySearchStepPosition(
                    polyline,
                    leftEdge,
                    rightEdge,
                    request.Direction,
                    extents,
                    spanLeft.Span,
                    spanRight.Span);

                panel.StepWasteWidth = spanLeft.Span < spanRight.Span
                    ? stepPos - leftEdge
                    : rightEdge - stepPos;
                panel.StepWasteHeight = spanDiff;
            }

            return panel;
        }

        private static (JointType Left, JointType Right) ResolvePanelJoints(
            StartEdge startEdge,
            bool isRemnant)
        {
            if (!isRemnant)
            {
                return (JointType.Female, JointType.Male);
            }

            return startEdge == StartEdge.Right
                ? (JointType.Cut, JointType.Male)
                : (JointType.Female, JointType.Cut);
        }

        private static (double Min, double Max) GetDivisionWindow(
            double currentOffset,
            double panelWidth,
            double divisionSpan,
            StartEdge startEdge)
        {
            if (startEdge == StartEdge.Right)
            {
                double max = divisionSpan - currentOffset;
                return (max - panelWidth, max);
            }

            return (currentOffset, currentOffset + panelWidth);
        }

        private SpanResult GetPolylineSpanAt(Polyline polyline, double relativePos, LayoutDirection direction, Extents3d extents)
        {
            double absolutePos = direction == LayoutDirection.Vertical
                ? extents.MinPoint.X + relativePos
                : extents.MinPoint.Y + relativePos;

            var intersections = new List<double>();
            int vertexCount = polyline.NumberOfVertices;

            for (int i = 0; i < vertexCount; i++)
            {
                int j = (i + 1) % vertexCount;
                var p1 = polyline.GetPoint2dAt(i);
                var p2 = polyline.GetPoint2dAt(j);

                double a;
                double b;
                double c;
                double d;
                if (direction == LayoutDirection.Vertical)
                {
                    a = p1.X;
                    b = p2.X;
                    c = p1.Y;
                    d = p2.Y;
                }
                else
                {
                    a = p1.Y;
                    b = p2.Y;
                    c = p1.X;
                    d = p2.X;
                }

                double minAB = Math.Min(a, b);
                double maxAB = Math.Max(a, b);
                if (absolutePos < minAB - 0.01 || absolutePos > maxAB + 0.01)
                {
                    continue;
                }

                if (Math.Abs(b - a) < 0.01)
                {
                    intersections.Add(c);
                    intersections.Add(d);
                    continue;
                }

                double t = (absolutePos - a) / (b - a);
                intersections.Add(c + t * (d - c));
            }

            if (intersections.Count < 2)
            {
                return direction == LayoutDirection.Vertical
                    ? new SpanResult(extents.MaxPoint.Y - extents.MinPoint.Y, extents.MinPoint.Y)
                    : new SpanResult(extents.MaxPoint.X - extents.MinPoint.X, extents.MinPoint.X);
            }

            double spanMin = intersections.Min();
            double spanMax = intersections.Max();
            return new SpanResult(spanMax - spanMin, spanMin);
        }

        private record SpanResult(double Span, double Origin);

        private double BinarySearchStepPosition(
            Polyline polyline,
            double posA,
            double posB,
            LayoutDirection direction,
            Extents3d extents,
            double spanA,
            double spanB,
            int maxIter = 15)
        {
            double tolerance = 2.0;
            for (int i = 0; i < maxIter; i++)
            {
                double mid = (posA + posB) / 2.0;
                var spanMid = GetPolylineSpanAt(polyline, mid, direction, extents);
                if (Math.Abs(spanMid.Span - spanA) < tolerance)
                {
                    posA = mid;
                }
                else
                {
                    posB = mid;
                }

                if (Math.Abs(posB - posA) < tolerance)
                {
                    break;
                }
            }

            return (posA + posB) / 2.0;
        }

        private static void SplitCeilingPanelsByTLines(LayoutRequest request, LayoutResult result)
        {
            if (request.BoundaryPolyline == null || request.CeilingTSpacingMm <= 0)
            {
                return;
            }

            CeilingSuspensionLayoutData? tLayout = CeilingSuspensionPreviewService.BuildLayout(
                request.BoundaryPolyline,
                request.CeilingSuspensionDirection,
                request.CeilingDivideFromMaxSide,
                request.CeilingTSpacingMm,
                0,
                request.CeilingBaySpansMm);

            if (tLayout == null || tLayout.TPositions.Count == 0)
            {
                return;
            }

            result.FullPanels = result.FullPanels
                .SelectMany(panel => SplitPanelByTLines(panel, tLayout, request.CeilingTClearGapMm))
                .ToList();

            if (result.RemnantPanel == null)
            {
                return;
            }

            List<Panel> remnantPieces = SplitPanelByTLines(result.RemnantPanel, tLayout, request.CeilingTClearGapMm);
            if (remnantPieces.Count == 1)
            {
                result.RemnantPanel = remnantPieces[0];
                return;
            }

            result.FullPanels.AddRange(remnantPieces);
            result.RemnantPanel = null;
        }

        private static List<Panel> SplitPanelByTLines(
            Panel sourcePanel,
            CeilingSuspensionLayoutData tLayout,
            double tClearGapMm)
        {
            bool splitAlongX = !tLayout.RunAlongX;
            if (splitAlongX != sourcePanel.IsHorizontal)
            {
                return new List<Panel> { sourcePanel };
            }

            double panelStart = splitAlongX ? sourcePanel.X : sourcePanel.Y;
            double panelLength = sourcePanel.LengthMm;
            double panelEnd = panelStart + panelLength;
            const double tolerance = 1.0;

            List<double> splitPositions = tLayout.TPositions
                .Where(pos => pos > panelStart + tolerance && pos < panelEnd - tolerance)
                .Distinct()
                .OrderBy(pos => pos)
                .ToList();

            if (splitPositions.Count == 0)
            {
                return new List<Panel> { sourcePanel };
            }

            var pieces = new List<Panel>();
            double cursor = panelStart;
            double halfGap = Math.Max(0, tClearGapMm) / 2.0;

            for (int i = 0; i < splitPositions.Count; i++)
            {
                double splitPosition = splitPositions[i];
                double segmentStart = cursor;
                double segmentEnd = Math.Min(panelEnd, splitPosition - halfGap);
                double segmentLength = segmentEnd - segmentStart;
                if (segmentLength > tolerance)
                {
                    pieces.Add(CreateSplitPanelPiece(
                        sourcePanel,
                        splitAlongX,
                        segmentStart,
                        segmentLength,
                        pieces.Count == 0 ? sourcePanel.JointLeft : JointType.Female,
                        JointType.Male));
                }

                cursor = Math.Max(cursor, Math.Min(panelEnd, splitPosition + halfGap));
            }

            double lastSegmentLength = panelEnd - cursor;
            if (lastSegmentLength > tolerance)
            {
                pieces.Add(CreateSplitPanelPiece(
                    sourcePanel,
                    splitAlongX,
                    cursor,
                    lastSegmentLength,
                    pieces.Count == 0 ? sourcePanel.JointLeft : JointType.Female,
                    sourcePanel.JointRight));
            }

            return pieces.Count > 0 ? pieces : new List<Panel> { sourcePanel };
        }

        private static Panel CreateSplitPanelPiece(
            Panel sourcePanel,
            bool splitAlongX,
            double segmentStart,
            double segmentLength,
            JointType jointLeft,
            JointType jointRight)
        {
            return new Panel
            {
                PanelId = sourcePanel.PanelId,
                WallCode = sourcePanel.WallCode,
                X = splitAlongX ? segmentStart : sourcePanel.X,
                Y = splitAlongX ? sourcePanel.Y : segmentStart,
                WidthMm = sourcePanel.WidthMm,
                LengthMm = segmentLength,
                ThickMm = sourcePanel.ThickMm,
                Spec = sourcePanel.Spec,
                JointLeft = jointLeft,
                JointRight = jointRight,
                IsReused = sourcePanel.IsReused,
                SourceId = sourcePanel.SourceId,
                IsCutPanel = sourcePanel.IsCutPanel,
                IsHorizontal = sourcePanel.IsHorizontal,
                ParentPanelId = sourcePanel.ParentPanelId,
                StepWasteWidth = 0,
                StepWasteHeight = 0,
                Application = sourcePanel.Application,
                TopPanelTreatment = sourcePanel.TopPanelTreatment,
                StartPanelTreatment = sourcePanel.StartPanelTreatment,
                EndPanelTreatment = sourcePanel.EndPanelTreatment,
                BottomEdgeEnabled = sourcePanel.BottomEdgeEnabled
            };
        }
    }
}
