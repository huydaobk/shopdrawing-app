using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ShopDrawing.Plugin.Models;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ShopDrawing.Plugin.Core
{
    public class BomManager
    {
        private const string TagBlockName = "SD_PANEL_TAG";
        private ObjectId _tableId = ObjectId.Null;

        // Theo dõi các wall đã cleanup trong phiên hiện tại để tránh xóa trùng
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
        /// Khi entity bị xóa: nếu nằm trên layer SD_TAG → xóa tấm lẻ liên quan.
        /// </summary>
        private void OnObjectErased(object? sender, ObjectErasedEventArgs e)
        {
            try
            {
                // Chỉ xử lý khi entity bị xóa (không phải Undo restore)
                if (!e.Erased) return;

                var obj = e.DBObject;
                if (obj == null) return;

                // Khi ObjectErased fire, entity ĐÃ bị xóa (IsErased=true)
                // Nhưng e.DBObject vẫn giữ data trong memory → đọc được Layer, TextString
                if (obj is DBText txt)
                {
                    // Kiểm tra layer — dùng LayerId vì property Layer có thể không truy cập được
                    string layerName = "";
                    try
                    {
                        // Thử đọc trực tiếp
                        layerName = txt.Layer;
                    }
                    catch
                    {
                        // Fallback: đọc qua LayerId
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

                    // Tag text dạng "W1-01" → wall code = "W1"
                    string panelId = "";
                    try { panelId = txt.TextString; } catch { return; }
                    if (string.IsNullOrEmpty(panelId) || !panelId.Contains('-')) return;

                    // Trích wall code: {wallCode}-{seq} → e.g. "W1-01" → "W1"
                    int lastDash = panelId.LastIndexOf('-');
                    if (lastDash <= 0) return;
                    string wallCode = panelId.Substring(0, lastDash); // "W1"

                    // Tránh xóa nhiều lần cùng 1 tường (mỗi tường có nhiều panel tag)
                    if (_cleanedWalls.Contains(wallCode)) return;
                    _cleanedWalls.Add(wallCode);

                    // Xóa tấm lẻ thuộc tường này
                    var repo = Commands.ShopDrawingCommands.WasteRepo;
                    if (repo != null)
                    {
                        int deleted = repo.DeleteBySourceWall(wallCode);
                        if (deleted > 0)
                        {
                            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                            ed?.WriteMessage($"\n🗑️ Đã xóa {deleted} tấm lẻ thuộc tường {wallCode} khỏi kho.");
                        }
                    }

                    // Xóa khỏi tracking set sau 2 giây (cho phép xóa lại nếu Undo → Redo)
                    System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ => _cleanedWalls.Remove(wallCode));
                }
            }
            catch { /* Không crash AutoCAD */ }
        }

        private void OnObjectModified(object? sender, ObjectEventArgs e)
        {
            // Chỉ refresh khi AttributeReference của SD_PANEL_TAG thay đổi
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
                    // TODO Phase sau: SD_INSERT_BOM command (UpdateTable chưa production-ready)
                    // if (rows.Count > 0) UpdateTable(rows, tr, doc.Database);
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

            // === STEP 1: Thu thập tất cả SD_TAG text và SD_PANEL polyline ===
            var tagTexts = new List<(double X, double Y, string Text)>();
            var panelOutlines = new List<(double X, double Y, double W, double H)>();

            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
                if (ent == null) continue;

                // DBText trên layer SD_TAG
                if (ent is DBText txt && txt.Layer == "SD_TAG")
                {
                    var pt = txt.HorizontalMode != TextHorizontalMode.TextLeft
                        ? txt.AlignmentPoint : txt.Position;
                    tagTexts.Add((Math.Round(pt.X, 0), Math.Round(pt.Y, 0), txt.TextString));
                }
                // Polyline trên layer SD_PANEL
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
                    panelOutlines.Add((minX, minY, w, h));
                }
            }

            // === STEP 2: Polyline-first — iterate mỗi polyline, tìm texts bên trong ===
            // Cách cũ group text by X bị lỗi khi 2 panel cùng X (do opening split)
            // Cách mới: mỗi SD_PANEL polyline = 1 panel duy nhất → match text vào polyline
            double margin = 50;
            foreach (var (px, py, pw, ph) in panelOutlines)
            {
                // Tìm tất cả texts nằm BÊN TRONG polyline này
                var textsInside = tagTexts
                    .Where(t => t.X >= px - margin && t.X <= px + pw + margin &&
                                t.Y >= py - margin && t.Y <= py + ph + margin)
                    .OrderByDescending(t => t.Y)
                    .ToList();
                
                if (textsInside.Count < 1) continue; // Ít nhất 1 text (tag+spec gộp)
                
                string panelId = "";
                string spec = "";
                string status = "✦ MỚI";
                string jointLeft = "";
                string jointRight = "";

                foreach (var (_, _, text) in textsInside)
                {
                    if (text.Contains("-") && (text.StartsWith("W") || text.StartsWith("w")))
                    {
                        // Format mới: "W01-12 / Spec1" hoặc "W01-12 ✂ / Spec1"
                        // Tách spec qua delimiter " / "
                        if (text.Contains(" / "))
                        {
                            int slashIdx = text.IndexOf(" / ");
                            string leftPart = text.Substring(0, slashIdx).Trim();
                            spec = text.Substring(slashIdx + 3).Trim();
                            panelId = leftPart.Split(' ')[0].Trim();
                            // Status icon nằm trong leftPart
                            if (leftPart.Contains("✂") || leftPart.Contains("CẮT"))
                                status = "✂ CẮT";
                            else if (leftPart.Contains("♻") || leftPart.Contains("TÁI"))
                                status = "♻ TÁI SỬ DỤNG";
                        }
                        else
                        {
                            // Fallback: format cũ không có spec (hoặc spec rỗng)
                            panelId = text.Split(' ')[0].Trim();
                            if (text.Contains("✂") || text.Contains("CẮT"))
                                status = "✂ CẮT";
                            else if (text.Contains("♻") || text.Contains("TÁI"))
                                status = "♻ TÁI SỬ DỤNG";
                        }
                    }
                    else if (text.Contains("✂") || text.Contains("CẮT"))
                        status = text;
                    else if (text.Contains("♻") || text.Contains("TÁI"))
                        status = text;
                    else if (text.Contains("✦") || text.Contains("MỚI"))
                        status = text;
                    // Fallback: text riêng lẻ (tương thích bản vẽ cũ chưa gộp)
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
                    Spec = spec,
                    Status = status,
                    WallCode = wallCode,
                    JointLeft = jointLeft,
                    JointRight = jointRight,
                    WidthMm = widthMm,
                    LengthMm = lengthMm
                });
            }

            // === STEP 3: Fallback Spec — nếu rỗng thì lấy từ panel cùng WallCode hoặc config ===
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
                       .Select(g => new BomRow {
                           Id = g.Key.Id,
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

            // Setup Header
            tb.SetSize(rows.Count + 2, 5); 
            tb.Cells[0, 0].TextString = "BẢNG THỐNG KÊ TẤM SANDWICH";
            
            string[] headers = { "MÃ TẤM", "CẤU TẠO", "KÍCH THƯỚC (W×L)", "SỐ LƯỢNG", "TRẠNG THÁI" };
            for (int i = 0; i < headers.Length; i++)
            {
                tb.Cells[1, i].TextString = headers[i];
            }

            // Fill Data
            for (int i = 0; i < rows.Count; i++)
            {
                int r = i + 2;
                tb.Cells[r, 0].TextString = rows[i].Id;
                tb.Cells[r, 1].TextString = rows[i].Spec;
                tb.Cells[r, 2].TextString = rows[i].Dims;
                tb.Cells[r, 3].TextString = rows[i].Qty.ToString();
                tb.Cells[r, 4].TextString = rows[i].Status;
            }
            
            tb.GenerateLayout();
        }

    }
}
