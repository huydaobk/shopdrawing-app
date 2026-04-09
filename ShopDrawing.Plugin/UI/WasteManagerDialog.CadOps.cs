using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.Modules.Accessories;
using Exception = System.Exception;

namespace ShopDrawing.Plugin.UI
{
    public partial class WasteManagerDialog
    {
        private ObjectId? _previewHighlightPolylineId = null;
        private const string HIGHLIGHT_LAYER_NAME = "SD_HIGHLIGHT";

        private void OnWasteSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_wasteGrid.SelectedItem is WastePanel selectedWaste)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    HighlightWasteParent(selectedWaste);
                }));
            }
            else
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ClearHighlight));
            }
        }

        private void HighlightWasteParent(WastePanel waste)
        {
            ClearHighlight();

            string exactPanelId = waste.PanelCode?.Trim() ?? string.Empty;
            string normalizedPanelId = NormalizeWastePanelCode(waste);
            if (string.IsNullOrWhiteSpace(exactPanelId) && string.IsNullOrWhiteSpace(normalizedPanelId))
            {
                return;
            }

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var db = doc.Database;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    Extents3d? panelExtents = TryResolvePanelExtents(db, tr, modelSpace, waste, exactPanelId, normalizedPanelId);

                    if (!panelExtents.HasValue)
                    {
                        tr.Commit();
                        return;
                    }

                    ObjectId layerId = EnsureHighlightLayer(db, tr);
                    var highlightPolyline = BuildHighlightPolyline(panelExtents.Value, layerId);

                    double viewSize = Convert.ToDouble(AcApp.GetSystemVariable("VIEWSIZE"));
                    highlightPolyline.ConstantWidth = Math.Max(viewSize * 0.005, 10.0);

                    ObjectId addedId = modelSpace.AppendEntity(highlightPolyline);
                    tr.AddNewlyCreatedDBObject(highlightPolyline, true);

                    highlightPolyline.Highlight();
                    _previewHighlightPolylineId = addedId;
                    ZoomToExtents(doc, panelExtents.Value);
                }
                catch (Exception ex)
                {
                    PluginLogger.Error("Lỗi khi highlight tấm cha của hao hụt", ex);
                }

                tr.Commit();
            }
        }

        private static Extents3d? TryResolvePanelExtents(
            Database db,
            Transaction tr,
            BlockTableRecord modelSpace,
            WastePanel waste,
            string exactPanelId,
            string normalizedPanelId)
        {
            string preferredPanelId = !string.IsNullOrWhiteSpace(normalizedPanelId) ? normalizedPanelId : exactPanelId;
            if (!string.IsNullOrWhiteSpace(preferredPanelId))
            {
                Extents3d? metadataExtents = TryFindPanelExtentsFromMetadata(tr, modelSpace, preferredPanelId, waste);
                if (metadataExtents.HasValue)
                {
                    return metadataExtents;
                }
            }

            if (!string.IsNullOrWhiteSpace(exactPanelId))
            {
                Extents3d? exactExtents = TryFindPanelExtentsFromGroup(db, tr, exactPanelId)
                    ?? TryFindPanelExtentsFromTags(tr, modelSpace, exactPanelId);
                if (exactExtents.HasValue)
                {
                    return exactExtents;
                }
            }

            if (!string.IsNullOrWhiteSpace(normalizedPanelId)
                && !string.Equals(normalizedPanelId, exactPanelId, StringComparison.OrdinalIgnoreCase))
            {
                return TryFindPanelExtentsFromGroup(db, tr, normalizedPanelId)
                    ?? TryFindPanelExtentsFromTags(tr, modelSpace, normalizedPanelId);
            }

            return null;
        }

        private static Extents3d? TryFindPanelExtentsFromMetadata(
            Transaction tr,
            BlockTableRecord modelSpace,
            string panelId,
            WastePanel waste)
        {
            var candidates = new List<(Extents3d Extents, double Score, bool StrongMatch)>();

            foreach (ObjectId id in modelSpace)
            {
                if (id.IsErased)
                {
                    continue;
                }

                if (tr.GetObject(id, OpenMode.ForRead) is not Polyline polyline
                    || !polyline.Closed
                    || !string.Equals(polyline.Layer, "SD_PANEL", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                IReadOnlyDictionary<string, string> metadata = ShopdrawingEntityMetadata.Read(polyline);
                if (!metadata.TryGetValue("PANEL_ID", out string? candidatePanelId)
                    || !string.Equals(candidatePanelId?.Trim(), panelId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryMatchPanelLocation(metadata, waste, out double locationScore))
                {
                    candidates.Add((polyline.GeometricExtents, locationScore, true));
                    continue;
                }

                if (!TryBuildMetadataScore(polyline, metadata, waste, out double score, out bool strongMatch))
                {
                    continue;
                }

                candidates.Add((polyline.GeometricExtents, score, strongMatch));
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var strongCandidates = candidates
                .Where(candidate => candidate.StrongMatch)
                .OrderBy(candidate => candidate.Score)
                .ToList();

            if (strongCandidates.Count > 0)
            {
                return strongCandidates[0].Extents;
            }

            return candidates.Count == 1 ? candidates[0].Extents : null;
        }

        private static bool TryMatchPanelLocation(
            IReadOnlyDictionary<string, string> metadata,
            WastePanel waste,
            out double score)
        {
            score = 0;

            if (!waste.SourcePanelX.HasValue || !waste.SourcePanelY.HasValue)
            {
                return false;
            }

            if (!TryParseMetadataDouble(metadata, "PANEL_X", out double panelX)
                || !TryParseMetadataDouble(metadata, "PANEL_Y", out double panelY))
            {
                return false;
            }

            score = Math.Abs(panelX - waste.SourcePanelX.Value) + Math.Abs(panelY - waste.SourcePanelY.Value);
            return score <= 5.0;
        }

        private static string NormalizeWastePanelCode(WastePanel waste)
        {
            string panelCode = waste.PanelCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(panelCode))
            {
                return string.Empty;
            }

            string[] removableSuffixes = { "REM", "STEP", "OPEN", "TRIM" };
            while (true)
            {
                int lastDash = panelCode.LastIndexOf('-');
                if (lastDash <= 0)
                {
                    return panelCode;
                }

                string suffix = panelCode[(lastDash + 1)..];
                if (!removableSuffixes.Any(x => string.Equals(x, suffix, StringComparison.OrdinalIgnoreCase)))
                {
                    return panelCode;
                }

                panelCode = panelCode[..lastDash];
            }
        }

        private static Extents3d? TryFindPanelExtentsFromGroup(Database db, Transaction tr, string panelId)
        {
            var groupDictionary = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry entry in groupDictionary)
            {
                if (tr.GetObject(entry.Value, OpenMode.ForRead) is not Group group)
                {
                    continue;
                }

                if (!string.Equals(group.Description?.Trim(), panelId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var entityIds = group.GetAllEntityIds()
                    .Cast<ObjectId>()
                    .Where(id => tr.GetObject(id, OpenMode.ForRead) is Entity entity
                        && string.Equals(entity.Layer, "SD_PANEL", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (entityIds.Count == 0)
                {
                    entityIds = group.GetAllEntityIds().Cast<ObjectId>().ToList();
                }

                return MergeEntityExtents(tr, entityIds);
            }

            return null;
        }

        private static Extents3d? TryFindPanelExtentsFromTags(Transaction tr, BlockTableRecord modelSpace, string panelId)
        {
            var tagPoints = new List<Point3d>();
            var panelPolylines = new List<Polyline>();

            foreach (ObjectId id in modelSpace)
            {
                if (id.IsErased) continue;
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity entity)
                {
                    continue;
                }

                if (entity is DBText text
                    && string.Equals(text.Layer, "SD_TAG", StringComparison.OrdinalIgnoreCase)
                    && (text.TextString?.TrimStart().StartsWith(panelId, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    tagPoints.Add(text.HorizontalMode != TextHorizontalMode.TextLeft ? text.AlignmentPoint : text.Position);
                    continue;
                }

                if (entity is Polyline polyline
                    && string.Equals(polyline.Layer, "SD_PANEL", StringComparison.OrdinalIgnoreCase)
                    && polyline.Closed)
                {
                    panelPolylines.Add(polyline);
                }
            }

            if (tagPoints.Count == 0 || panelPolylines.Count == 0)
            {
                return null;
            }

            Polyline? bestMatch = null;
            double bestDistance = double.MaxValue;

            foreach (Point3d tagPoint in tagPoints)
            {
                foreach (Polyline panelPolyline in panelPolylines)
                {
                    Point3d closestPoint = panelPolyline.GetClosestPointTo(tagPoint, false);
                    double distance = tagPoint.DistanceTo(closestPoint);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestMatch = panelPolyline;
                    }
                }
            }

            return bestMatch?.GeometricExtents;
        }

        private static bool TryBuildMetadataScore(
            Polyline polyline,
            IReadOnlyDictionary<string, string> metadata,
            WastePanel waste,
            out double score,
            out bool strongMatch)
        {
            score = 0;
            strongMatch = false;

            if (!string.Equals(waste.SourceType, "STEP", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!TryParseMetadataDouble(metadata, "STEP_W", out double stepWidth)
                || !TryParseMetadataDouble(metadata, "STEP_H", out double stepHeight)
                || stepWidth <= 0
                || stepHeight <= 0)
            {
                return false;
            }

            strongMatch = true;
            score = Math.Abs(stepWidth - waste.WidthMm) + Math.Abs(stepHeight - waste.LengthMm);

            try
            {
                Extents3d extents = polyline.GeometricExtents;
                double centerBias = Math.Abs((extents.MinPoint.X + extents.MaxPoint.X) / 2.0)
                    + Math.Abs((extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0);
                score += centerBias * 0.000001;
            }
            catch
            {
                // Shape score is enough. Ignore bias failures.
            }

            return true;
        }

        private static bool TryParseMetadataDouble(
            IReadOnlyDictionary<string, string> metadata,
            string key,
            out double value)
        {
            value = 0;
            if (!metadata.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return double.TryParse(
                raw,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }

        private static Extents3d? MergeEntityExtents(Transaction tr, IEnumerable<ObjectId> entityIds)
        {
            Extents3d? merged = null;
            foreach (ObjectId entityId in entityIds)
            {
                if (tr.GetObject(entityId, OpenMode.ForRead) is not Entity entity)
                {
                    continue;
                }

                try
                {
                    Extents3d extents = entity.GeometricExtents;
                    if (!merged.HasValue)
                    {
                        merged = extents;
                    }
                    else
                    {
                        Extents3d current = merged.Value;
                        current.AddExtents(extents);
                        merged = current;
                    }
                }
                catch
                {
                    // Skip entities without extents.
                }
            }

            return merged;
        }

        private static Polyline BuildHighlightPolyline(Extents3d extents, ObjectId layerId)
        {
            var polyline = new Polyline();
            polyline.AddVertexAt(0, new Point2d(extents.MinPoint.X, extents.MinPoint.Y), 0, 0, 0);
            polyline.AddVertexAt(1, new Point2d(extents.MaxPoint.X, extents.MinPoint.Y), 0, 0, 0);
            polyline.AddVertexAt(2, new Point2d(extents.MaxPoint.X, extents.MaxPoint.Y), 0, 0, 0);
            polyline.AddVertexAt(3, new Point2d(extents.MinPoint.X, extents.MaxPoint.Y), 0, 0, 0);
            polyline.Closed = true;
            polyline.LayerId = layerId;
            return polyline;
        }

        private static void ZoomToExtents(Autodesk.AutoCAD.ApplicationServices.Document doc, Extents3d extents)
        {
            double width = Math.Max(extents.MaxPoint.X - extents.MinPoint.X, 100.0);
            double height = Math.Max(extents.MaxPoint.Y - extents.MinPoint.Y, 100.0);

            var view = doc.Editor.GetCurrentView();
            view.CenterPoint = new Point2d(
                (extents.MinPoint.X + extents.MaxPoint.X) / 2.0,
                (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0);
            view.Width = width * 1.8;
            view.Height = height * 1.8;
            doc.Editor.SetCurrentView(view);
        }

        private void ClearHighlight()
        {
            if (!_previewHighlightPolylineId.HasValue)
            {
                return;
            }

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var db = doc.Database;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    if (!_previewHighlightPolylineId.Value.IsErased)
                    {
                        var entity = tr.GetObject(_previewHighlightPolylineId.Value, OpenMode.ForWrite) as Entity;
                        if (entity != null && !entity.IsErased)
                        {
                            entity.Unhighlight();
                            entity.Erase();
                        }
                    }
                }
                catch
                {
                    // Ignore clear failures to prevent crashes while closing/canceling.
                }

                tr.Commit();
            }

            _previewHighlightPolylineId = null;
        }

        private ObjectId EnsureHighlightLayer(Database db, Transaction tr)
        {
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (layerTable.Has(HIGHLIGHT_LAYER_NAME))
            {
                var layerTableRecord = (LayerTableRecord)tr.GetObject(layerTable[HIGHLIGHT_LAYER_NAME], OpenMode.ForRead);
                return layerTableRecord.ObjectId;
            }

            layerTable.UpgradeOpen();
            var newLayer = new LayerTableRecord
            {
                Name = HIGHLIGHT_LAYER_NAME,
                Color = Color.FromColorIndex(ColorMethod.ByAci, 2)
            };

            ObjectId layerId = layerTable.Add(newLayer);
            tr.AddNewlyCreatedDBObject(newLayer, true);
            return layerId;
        }
    }
}
