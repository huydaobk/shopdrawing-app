using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Modules.Accessories
{
    internal sealed class ShopdrawingWallAccessoryScanner
    {
        public IReadOnlyList<ShopdrawingWallAccessorySnapshot> ScanWalls(
            Transaction tr,
            Database db,
            string application,
            string fallbackSpecKey)
        {
            if (tr.GetObject(db.BlockTableId, OpenMode.ForRead) is not BlockTable bt
                || tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) is not BlockTableRecord ms)
            {
                return Array.Empty<ShopdrawingWallAccessorySnapshot>();
            }

            var panelEntities = new List<PanelEntityInfo>();
            var tagTexts = new List<TagTextInfo>();
            var openingBounds = new List<OpeningBounds>();

            foreach (ObjectId id in ms)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity entity)
                {
                    continue;
                }

                if (entity is DBText text && string.Equals(text.Layer, "SD_TAG", StringComparison.OrdinalIgnoreCase))
                {
                    Point3d point = text.HorizontalMode != TextHorizontalMode.TextLeft
                        ? text.AlignmentPoint
                        : text.Position;

                    tagTexts.Add(new TagTextInfo(point.X, point.Y, text.TextString));
                    continue;
                }

                if (entity is Polyline panelPolyline
                    && string.Equals(panelPolyline.Layer, "SD_PANEL", StringComparison.OrdinalIgnoreCase)
                    && panelPolyline.Closed
                    && panelPolyline.NumberOfVertices >= 4)
                {
                    panelEntities.Add(CreatePanelEntityInfo(panelPolyline, ShopdrawingEntityMetadata.Read(entity)));
                    continue;
                }

                if (entity is Polyline openingPolyline
                    && string.Equals(openingPolyline.Layer, "SD_OPENING", StringComparison.OrdinalIgnoreCase)
                    && openingPolyline.Closed
                    && openingPolyline.NumberOfVertices >= 4)
                {
                    openingBounds.Add(CreateOpeningBounds(openingPolyline, ShopdrawingEntityMetadata.Read(entity)));
                }
            }

            var taggedPanels = MatchPanelTags(panelEntities, tagTexts, fallbackSpecKey);
            return taggedPanels
                .Where(panel => panel.WallCode.StartsWith("W", StringComparison.OrdinalIgnoreCase))
                .GroupBy(panel => panel.WallCode, StringComparer.OrdinalIgnoreCase)
                .Select(group => BuildSnapshot(group.Key, group.ToList(), openingBounds, application))
                .OrderBy(snapshot => snapshot.WallCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ShopdrawingWallAccessorySnapshot BuildSnapshot(
            string wallCode,
            List<PanelEntityInfo> panels,
            List<OpeningBounds> openings,
            string fallbackApplication)
        {
            double minX = panels.Min(panel => panel.MinX);
            double maxX = panels.Max(panel => panel.MaxX);
            double minY = panels.Min(panel => panel.MinY);
            double maxY = panels.Max(panel => panel.MaxY);
            double wallLengthM = Math.Max(0, (maxX - minX) / 1000.0);
            double wallHeightM = Math.Max(0, (maxY - minY) / 1000.0);

            bool isHorizontalLayout = panels.Count(panel => panel.WidthMm > panel.HeightMm) >= Math.Max(1, panels.Count / 2);
            double jointLengthM = EstimateJointLengthM(panels, wallLengthM, wallHeightM, isHorizontalLayout);
            double verticalJointLengthM = EstimateVerticalJointLengthM(panels, wallHeightM, isHorizontalLayout);
            double tekScrewQty = panels.Sum(panel => Math.Ceiling(panel.LongSpanMm / 1500.0) * 2.0);
            double panelSupportBracketQty = wallLengthM <= 0 ? 0 : Math.Ceiling((wallLengthM * 1000.0) / 1500.0);

            var relatedOpenings = openings
                .Where(opening => Overlaps(opening, minX, minY, maxX, maxY))
                .ToList();

            PanelEntityInfo metadataPanel = panels.FirstOrDefault(panel => panel.HasMetadata) ?? panels[0];
            string resolvedApplication = !string.IsNullOrWhiteSpace(metadataPanel.Application)
                ? metadataPanel.Application
                : fallbackApplication;

            string topTreatment = TenderWall.NormalizeTopPanelTreatment(metadataPanel.TopPanelTreatment);
            string startTreatment = TenderWall.NormalizeEndPanelTreatment(metadataPanel.StartPanelTreatment);
            string endTreatment = TenderWall.NormalizeEndPanelTreatment(metadataPanel.EndPanelTreatment);
            bool bottomEdgeEnabled = metadataPanel.BottomEdgeEnabled;

            double topCenterLengthM = string.Equals(topTreatment, TenderWall.TopPanelTreatmentCeilingCenter, StringComparison.OrdinalIgnoreCase)
                ? wallLengthM
                : 0;
            double topPerimeterLengthM = string.Equals(topTreatment, TenderWall.TopPanelTreatmentCeilingPerimeter, StringComparison.OrdinalIgnoreCase)
                ? wallLengthM
                : 0;
            double topFreeLengthM = string.Equals(topTreatment, TenderWall.TopPanelTreatmentFree, StringComparison.OrdinalIgnoreCase)
                ? wallLengthM
                : 0;
            double topEdgeLengthM = string.Equals(topTreatment, TenderWall.TopPanelTreatmentNone, StringComparison.OrdinalIgnoreCase)
                ? 0
                : wallLengthM;
            double bottomEdgeLengthM = bottomEdgeEnabled ? wallLengthM : 0;

            double endCenterLengthM =
                GetEndContribution(startTreatment, TenderWall.EndPanelTreatmentCenter, wallHeightM) +
                GetEndContribution(endTreatment, TenderWall.EndPanelTreatmentCenter, wallHeightM);
            double endPerimeterLengthM =
                GetEndContribution(startTreatment, TenderWall.EndPanelTreatmentPerimeter, wallHeightM) +
                GetEndContribution(endTreatment, TenderWall.EndPanelTreatmentPerimeter, wallHeightM);
            double endFreeLengthM =
                GetEndContribution(startTreatment, TenderWall.EndPanelTreatmentFree, wallHeightM) +
                GetEndContribution(endTreatment, TenderWall.EndPanelTreatmentFree, wallHeightM);
            double exposedEndLengthM = endFreeLengthM;
            double totalExposedEdgeLengthM = topEdgeLengthM + bottomEdgeLengthM + exposedEndLengthM;

            double openingPerimeterM = relatedOpenings.Sum(o => o.PerimeterMm) / 1000.0;
            double openingPerimeterTwoFacesM = relatedOpenings.Sum(o => o.PerimeterTwoFacesMm) / 1000.0;
            double doorOpeningPerimeterM = relatedOpenings.Where(o => o.IsDoor).Sum(o => o.PerimeterMm) / 1000.0;
            double nonDoorOpeningPerimeterM = relatedOpenings.Where(o => !o.IsDoor).Sum(o => o.PerimeterMm) / 1000.0;
            int openingCount = relatedOpenings.Count;
            int doorOpeningCount = relatedOpenings.Count(o => o.IsDoor);
            int nonDoorOpeningCount = relatedOpenings.Count(o => !o.IsDoor);
            double openingVerticalEdgesM = relatedOpenings.Sum(o => 2.0 * o.HeightMm) / 1000.0;
            double openingTopLengthM = relatedOpenings.Sum(o => o.WidthMm) / 1000.0;
            double openingSillLengthM = relatedOpenings.Where(o => !o.IsDoor).Sum(o => o.WidthMm) / 1000.0;
            double openingAreaM2 = relatedOpenings.Sum(o => o.WidthMm * o.HeightMm) / 1_000_000.0;
            double netAreaM2 = Math.Max(0, wallLengthM * wallHeightM - openingAreaM2);
            string specKey = panels.FirstOrDefault(panel => !string.IsNullOrWhiteSpace(panel.SpecKey))?.SpecKey ?? string.Empty;

            return new ShopdrawingWallAccessorySnapshot(
                wallCode,
                specKey,
                resolvedApplication,
                wallLengthM,
                wallHeightM,
                netAreaM2,
                panels.Count,
                jointLengthM,
                verticalJointLengthM,
                topEdgeLengthM,
                topCenterLengthM,
                topPerimeterLengthM,
                topFreeLengthM,
                bottomEdgeLengthM,
                endCenterLengthM,
                endPerimeterLengthM,
                endFreeLengthM,
                exposedEndLengthM,
                totalExposedEdgeLengthM,
                openingPerimeterM,
                openingPerimeterTwoFacesM,
                openingCount,
                doorOpeningPerimeterM,
                nonDoorOpeningPerimeterM,
                doorOpeningCount,
                nonDoorOpeningCount,
                openingVerticalEdgesM,
                openingTopLengthM,
                openingSillLengthM,
                tekScrewQty,
                panelSupportBracketQty);
        }

        private static double GetEndContribution(string treatment, string targetTreatment, double wallHeightM)
        {
            return string.Equals(treatment, targetTreatment, StringComparison.OrdinalIgnoreCase)
                ? wallHeightM
                : 0;
        }

        private static double EstimateJointLengthM(
            IReadOnlyList<PanelEntityInfo> panels,
            double wallLengthM,
            double wallHeightM,
            bool isHorizontalLayout)
        {
            if (panels.Count <= 1)
            {
                return 0;
            }

            if (isHorizontalLayout)
            {
                int rowCount = CountDistinctBands(panels.Select(panel => (panel.MinY, panel.MaxY)));
                return Math.Max(0, rowCount - 1) * wallLengthM;
            }

            int columnCount = CountDistinctBands(panels.Select(panel => (panel.MinX, panel.MaxX)));
            return Math.Max(0, columnCount - 1) * wallHeightM;
        }

        private static double EstimateVerticalJointLengthM(
            IReadOnlyList<PanelEntityInfo> panels,
            double wallHeightM,
            bool isHorizontalLayout)
        {
            if (!isHorizontalLayout || panels.Count <= 1)
            {
                return 0;
            }

            int columnCount = CountDistinctBands(panels.Select(panel => (panel.MinX, panel.MaxX)));
            return Math.Max(0, columnCount - 1) * wallHeightM;
        }

        private static int CountDistinctBands(IEnumerable<(double Min, double Max)> bands)
        {
            const double tolerance = 5.0;
            var normalized = new List<(double Min, double Max)>();
            foreach (var band in bands.OrderBy(b => b.Min).ThenBy(b => b.Max))
            {
                if (normalized.Count == 0)
                {
                    normalized.Add(band);
                    continue;
                }

                var previous = normalized[^1];
                if (Math.Abs(previous.Min - band.Min) <= tolerance && Math.Abs(previous.Max - band.Max) <= tolerance)
                {
                    continue;
                }

                normalized.Add(band);
            }

            return normalized.Count;
        }

        private static bool Overlaps(OpeningBounds opening, double minX, double minY, double maxX, double maxY)
        {
            return opening.MaxX > minX
                && opening.MinX < maxX
                && opening.MaxY > minY
                && opening.MinY < maxY;
        }

        private static List<PanelEntityInfo> MatchPanelTags(
            IReadOnlyList<PanelEntityInfo> panels,
            IReadOnlyList<TagTextInfo> tagTexts,
            string fallbackSpecKey)
        {
            const double margin = 50.0;
            var taggedPanels = new List<PanelEntityInfo>();

            foreach (var panel in panels)
            {
                var textsInside = tagTexts
                    .Where(t => t.X >= panel.MinX - margin && t.X <= panel.MaxX + margin
                        && t.Y >= panel.MinY - margin && t.Y <= panel.MaxY + margin)
                    .OrderByDescending(t => t.Y)
                    .ToList();

                string panelId = string.Empty;
                string specKey = string.Empty;

                foreach (var text in textsInside)
                {
                    if (!TryParsePanelTag(text.Text, out string parsedPanelId, out string parsedSpecKey))
                    {
                        continue;
                    }

                    panelId = parsedPanelId;
                    specKey = parsedSpecKey;
                    break;
                }

                if (string.IsNullOrWhiteSpace(panelId) || !panelId.StartsWith("W", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string wallCode = panelId.Split('-')[0];
                taggedPanels.Add(panel with
                {
                    PanelId = panelId,
                    WallCode = wallCode,
                    SpecKey = string.IsNullOrWhiteSpace(specKey) ? fallbackSpecKey : specKey
                });
            }

            return taggedPanels;
        }

        private static bool TryParsePanelTag(string text, out string panelId, out string specKey)
        {
            panelId = string.Empty;
            specKey = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!text.Contains('-') || (!text.StartsWith("W", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("C", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string workingText = text.Trim();
            if (workingText.Contains(" / ", StringComparison.Ordinal))
            {
                int slashIndex = workingText.IndexOf(" / ", StringComparison.Ordinal);
                string leftPart = workingText.Substring(0, slashIndex).Trim();
                specKey = workingText.Substring(slashIndex + 3).Trim();
                panelId = leftPart.Split(' ')[0].Trim();
                return !string.IsNullOrWhiteSpace(panelId);
            }

            panelId = workingText.Split(' ')[0].Trim();
            return !string.IsNullOrWhiteSpace(panelId);
        }

        private static PanelEntityInfo CreatePanelEntityInfo(Polyline polyline, IReadOnlyDictionary<string, string> metadata)
        {
            GetBounds(polyline, out double minX, out double minY, out double maxX, out double maxY);

            return new PanelEntityInfo(
                Math.Round(minX, 3),
                Math.Round(minY, 3),
                Math.Round(maxX, 3),
                Math.Round(maxY, 3),
                Math.Round(maxX - minX, 3),
                Math.Round(maxY - minY, 3),
                Math.Round(Math.Max(maxX - minX, maxY - minY), 3),
                string.Empty,
                string.Empty,
                string.Empty,
                ReadMetadataValue(metadata, "APP"),
                TenderWall.NormalizeTopPanelTreatment(ReadMetadataValue(metadata, "TOP")),
                TenderWall.NormalizeEndPanelTreatment(ReadMetadataValue(metadata, "START")),
                TenderWall.NormalizeEndPanelTreatment(ReadMetadataValue(metadata, "END")),
                IsTruthy(ReadMetadataValue(metadata, "BOTTOM")),
                metadata.Count > 0);
        }

        private static OpeningBounds CreateOpeningBounds(Polyline polyline, IReadOnlyDictionary<string, string> metadata)
        {
            GetBounds(polyline, out double minX, out double minY, out double maxX, out double maxY);

            double width = Math.Max(0, maxX - minX);
            double height = Math.Max(0, maxY - minY);
            string openingType = ReadMetadataValue(metadata, "OPENING_TYPE");
            if (string.IsNullOrWhiteSpace(openingType))
            {
                openingType = "Cửa sổ/LKT";
            }

            return new OpeningBounds(minX, minY, maxX, maxY, width, height, openingType);
        }

        private static void GetBounds(Polyline polyline, out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = double.MaxValue;
            minY = double.MaxValue;
            maxX = double.MinValue;
            maxY = double.MinValue;

            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                Point2d point = polyline.GetPoint2dAt(i);
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        private static string ReadMetadataValue(IReadOnlyDictionary<string, string> metadata, string key)
        {
            return metadata.TryGetValue(key, out string? value)
                ? value?.Trim() ?? string.Empty
                : string.Empty;
        }

        private static bool IsTruthy(string rawValue)
        {
            return string.Equals(rawValue, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawValue, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private sealed record TagTextInfo(double X, double Y, string Text);

        private sealed record OpeningBounds(
            double MinX,
            double MinY,
            double MaxX,
            double MaxY,
            double WidthMm,
            double HeightMm,
            string OpeningType)
        {
            public bool IsDoor => string.Equals(OpeningType, "Cửa đi", StringComparison.OrdinalIgnoreCase);

            public double PerimeterMm => IsDoor
                ? WidthMm + (HeightMm * 2.0)
                : (WidthMm + HeightMm) * 2.0;

            public double PerimeterTwoFacesMm => PerimeterMm * 2.0;
        }

        private sealed record PanelEntityInfo(
            double MinX,
            double MinY,
            double MaxX,
            double MaxY,
            double WidthMm,
            double HeightMm,
            double LongSpanMm,
            string PanelId,
            string WallCode,
            string SpecKey,
            string Application,
            string TopPanelTreatment,
            string StartPanelTreatment,
            string EndPanelTreatment,
            bool BottomEdgeEnabled,
            bool HasMetadata);
    }
}
