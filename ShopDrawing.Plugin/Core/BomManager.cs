using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.Modules.Accessories;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ShopDrawing.Plugin.Core
{
    public class BomManager
    {
        private const string TagBlockName = "SD_PANEL_TAG";
        private ObjectId _tableId = ObjectId.Null;

        // Theo doi cac wall da cleanup trong phien hien tai de tranh xoa trung
        private readonly HashSet<string> _cleanedWalls = new();

        public void RegisterReactor()
        {
            try
            {
                Database db = HostApplicationServices.WorkingDatabase;
                if (db != null)
                {
                    db.ObjectModified += OnObjectModified;
                    db.ObjectErased += OnObjectErased;
                }
            }
            catch (System.Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\nError registering BOM reactor: {ex.Message}");
            }
        }

        /// <summary>
        /// Khi entity bi xoa: neu nam tren layer SD_TAG thi xoa tam le lien quan.
        /// </summary>
        private void OnObjectErased(object? sender, ObjectErasedEventArgs e)
        {
            try
            {
                if (!e.Erased) return;

                var obj = e.DBObject;
                if (obj == null) return;

                if (obj is DBText txt)
                {
                    string layerName = "";
                    try
                    {
                        layerName = txt.Layer;
                    }
                    catch
                    {
                        try
                        {
                            using (var tr = txt.Database.TransactionManager.StartOpenCloseTransaction())
                            {
                                if (tr.GetObject(txt.LayerId, OpenMode.ForRead) is LayerTableRecord ltr)
                                    layerName = ltr.Name;
                            }
                        }
                        catch { return; }
                    }

                    if (layerName != "SD_TAG") return;

                    string panelId = "";
                    try { panelId = txt.TextString; } catch { return; }
                    if (string.IsNullOrEmpty(panelId) || !panelId.Contains('-')) return;

                    int lastDash = panelId.LastIndexOf('-');
                    if (lastDash <= 0) return;
                    string wallCode = panelId.Substring(0, lastDash);

                    if (_cleanedWalls.Contains(wallCode)) return;
                    _cleanedWalls.Add(wallCode);

                    var repo = Commands.ShopDrawingCommands.WasteRepo;
                    if (repo != null)
                    {
                        int deleted = repo.DeleteBySourceWall(wallCode);
                        if (deleted > 0)
                        {
                            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                            ed?.WriteMessage($"\nÃ°Å¸â€”â€˜Ã¯Â¸Â Ã„ÂÃƒÂ£ xÃƒÂ³a {deleted} tÃ¡ÂºÂ¥m lÃ¡ÂºÂ» thuÃ¡Â»â„¢c tÃ†Â°Ã¡Â»Âng {wallCode} khÃ¡Â»Âi kho.");
                        }
                    }

                    System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ => _cleanedWalls.Remove(wallCode));
                }
            }
            catch { }
        }

        private void OnObjectModified(object? sender, ObjectEventArgs e)
        {
            if (e.DBObject is AttributeReference attr)
            {
                if (IsShopDrawingAttribute(attr.Tag))
                {
                    Refresh();
                }
            }
        }

        private bool IsShopDrawingAttribute(string tag)
        {
            var allowedTags = new[] { "PANEL_ID", "SPEC", "DIMENSIONS", "THICKNESS", "STATUS", "SOURCE_ID" };
            return allowedTags.Contains(tag);
        }

        public void Refresh()
        {
            Document? doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var rows = ScanDocumentForPanels(tr, doc.Database);
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage($"\nBOM Refresh Error: {ex.Message}");
            }
        }

        public List<BomRow> ScanDocumentForPanels(Transaction tr, Database db)
        {
            var data = new List<BomItem>();

            if (tr.GetObject(db.BlockTableId, OpenMode.ForRead) is not BlockTable bt)
                return new List<BomRow>();

            if (tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) is not BlockTableRecord ms)
                return new List<BomRow>();

            var tagTexts = new List<(double X, double Y, string Text)>();
            var panelOutlines = new List<(double X, double Y, double W, double H)>();

            foreach (ObjectId id in ms)
            {
                if (id.IsErased) continue;

                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                if (ent is DBText txt && txt.Layer == "SD_TAG")
                {
                    var pt = txt.HorizontalMode != TextHorizontalMode.TextLeft
                        ? txt.AlignmentPoint : txt.Position;
                    tagTexts.Add((Math.Round(pt.X, 0), Math.Round(pt.Y, 0), txt.TextString));
                }
                else if (ent is Polyline pl && pl.Layer == "SD_PANEL" && pl.Closed && pl.NumberOfVertices >= 4)
                {
                    double minX = double.MaxValue, maxX = double.MinValue;
                    double minY = double.MaxValue, maxY = double.MinValue;
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                    {
                        var v = pl.GetPoint2dAt(i);
                        if (v.X < minX) minX = v.X;
                        if (v.X > maxX) maxX = v.X;
                        if (v.Y < minY) minY = v.Y;
                        if (v.Y > maxY) maxY = v.Y;
                    }

                    double w = Math.Round(maxX - minX, 0);
                    double h = Math.Round(maxY - minY, 0);
                    if (TryBuildBomItemFromMetadata(pl, w, h, out BomItem? metadataItem) && metadataItem != null)
                    {
                        data.Add(metadataItem);
                    }
                    else
                    {
                        panelOutlines.Add((minX, minY, w, h));
                    }
                }
            }

            foreach (var (px, py, pw, ph) in panelOutlines)
            {
                var textsForPanel = FindTextsForPanel(tagTexts, px, py, pw, ph);
                if (textsForPanel.Count < 1) continue;

                string panelId = "";
                string spec = "";
                string status = "\u2726 M\u1edaI";
                string jointLeft = "";
                string jointRight = "";

                foreach (var (_, _, text) in textsForPanel)
                {
                    ParsedPanelTag? primaryPanelTag = TryParsePanelTagText(text);
                    if (primaryPanelTag == null)
                    {
                        continue;
                    }

                    panelId = primaryPanelTag.PanelId;
                    spec = primaryPanelTag.Spec;
                    status = primaryPanelTag.Status;
                    break;
                }

                foreach (var (_, _, text) in textsForPanel)
                {
                    ParsedPanelTag? parsedTag = TryParsePanelTagText(text);
                    if (parsedTag != null)
                    {
                        if (string.IsNullOrEmpty(panelId))
                        {
                            panelId = parsedTag.PanelId;
                        }

                        if (string.IsNullOrEmpty(spec) && !string.IsNullOrEmpty(parsedTag.Spec))
                        {
                            spec = parsedTag.Spec;
                        }

                        if (status == "\u2726 M\u1edaI" && parsedTag.Status != "\u2726 M\u1edaI")
                        {
                            status = parsedTag.Status;
                        }
                    }
                    else if (text.Contains('\u2702') || text.Contains("C\u1eaeT", StringComparison.OrdinalIgnoreCase) || text.Contains("CAT", StringComparison.OrdinalIgnoreCase))
                        status = text;
                    else if (text.Contains('\u267b') || text.Contains("T\u00c1I", StringComparison.OrdinalIgnoreCase) || text.Contains("TAI", StringComparison.OrdinalIgnoreCase))
                        status = text;
                    else if (text.Contains('\u2726') || text.Contains("M\u1edaI", StringComparison.OrdinalIgnoreCase) || text.Contains("MOI", StringComparison.OrdinalIgnoreCase))
                        status = text;
                    else if (!string.IsNullOrWhiteSpace(text) && string.IsNullOrEmpty(spec)
                             && text != "+" && text != "-" && text != "0"
                             && text.Length > 1)
                        spec = text;
                }

                if (string.IsNullOrEmpty(panelId)) continue;

                string wallCode = "";
                var parts = panelId.Split('-');
                if (parts.Length >= 1) wallCode = parts[0];

                double widthMm = Math.Min(pw, ph);
                double lengthMm = Math.Max(pw, ph);

                data.Add(new BomItem
                {
                    Id = panelId,
                    DisplayId = panelId,
                    Spec = spec,
                    Status = status,
                    WallCode = wallCode,
                    JointLeft = jointLeft,
                    JointRight = jointRight,
                    WidthMm = widthMm,
                    LengthMm = lengthMm
                });
            }

            string globalSpec = ShopDrawing.Plugin.Commands.ShopDrawingCommands.DefaultSpec;
            if (string.IsNullOrEmpty(globalSpec))
            {
                var allSpecs = ShopDrawing.Plugin.Commands.ShopDrawingCommands.SpecManager?.GetAll();
                if (allSpecs != null && allSpecs.Count > 0)
                    globalSpec = allSpecs[0].Key;
            }

            foreach (var item in data)
            {
                if (string.IsNullOrEmpty(item.Spec))
                {
                    var donor = data.FirstOrDefault(d => d.WallCode == item.WallCode && !string.IsNullOrEmpty(d.Spec));
                    if (donor != null)
                        item.Spec = donor.Spec;
                    else if (!string.IsNullOrEmpty(globalSpec))
                        item.Spec = globalSpec;
                }
            }

            return data.GroupBy(x => new { x.Id, x.Spec, x.Status, x.WallCode, x.JointLeft, x.JointRight, x.WidthMm, x.LengthMm })
                       .Select(g => new BomRow
                       {
                           Id = g.Key.Id,
                           DisplayId = g.First().DisplayId,
                           Spec = g.Key.Spec,
                           Status = g.Key.Status,
                           WallCode = g.Key.WallCode,
                           JointLeft = g.Key.JointLeft,
                           JointRight = g.Key.JointRight,
                           WidthMm = g.Key.WidthMm,
                           LengthMm = g.Key.LengthMm,
                           Qty = g.Count()
                       })
                       .OrderBy(r => r.Id)
                       .ToList();
        }

        private static bool TryBuildBomItemFromMetadata(Polyline polyline, double widthMm, double lengthMm, out BomItem? item)
        {
            item = null;

            IReadOnlyDictionary<string, string> metadata = ShopdrawingEntityMetadata.Read(polyline);
            if (!metadata.TryGetValue("PANEL_ID", out string? panelId) || string.IsNullOrWhiteSpace(panelId))
            {
                return false;
            }

            string wallCode = TryGetMetadataValue(metadata, "WALL_CODE");
            if (string.IsNullOrWhiteSpace(wallCode))
            {
                wallCode = ResolveWallCode(panelId);
            }

            item = new BomItem
            {
                Id = panelId.Trim(),
                DisplayId = ResolveDisplayPanelId(metadata, panelId),
                Spec = TryGetMetadataValue(metadata, "SPEC"),
                Status = ResolveStatusFromMetadata(metadata),
                WallCode = wallCode,
                JointLeft = string.Empty,
                JointRight = string.Empty,
                WidthMm = Math.Min(widthMm, lengthMm),
                LengthMm = Math.Max(widthMm, lengthMm)
            };

            return true;
        }

        private static string ResolveStatusFromMetadata(IReadOnlyDictionary<string, string> metadata)
        {
            if (IsMetadataFlagEnabled(metadata, "IS_CUT"))
            {
                return "\u2702 C\u1eaeT";
            }

            if (IsMetadataFlagEnabled(metadata, "IS_REUSED"))
            {
                return "\u267b T\u00c1I S\u1eec D\u1ee4NG";
            }

            return "\u2726 M\u1edaI";
        }

        private static string ResolveDisplayPanelId(IReadOnlyDictionary<string, string> metadata, string panelId)
        {
            string normalizedPanelId = panelId.Trim();
            if (!IsMetadataFlagEnabled(metadata, "IS_REUSED"))
            {
                return normalizedPanelId;
            }

            string sourceId = TryGetMetadataValue(metadata, "SOURCE_ID");
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return normalizedPanelId;
            }

            string[] suffixes = { "-REM", "-STEP", "-OPEN", "-TRIM" };
            foreach (string suffix in suffixes)
            {
                if (sourceId.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return sourceId.Substring(0, sourceId.Length - suffix.Length);
                }
            }

            return sourceId;
        }

        private static bool IsMetadataFlagEnabled(IReadOnlyDictionary<string, string> metadata, string key)
        {
            return metadata.TryGetValue(key, out string? value)
                && (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        }

        private static string TryGetMetadataValue(IReadOnlyDictionary<string, string> metadata, string key)
        {
            return metadata.TryGetValue(key, out string? value) ? value?.Trim() ?? string.Empty : string.Empty;
        }

        private static string ResolveWallCode(string panelId)
        {
            if (string.IsNullOrWhiteSpace(panelId))
            {
                return string.Empty;
            }

            string[] parts = panelId.Split('-');
            return parts.Length >= 1 ? parts[0] : string.Empty;
        }

        private static ParsedPanelTag? TryParsePanelTagText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !text.Contains('-'))
            {
                return null;
            }

            string trimmed = text.Trim();
            if (!trimmed.StartsWith("W", StringComparison.OrdinalIgnoreCase)
                && !trimmed.StartsWith("C", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string panelId = trimmed.Split(' ')[0].Trim();
            string spec = string.Empty;
            if (trimmed.Contains(" / "))
            {
                int slashIdx = trimmed.IndexOf(" / ", StringComparison.Ordinal);
                string leftPart = trimmed.Substring(0, slashIdx).Trim();
                panelId = leftPart.Split(' ')[0].Trim();
                spec = trimmed.Substring(slashIdx + 3).Trim();
            }

            string status = "\u2726 M\u1edaI";
            if (trimmed.Contains('\u2702') || trimmed.Contains("C\u1eaeT", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("CAT", StringComparison.OrdinalIgnoreCase))
            {
                status = "\u2702 C\u1eaeT";
            }
            else if (trimmed.Contains('\u267b') || trimmed.Contains("T\u00c1I", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("TAI", StringComparison.OrdinalIgnoreCase))
            {
                status = "\u267b T\u00c1I S\u1eec D\u1ee4NG";
            }

            return new ParsedPanelTag(panelId, spec, status);
        }

        private static List<(double X, double Y, string Text)> FindTextsForPanel(
            IReadOnlyList<(double X, double Y, string Text)> tagTexts,
            double px,
            double py,
            double pw,
            double ph)
        {
            const double insideMargin = 50;
            double centerX = px + (pw / 2.0);
            double centerY = py + (ph / 2.0);

            var insideTexts = tagTexts
                .Where(t => t.X >= px - insideMargin && t.X <= px + pw + insideMargin &&
                            t.Y >= py - insideMargin && t.Y <= py + ph + insideMargin)
                .OrderBy(t =>
                {
                    double dx = t.X - centerX;
                    double dy = t.Y - centerY;
                    return (dx * dx) + (dy * dy);
                })
                .ToList();

            if (insideTexts.Count > 0)
            {
                return insideTexts;
            }

            bool looksHorizontal = pw >= ph;
            double outerMarginX = looksHorizontal
                ? Math.Max(600, pw * 0.20)
                : Math.Max(1800, pw * 0.75);
            double outerMarginY = looksHorizontal
                ? Math.Max(1800, ph * 3.0)
                : Math.Max(600, ph * 0.20);

            return tagTexts
                .Where(t =>
                    t.X >= px - outerMarginX && t.X <= px + pw + outerMarginX &&
                    t.Y >= py - outerMarginY && t.Y <= py + ph + outerMarginY)
                .OrderBy(t =>
                {
                    double dx = t.X - centerX;
                    double dy = t.Y - centerY;
                    return (dx * dx) + (dy * dy);
                })
                .Take(6)
                .ToList();
        }

        private sealed record ParsedPanelTag(string PanelId, string Spec, string Status);

        private void UpdateTable(List<BomRow> rows, Transaction tr, Database db)
        {
            if (tr.GetObject(db.BlockTableId, OpenMode.ForRead) is not BlockTable bt) return;
            if (tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) is not BlockTableRecord ms) return;

            Table tb;
            if (_tableId.IsNull || _tableId.IsErased)
            {
                tb = new Table();
                tb.SetDatabaseDefaults();
                tb.TableStyle = db.Tablestyle;
                tb.Position = new Point3d(0, 0, 0);
                ms.AppendEntity(tb);
                tr.AddNewlyCreatedDBObject(tb, true);
                _tableId = tb.ObjectId;
            }
            else
            {
                tb = (Table)tr.GetObject(_tableId, OpenMode.ForWrite);
            }

            tb.SetSize(rows.Count + 2, 5);
            tb.Cells[0, 0].TextString = "BÃ¡ÂºÂ¢NG THÃ¡Â»ÂNG KÃƒÅ  TÃ¡ÂºÂ¤M SANDWICH";

            string[] headers = { "MÃƒÆ’ TÃ¡ÂºÂ¤M", "CÃ¡ÂºÂ¤U TÃ¡ÂºÂ O", "KÃƒÂCH THÃ†Â¯Ã¡Â»Å¡C (WÃƒâ€”L)", "SÃ¡Â»Â LÃ†Â¯Ã¡Â»Â¢NG", "TRÃ¡ÂºÂ NG THÃƒÂI" };
            for (int i = 0; i < headers.Length; i++)
            {
                tb.Cells[1, i].TextString = headers[i];
            }

            for (int i = 0; i < rows.Count; i++)
            {
                int r = i + 2;
                tb.Cells[r, 0].TextString = string.IsNullOrWhiteSpace(rows[i].DisplayId) ? rows[i].Id : rows[i].DisplayId;
                tb.Cells[r, 1].TextString = rows[i].Spec;
                tb.Cells[r, 2].TextString = rows[i].Dims;
                tb.Cells[r, 3].TextString = rows[i].Qty.ToString();
                tb.Cells[r, 4].TextString = rows[i].Status;
            }

            tb.GenerateLayout();
        }
    }
}
