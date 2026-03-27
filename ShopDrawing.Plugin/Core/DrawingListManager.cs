using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace ShopDrawing.Plugin.Core
{
    /// <summary>
    /// Quản lý Drawing List (Danh mục bản vẽ) trong Layout tab "SD-DMBV".
    /// AutoCAD Table object, Paper Space, font SD_Arial.
    /// Auto-sync khi thêm/xóa layout.
    /// </summary>
    public static class DrawingListManager
    {
        public const string DMBD_LAYOUT = "SD-DMBV";
        public const string DMBD_PREFIX = LayoutManagerEngine.LAYOUT_PREFIX;

        // ═══════════════════════════════════════════════════
        // SYNC — quét tất cả layout, rebuild bảng
        // ═══════════════════════════════════════════════════

        public static void Sync(Document doc)
        {
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();

            // Lấy danh sách layout (trừ Model và DMBV)
            var entries = GetLayoutEntries(db, tr);

            // Đảm bảo layout DMBV tồn tại
            var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForWrite);
            bool isNewDmbd = !layoutDict.Contains(DMBD_LAYOUT);
            if (isNewDmbd)
                LayoutManager.Current.CreateLayout(DMBD_LAYOUT);

            ObjectId dmbdId = layoutDict.GetAt(DMBD_LAYOUT);
            var dmbdLayout  = (Layout)tr.GetObject(dmbdId, OpenMode.ForWrite);

            // Áp dụng Page Setup cho DMBV — dùng cùng paper size với các layout SD
            string dmbdPaperSize = DetectSdPaperSize(db, tr, layoutDict) ?? "A3";
            LayoutManagerEngine.ApplyPageSetup(dmbdLayout, dmbdPaperSize);

            // Đảm bảo DMBV luôn là tab đầu tiên (sau Model)
            if (dmbdLayout.TabOrder != 1)
                dmbdLayout.TabOrder = 1;

            var psBtr = (BlockTableRecord)tr.GetObject(dmbdLayout.BlockTableRecordId, OpenMode.ForWrite);

            // Xóa Table cũ + frame cũ (polylines trên SHEET_FRAME_LAYER hoặc DMBV_LAYER)
            foreach (ObjectId id in psBtr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent is Table)
                {
                    ent.UpgradeOpen();
                    ((Table)ent).Erase();
                }
                else if (ent is Entity entity && (
                    string.Equals(entity.Layer, LayoutManagerEngine.SHEET_FRAME_LAYER, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entity.Layer, LayoutManagerEngine.DMBD_LAYER, StringComparison.OrdinalIgnoreCase)))
                {
                    ent.UpgradeOpen();
                    entity.Erase();
                }
            }

            // ═══ Constants — dùng cùng margin với các layout thường ═══
            const double marginLeft  = 25;
            const double marginRight = 5;
            const double marginTop   = 5;
            const double marginBot   = 5;
            double paperWidth  = 420; // A3
            double paperHeight = 297;

            // Nếu đã có PlotPaperSize, dùng giá trị thực
            try
            {
                var plotInfo = dmbdLayout.PlotPaperSize;
                if (plotInfo.X > 0 && plotInfo.Y > 0)
                {
                    paperWidth = plotInfo.X;
                    paperHeight = plotInfo.Y;
                }
            }
            catch { /* fallback A3 */ }

            double frameX0 = marginLeft;
            double frameY0 = marginBot;
            double frameX1 = paperWidth - marginRight;
            double frameY1 = paperHeight - marginTop;

            // Đảm bảo layer tồn tại
            LayoutManagerEngine.EnsureTitleLayers(db, tr);

            // ═══ Vẽ border frame ═══
            LayoutManagerEngine.DrawRect(psBtr, tr, frameX0, frameY0, frameX1, frameY1, 0.5,
                LayoutManagerEngine.SHEET_FRAME_LAYER);

            if (entries.Count == 0) { tr.Commit(); return; }

            // ═══ Tạo Table mới ═══
            var table = BuildTable(db, tr, entries);

            // ═══ Căn table vào giữa khu vực usable ═══
            double usableWidth  = frameX1 - frameX0;
            double usableHeight = frameY1 - frameY0;

            // Kích thước thực tế của table
            double tableWidth = 0;
            for (int c = 0; c < table.Columns.Count; c++)
                tableWidth += table.Columns[c].Width;

            double tableHeight = 0;
            for (int r = 0; r < table.Rows.Count; r++)
                tableHeight += table.Rows[r].Height;

            // Tọa độ đặt table (căn giữa frame, Table gốc tọa độ top-left)
            double tableX = frameX0 + (usableWidth - tableWidth) / 2.0;
            double tableY = frameY1 - (usableHeight - tableHeight) / 2.0; // Table origin is top-left

            table.Position = new Point3d(tableX, tableY, 0);
            table.Layer = LayoutManagerEngine.DMBD_LAYER;
            psBtr.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);

            tr.Commit();
        }

        // ═══════════════════════════════════════════════════
        // LẤY DANH SÁCH ENTRY TỪ CÁC LAYOUT
        // ═══════════════════════════════════════════════════

        private static List<DrawingEntry> GetLayoutEntries(Database db, Transaction tr)
        {
            var layouts = GetManagedLayouts(db, tr);

            return layouts
                .Select(x => new
                {
                    x.Name,
                    x.Meta,
                    DrawingNumber = ParseDrawingNumber(x.Meta.SoBanVe)
                })
                .OrderBy(x => x.DrawingNumber == 0 ? int.MaxValue : x.DrawingNumber)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select((x, index) => new DrawingEntry
                {
                    Stt = index + 1,
                    ProjectName = x.Meta.ProjectName,
                    TenBanVe = string.IsNullOrWhiteSpace(x.Meta.DrawingTitle)
                        ? LayoutManagerRules.InferDrawingTitleFromLayoutTabName(x.Name)
                        : x.Meta.DrawingTitle,
                    SoBanVe  = string.IsNullOrWhiteSpace(x.Meta.SoBanVe) ? $"SD-{index + 1:D3}" : x.Meta.SoBanVe,
                    TyLe = LayoutManagerRules.NormalizeScale(x.Meta.TyLe),
                    Ngay     = string.IsNullOrWhiteSpace(x.Meta.Ngay) ? LayoutManagerRules.FormatDate(DateTime.Now) : x.Meta.Ngay,
                    Revision = string.IsNullOrWhiteSpace(x.Meta.Revision) ? "A" : x.Meta.Revision,
                    GhiChu   = x.Meta.GhiChu
                })
                .ToList();
        }

        // ═══════════════════════════════════════════════════
        // META — lưu/đọc thông tin layout vào NOD
        // ═══════════════════════════════════════════════════

        private const string NOD_KEY = "SD_LAYOUT_META";

        public static void SaveLayoutMeta(Document doc, string layoutName, LayoutMeta meta)
        {
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();
            SaveLayoutMeta(db, tr, layoutName, meta);
            tr.Commit();
        }

        public static LayoutMeta GetOrCreateLayoutMeta(
            Document doc,
            string layoutName,
            string scaleText,
            string projectName,
            string drawingTitle = "")
        {
            return PrepareLayoutMeta(doc, layoutName, scaleText, projectName, drawingTitle, string.Empty);
        }

        /// <summary>
        /// Returns the next drawing number (e.g., 1 for "SD-001") WITHOUT writing to the database.
        /// Used to pre-allocate layout tab names that match the future drawing numbers.
        /// </summary>
        public static int PeekNextDrawingNumber(Document doc)
        {
            try
            {
                var db = doc.Database;
                using var tr = db.TransactionManager.StartTransaction();
                int layoutCount = 0;
                int maxNumber = 0;

                foreach (var (_, meta) in GetManagedLayouts(db, tr))
                {
                    layoutCount++;
                    maxNumber = Math.Max(maxNumber, ParseDrawingNumber(meta.SoBanVe));
                }

                tr.Commit();
                return Math.Max(layoutCount, maxNumber) + 1;
            }
            catch
            {
                return 1;
            }
        }

        public static LayoutMeta PrepareLayoutMeta(
            Document doc,
            string layoutName,
            string scaleText,
            string projectName,
            string drawingTitle,
            string revisionContent)
        {
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();

            bool layoutExists = LayoutExists(db, tr, layoutName);
            var existing = ReadLayoutMeta(db, tr, layoutName);
            string resolvedProjectName = string.IsNullOrWhiteSpace(projectName)
                ? (!string.IsNullOrWhiteSpace(existing.ProjectName)
                    ? existing.ProjectName
                    : GetDocumentProjectName(db, tr, layoutName))
                : projectName.Trim();
            string resolvedScale = string.IsNullOrWhiteSpace(scaleText)
                ? LayoutManagerRules.NormalizeScale(existing.TyLe)
                : LayoutManagerRules.NormalizeScale(scaleText);
            string resolvedDrawingTitle = string.IsNullOrWhiteSpace(drawingTitle)
                ? existing.DrawingTitle?.Trim() ?? string.Empty
                : drawingTitle.Trim();

            var meta = new LayoutMeta
            {
                SoBanVe = layoutExists && !string.IsNullOrWhiteSpace(existing.SoBanVe)
                    ? existing.SoBanVe
                    : GetNextDrawingNumber(db, tr, layoutName),
                ProjectName = resolvedProjectName,
                DrawingTitle = resolvedDrawingTitle,
                TyLe = resolvedScale,
                Ngay = LayoutManagerRules.FormatDate(DateTime.Now),
                Revision = layoutExists
                    ? LayoutManagerRules.NextRevision(existing.Revision)
                    : "A",
                GhiChu = revisionContent?.Trim() ?? ""
            };

            SaveLayoutMeta(db, tr, layoutName, meta);
            tr.Commit();
            return meta;
        }

        public static LayoutMeta GetLayoutMeta(Document doc, string layoutName)
        {
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();
            var meta = ReadLayoutMeta(db, tr, layoutName);
            tr.Commit();
            return meta;
        }

        public static string GetDocumentProjectName(Document doc)
        {
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();
            string projectName = GetDocumentProjectName(db, tr);
            tr.Commit();
            return projectName;
        }

        public static IReadOnlyList<string> GetOrderedLayoutNames(Document doc)
        {
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();
            var names = GetManagedLayouts(db, tr)
                .Select(x => new
                {
                    x.Name,
                    DrawingNumber = ParseDrawingNumber(x.Meta.SoBanVe)
                })
                .OrderBy(x => x.DrawingNumber == 0 ? int.MaxValue : x.DrawingNumber)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Name)
                .ToList();

            tr.Commit();
            return names;
        }

        public static bool LayoutExists(Document doc, string layoutName)
        {
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();
            bool exists = LayoutExists(db, tr, layoutName);
            tr.Commit();
            return exists;
        }

        public static void DeleteLayoutMeta(Document doc, string layoutName)
        {
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();

            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(NOD_KEY))
            {
                tr.Commit();
                return;
            }

            var metaDict = (DBDictionary)tr.GetObject(nod.GetAt(NOD_KEY), OpenMode.ForWrite);
            string key = SanitizeKey(layoutName);
            if (metaDict.Contains(key))
            {
                var record = (DBObject)tr.GetObject(metaDict.GetAt(key), OpenMode.ForWrite);
                record.Erase();
                metaDict.Remove(key);
            }

            tr.Commit();
        }

        public static void RenameLayoutMeta(Document doc, string oldLayoutName, string newLayoutName)
        {
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
            if (!nod.Contains(NOD_KEY))
                return;

            var metaDict = (DBDictionary)tr.GetObject(nod.GetAt(NOD_KEY), OpenMode.ForWrite);
            string oldKey = SanitizeKey(oldLayoutName);
            string newKey = SanitizeKey(newLayoutName);

            if (oldKey != newKey && metaDict.Contains(oldKey))
            {
                var oldObjId = metaDict.GetAt(oldKey);
                var record = tr.GetObject(oldObjId, OpenMode.ForWrite);
                metaDict.Remove(oldKey);
                metaDict.SetAt(newKey, record);
            }
            tr.Commit();
        }

        private static LayoutMeta ReadLayoutMeta(Database db, Transaction tr, string layoutName)
        {
            try
            {
                var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (!nod.Contains(NOD_KEY)) return new LayoutMeta();

                var metaDict = (DBDictionary)tr.GetObject(nod.GetAt(NOD_KEY), OpenMode.ForRead);
                string key   = SanitizeKey(layoutName);
                if (!metaDict.Contains(key)) return new LayoutMeta();

                var xr   = (Xrecord)tr.GetObject(metaDict.GetAt(key), OpenMode.ForRead);
                var vals = xr.Data.AsArray().Select(tv => tv.Value?.ToString() ?? "").ToArray();

                return new LayoutMeta
                {
                    SoBanVe  = vals.Length > 0 ? vals[0] : "",
                    ProjectName = vals.Length > 5 ? vals[1] : "",
                    DrawingTitle = vals.Length > 6 ? vals[6] : "",
                    TyLe = LayoutManagerRules.NormalizeScale(
                        vals.Length > 5 ? vals[2] : vals.Length > 1 ? vals[1] : ""),
                    Ngay     = vals.Length > 5 ? vals[3] : vals.Length > 2 ? vals[2] : "",
                    Revision = vals.Length > 5 ? vals[4] : vals.Length > 3 ? vals[3] : "",
                    GhiChu   = vals.Length > 5 ? vals[5] : vals.Length > 4 ? vals[4] : "",
                };
            }
            catch { return new LayoutMeta(); }
        }

        private static void SaveLayoutMeta(Database db, Transaction tr, string layoutName, LayoutMeta meta)
        {
            var metaDict = GetOrCreateMetaDictionary(db, tr);
            string key   = SanitizeKey(layoutName);

            var xRecord = new Xrecord();
            xRecord.Data = new ResultBuffer(
                new TypedValue(1, meta.SoBanVe),
                new TypedValue(1, meta.ProjectName),
                new TypedValue(1, LayoutManagerRules.NormalizeScale(meta.TyLe)),
                new TypedValue(1, meta.Ngay),
                new TypedValue(1, meta.Revision),
                new TypedValue(1, meta.GhiChu),
                new TypedValue(1, meta.DrawingTitle));

            if (metaDict.Contains(key))
            {
                var oldRecord = (DBObject)tr.GetObject(metaDict.GetAt(key), OpenMode.ForWrite);
                oldRecord.Erase();
                metaDict.Remove(key);
            }

            metaDict.SetAt(key, xRecord);
            tr.AddNewlyCreatedDBObject(xRecord, true);
        }

        private static DBDictionary GetOrCreateMetaDictionary(Database db, Transaction tr)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
            if (!nod.Contains(NOD_KEY))
            {
                var inner = new DBDictionary();
                nod.SetAt(NOD_KEY, inner);
                tr.AddNewlyCreatedDBObject(inner, true);
            }

            return (DBDictionary)tr.GetObject(nod.GetAt(NOD_KEY), OpenMode.ForWrite);
        }

        private static bool LayoutExists(Database db, Transaction tr, string layoutName)
        {
            var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            return layoutDict.Contains(layoutName);
        }

        private static List<(string Name, LayoutMeta Meta)> GetManagedLayouts(Database db, Transaction tr)
        {
            var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            var layouts = new List<(string Name, LayoutMeta Meta)>();

            var enumerator = layoutDict.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string name = enumerator.Current.Key;
                // Match all SD- layouts (managed by LayoutManagerEngine)
                bool isManagedLayout = name.StartsWith(DMBD_PREFIX, StringComparison.OrdinalIgnoreCase);

                // Never include DMBV itself
                if (!isManagedLayout || string.Equals(name, DMBD_LAYOUT, StringComparison.OrdinalIgnoreCase))
                    continue;

                layouts.Add((name, ReadLayoutMeta(db, tr, name)));
            }

            return layouts;
        }

        /// <summary>
        /// Detect paper size from existing SD layouts so DMBV uses the same size.
        /// </summary>
        private static string? DetectSdPaperSize(Database db, Transaction tr, DBDictionary layoutDict)
        {
            var enumerator = layoutDict.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string name = enumerator.Current.Key;
                if (!name.StartsWith(DMBD_PREFIX, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, DMBD_LAYOUT, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var layout = (Layout)tr.GetObject(enumerator.Current.Value, OpenMode.ForRead);
                    var plotSize = layout.PlotPaperSize;
                    if (plotSize.X <= 0 || plotSize.Y <= 0) continue;

                    double w = Math.Max(plotSize.X, plotSize.Y);
                    double h = Math.Min(plotSize.X, plotSize.Y);

                    // Match against known paper sizes (tolerance ±5mm)
                    foreach (var kv in LayoutManagerEngine.PaperSizes)
                    {
                        double pw = Math.Max(kv.Value.W, kv.Value.H);
                        double ph = Math.Min(kv.Value.W, kv.Value.H);
                        if (Math.Abs(w - pw) < 5 && Math.Abs(h - ph) < 5)
                            return kv.Key; // "A3", "A2", or "A1"
                    }
                }
                catch { /* skip unreadable layout */ }
            }

            return null; // No SD layouts found → caller falls back to A3
        }

        private static string GetNextDrawingNumber(Database db, Transaction tr, string layoutName)
        {
            int layoutCount = 0;
            int maxNumber = 0;

            foreach (var (existingName, meta) in GetManagedLayouts(db, tr))
            {
                if (string.Equals(existingName, layoutName, StringComparison.OrdinalIgnoreCase))
                    continue;

                layoutCount++;
                maxNumber = Math.Max(maxNumber, ParseDrawingNumber(meta.SoBanVe));
            }

            int next = Math.Max(layoutCount, maxNumber) + 1;
            return $"SD-{next:D3}";
        }

        private static string GetDocumentProjectName(Database db, Transaction tr, string? excludedLayoutName = null)
        {
            return GetManagedLayouts(db, tr)
                .Where(x => !string.Equals(x.Name, excludedLayoutName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x =>
                {
                    int drawingNumber = ParseDrawingNumber(x.Meta.SoBanVe);
                    return drawingNumber == 0 ? int.MaxValue : drawingNumber;
                })
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Meta.ProjectName?.Trim())
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?? "";
        }

        private static int ParseDrawingNumber(string drawingNumber)
        {
            if (string.IsNullOrWhiteSpace(drawingNumber)) return 0;
            if (!drawingNumber.StartsWith("SD-", StringComparison.OrdinalIgnoreCase)) return 0;

            return int.TryParse(drawingNumber[3..], out int value) && value > 0
                ? value
                : 0;
        }

        // ═══════════════════════════════════════════════════
        // XÂY TABLE
        // ═══════════════════════════════════════════════════

        private static Table BuildTable(Database db, Transaction tr, List<DrawingEntry> entries)
        {
            ObjectId styleId = BlockManager.EnsureArialStyle(db, tr);

            var table = new Table();
            string projectName = entries
                .Select(e => e.ProjectName?.Trim())
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?? "";

            int rowCount = entries.Count + 3;
            int colCount = 7;

            table.SetSize(rowCount, colCount);

            double[] colWidths = { 15, 80, 25, 20, 30, 15, 30 };
            for (int c = 0; c < colCount; c++)
                table.SetColumnWidth(c, colWidths[c]);

            table.SetRowHeight(0, 10);
            table.SetRowHeight(1, 8);
            table.SetRowHeight(2, 8);
            for (int r = 3; r < rowCount; r++)
                table.SetRowHeight(r, 7);

            table.MergeCells(CellRange.Create(table, 0, 0, 0, colCount - 1));
            SetCell(table, 0, 0, "DANH MỤC BẢN VẼ", 3.5, styleId, true, CellAlignment.MiddleCenter);

            table.MergeCells(CellRange.Create(table, 1, 0, 1, colCount - 1));
            SetCell(table, 1, 0, projectName.ToUpper(), 2.5, styleId, true, CellAlignment.MiddleCenter);

            string[] headers = { "STT", "TÊN BẢN VẼ", "SỐ BV", "TỶ LỆ", "NGÀY", "REV", "GHI CHÚ" };
            for (int c = 0; c < colCount; c++)
                SetCell(table, 2, c, headers[c], 2.5, styleId, true, CellAlignment.MiddleCenter);

            for (int i = 0; i < entries.Count; i++)
            {
                int r = i + 3;
                var e = entries[i];
                SetCell(table, r, 0, e.Stt.ToString(), 2.5, styleId, false);
                SetCell(table, r, 1, e.TenBanVe, 2.5, styleId, false);
                SetCell(table, r, 2, e.SoBanVe, 2.5, styleId, false);
                SetCell(table, r, 3, e.TyLe, 2.5, styleId, false);
                SetCell(table, r, 4, e.Ngay, 2.5, styleId, false);
                SetCell(table, r, 5, e.Revision, 2.5, styleId, false);
                SetCell(table, r, 6, e.GhiChu, 2.5, styleId, false);
            }

            return table;
        }

        private static void SetCell(Table tbl, int row, int col, string text, double height,
                                    ObjectId styleId, bool bold,
                                    CellAlignment alignment = CellAlignment.MiddleLeft)
        {
            tbl.Cells[row, col].TextString = text;
            tbl.Cells[row, col].TextHeight = height;
            tbl.Cells[row, col].TextStyleId = styleId;
            tbl.Cells[row, col].Alignment  = alignment;
        }

        private static string SanitizeKey(string name) =>
            new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        // ═══════════════════════════════════════════════════
        // DATA MODELS
        // ═══════════════════════════════════════════════════

        public record DrawingEntry
        {
            public int    Stt      { get; init; }
            public string ProjectName { get; init; } = "";
            public string TenBanVe { get; init; } = "";
            public string SoBanVe  { get; init; } = "";
            public string TyLe     { get; init; } = "";
            public string Ngay     { get; init; } = "";
            public string Revision { get; init; } = "";
            public string GhiChu   { get; init; } = "";
        }

        public record LayoutMeta
        {
            public string SoBanVe  { get; init; } = "";
            public string ProjectName { get; init; } = "";
            public string DrawingTitle { get; init; } = "";
            public string TyLe     { get; init; } = "";
            public string Ngay     { get; init; } = "";
            public string Revision { get; init; } = "";
            public string GhiChu   { get; init; } = "";
        }
    }
}
