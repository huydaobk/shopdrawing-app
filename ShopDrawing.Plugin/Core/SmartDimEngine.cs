using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Internal;
using ShopDrawing.Plugin.Commands;

namespace ShopDrawing.Plugin.Core
{
    /// <summary>
    /// Smart Dimension Engine v1.0 — Auto & Manual dim for shop drawings.
    /// PRD: SmartDim_PRD_v1.md
    /// </summary>
    public class SmartDimEngine
    {
        // ── Layers ──
        public const string DIM_LAYER_PANEL   = "SD_DIM_PANEL";
        public const string DIM_LAYER_OPENING = "SD_DIM_OPENING";
        public const string DIM_STYLE         = "SD_DIM_STYLE";
        public const string XDATA_APP         = "SD_DIM_APP";

        // ═══════════════════════════════════════════════════
        // LAYER + STYLE SETUP
        // ═══════════════════════════════════════════════════

        public static void EnsureDimLayer(Database db, Transaction tr, string layerName, short colorIndex)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(layerName)) return;

            lt.UpgradeOpen();
            var layer = new LayerTableRecord
            {
                Name  = layerName,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                            Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex)
            };
            lt.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
        }

        /// <summary>
        /// Tạo hoặc CẬP NHẬT DimStyle (P2 — update existing thay vì skip).
        /// Text height = DefaultTextHeightMm × scaleFactor (đồng bộ TAG).
        /// </summary>
        /// <summary>
        /// Returns the ObjectId of the Small Blank Dot block used as dimension arrowhead.
        /// "_SMALL" is AutoCAD's built-in small hollow dot arrowhead.
        /// We retrieve it from the block table (AutoCAD creates it on demand when first referenced).
        /// </summary>
        private static ObjectId GetOrCreateSmallBlankDotBlockId(Database db, Transaction tr)
        {
            const string arrowBlockName = "_SMALL";
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (bt.Has(arrowBlockName))
                return bt[arrowBlockName];

            // Fallback definition: small blank dot centered at origin.
            bt.UpgradeOpen();
            var dotBlock = new BlockTableRecord { Name = arrowBlockName, Origin = Point3d.Origin };
            var dotId = bt.Add(dotBlock);
            tr.AddNewlyCreatedDBObject(dotBlock, true);

            var dot = new Circle(Point3d.Origin, Vector3d.ZAxis, 0.5);
            dot.SetDatabaseDefaults(db);
            dotBlock.AppendEntity(dot);
            tr.AddNewlyCreatedDBObject(dot, true);

            return dotId;
        }

        /// <summary>
        /// Creates or fully updates the SD_DIM_STYLE dimension style.
        /// 
        /// TCVN / ISO 129 principles + office styling applied:
        ///   - Arrow type  : Small Blank Dot ("_SMALL") — compact hollow dot for shop-drawing readability
        ///   - Dimasz      : 0.35 × text height (compact hollow dot, clearer at shop-drawing scales)
        ///   - Dimexo      : 1.5 mm fixed        (ISO 129: gap from object line to extension line ≥ 1.5 mm)
        ///   - Dimexe      : 1.0 × Dimasz        (extension line protrudes 1 tick length beyond dim line)
        ///   - Dimgap      : 0.4 × text height   (~1 mm gap between number and dim line)
        ///   - Dimdli      : 7–8 mm              (standard parallel dim line spacing)
        ///   - Dimtad = 1  : text above dim line  (TCVN / ISO preferred position)
        ///   - Dimdec = 0  : integer mm           (no decimal digits; unit = mm implied by drawing)
        ///   - Dimtih/Dimtoh = false : text aligned with dim line (not forced horizontal)
        ///   - Colors ByLayer so dim lines inherit their layer colour
        /// </summary>
        public static ObjectId EnsureDimStyle(Database db, Transaction tr)
        {
            double textH  = GetPaperTextHeight();           // e.g. 2.5 mm paper
            double arrow  = Math.Max(textH * 0.35, 0.7);   // tuned up so the hollow dot reads clearly in AutoCAD
            double extO   = 1.5;                            // ISO: extension offset (object → ext start) = 1.5 mm
            double extE   = Math.Max(arrow * 0.7, 0.25);   // shorter protrusion suits circular arrowheads better
            double gap    = textH * 0.4;                    // gap text ↔ dim line ~1 mm
            double dimdli = GetPaperDimLineSpacing();       // ~7-8 mm between parallel dim lines

            ObjectId arialStyleId  = BlockManager.EnsureArialStyle(db, tr);
            ObjectId smallDotId    = GetOrCreateSmallBlankDotBlockId(db, tr);

            var applyTo = (DimStyleTableRecord record) =>
            {
                //── Lines ──
                record.Dimclrd  = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                                    Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
                record.Dimclre  = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                                    Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
                record.Dimclrt  = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                                    Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
                record.Dimdli   = dimdli;        // parallel line spacing
                record.Dimexo   = extO;          // ISO 129: gap from geometry to ext line
                record.Dimexe   = extE;          // ext line protrusion past dim line
                record.Dimsoxd  = false;         // show extension lines

                //── Symbols & Arrows (office style: hollow dot) ──
                record.Dimblk   = smallDotId;    // both arrowheads = Small Blank Dot
                record.Dimsah   = false;         // use same block for both ends
                record.Dimasz   = arrow;         // dot size

                //── Text ──
                record.Dimtxsty = arialStyleId;
                record.Dimtxt   = textH;
                record.Dimtad   = 1;             // above dim line (TCVN preferred)
                record.Dimgap   = gap;           // gap number ↔ dim line
                record.Dimtih   = false;         // text follows dim line (xoay theo đường dim — chuẩn TCVN)
                record.Dimtoh   = false;         // text outside cũng xoay theo
                record.Dimtmove = 0;             // move text with dim line
                record.Dimjust  = 0;             // centred over dim line

                //── Fit / Scale ──
                record.Dimscale     = 0;         // 0 = annotative scale
                record.Annotative   = AnnotativeStates.True;
                record.Dimatfit     = 3;         // move text+arrow outside if tight

                //── Primary Units ──
                record.Dimdec   = 0;             // 0 decimal places (integer mm)
                record.Dimrnd   = 0;             // no rounding
                record.Dimpost  = "";            // no suffix (unit 'mm' implied)
                record.Dimlunit = 2;             // decimal units
                record.Dimdsep  = '.';
                record.Dimazin  = 0;             // suppress leading zeros: off
                record.Dimzin   = 8;             // suppress trailing zeros + zero feet/inches (metric)
            };

            // Set DIMFXL via system variables BEFORE applying style.
            // This must be outside the lambda so it runs at call-time.
            try
            {
                Application.SetSystemVariable("DIMFXLON", 1);
                Application.SetSystemVariable("DIMFXL", 5.0);
            }
            catch { /* Older API versions may not support DIMFXL — safe to ignore */ }

            var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            if (dst.Has(DIM_STYLE))
            {
                var existing = (DimStyleTableRecord)tr.GetObject(dst[DIM_STYLE], OpenMode.ForWrite);
                applyTo(existing);
                return existing.ObjectId;
            }

            dst.UpgradeOpen();
            var style = new DimStyleTableRecord { Name = DIM_STYLE };
            applyTo(style);
            dst.Add(style);
            tr.AddNewlyCreatedDBObject(style, true);
            return style.ObjectId;
        }

        private static double GetPaperTextHeight()
        {
            return Math.Max(ShopDrawingCommands.DefaultTextHeightMm, 1.0);
        }

        private static double GetScaleFactor(Database? db = null)
        {
            try
            {
                db ??= Application.DocumentManager.MdiActiveDocument?.Database;
                var acScale = db?.Cannoscale;
                if (acScale != null && acScale.PaperUnits > 0)
                    return acScale.DrawingUnits / acScale.PaperUnits;
            }
            catch { }

            try
            {
                var s = Application.GetSystemVariable("CANNOSCALE")?.ToString() ?? "1:100";
                var p = s.Split(':');
                if (p.Length == 2
                    && double.TryParse(p[0], out double n)
                    && double.TryParse(p[1], out double d)
                    && n > 0)
                    return d / n;
            }
            catch { }
            return 100.0;
        }

        private static double GetModelTextHeight(Database db)
        {
            return Math.Max(GetPaperTextHeight() * GetScaleFactor(db), 15.0);
        }

        private static double GetPaperFirstDimOffset()
        {
            // TCVN/ISO 129: first dim line sits 6–8 mm from the object.
            // Using 2.5 × textH gives ~6.25 mm at 2.5 mm text — within spec.
            return Math.Max(GetPaperTextHeight() * 2.5, 6.0);
        }

        private static double GetPaperDimLineSpacing()
        {
            // TCVN/ISO 129: parallel dim lines spaced ≥ 7 mm apart.
            // 3 × textH at 2.5 mm → 7.5 mm.  Clamp to 7.0 mm minimum.
            return Math.Max(GetPaperTextHeight() * 3.0, 7.0);
        }

        private static double GetPaperFixedExtensionLineLength()
        {
            return 5.0;
        }

        private static double GetModelFirstDimOffset(Database db)
        {
            // Keep extension lines short: 4mm paper on a 1:100 drawing = 400mm model.
            // Minimum 80mm so the dim line doesn't clip the wall linework.
            return Math.Max(GetPaperFirstDimOffset() * GetScaleFactor(db), 80.0);
        }

        private static double GetModelDimLineSpacing(Database db)
        {
            return Math.Max(GetPaperDimLineSpacing() * GetScaleFactor(db), 100.0);
        }

        private static double GetPaperTextHeightFromTag(Database db, Transaction tr)
        {
            double scaleFactor = GetScaleFactor(db);
            double fallback = Math.Max(ShopDrawingCommands.DefaultTextHeightMm, 1.8);
            if (scaleFactor <= 0)
                return fallback;

            try
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in ms)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is not DBText txt) continue;
                    if (!string.Equals(txt.Layer, "SD_TAG", StringComparison.OrdinalIgnoreCase)) continue;
                    if (txt.Height <= 0) continue;
                    return Math.Max(txt.Height / scaleFactor, fallback);
                }

            }
            catch { }

            return fallback;
        }

        private static string[] GetDesiredAnnotationScales(Database db)
        {
            var names = new List<string>();

            try
            {
                string? currentScale = db.Cannoscale?.Name;
                if (!string.IsNullOrWhiteSpace(currentScale))
                    names.Add(currentScale.Trim());
            }
            catch { }

            foreach (string part in (ShopDrawingCommands.DefaultAnnotationScales ?? string.Empty).Split(','))
            {
                string scaleName = part.Trim();
                if (!string.IsNullOrWhiteSpace(scaleName))
                    names.Add(scaleName);
            }

            if (names.Count == 0)
                names.Add("1:100");

            return names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void ApplyAnnotativeContexts(Entity entity, Database db)
        {
            try
            {
                entity.Annotative = AnnotativeStates.True;
            }
            catch { }

            try
            {
                ObjectContextManager ocm = db.ObjectContextManager;
                ObjectContextCollection occ = ocm.GetContextCollection("ACDB_ANNOTATIONSCALES");
                string[] desiredScales = GetDesiredAnnotationScales(db);

                if (desiredScales.Any(name => !string.Equals(name, "1:1", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        ObjectContext defaultScale = occ.GetContext("1:1");
                        if (defaultScale != null)
                            ObjectContexts.RemoveContext(entity, defaultScale);
                    }
                    catch { }
                }

                foreach (string scaleName in desiredScales)
                {
                    try
                    {
                        ObjectContext scale = occ.GetContext(scaleName);
                        if (scale != null)
                            ObjectContexts.AddContext(entity, scale);
                    }
                    catch { }
                }
            }
            catch { }
        }

        internal static void ApplyAnnotativeContextsForUpdate(Entity entity, Database db)
        {
            ApplyAnnotativeContexts(entity, db);
        }

        internal static void NormalizeDimensionAppearanceForUpdate(Dimension dim, Database db)
        {
            NormalizeDimensionAppearance(dim, db);
            TryApplyFixedExtensionLineOverride(dim);
        }

        // ═══════════════════════════════════════════════════
        // XDATA — gắn wallCode vào dim (P1)
        // ═══════════════════════════════════════════════════

        private static void RegisterXDataApp(Database db, Transaction tr)
        {
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (rat.Has(XDATA_APP)) return;

            rat.UpgradeOpen();
            var rec = new RegAppTableRecord { Name = XDATA_APP };
            rat.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
        }

        private static void AttachWallXData(Database db, Transaction tr, Entity ent, string wallCode)
        {
            RegisterXDataApp(db, tr);
            ent.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, XDATA_APP),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, wallCode));
        }

        private static void AppendDimension(
            BlockTableRecord ms,
            Transaction tr,
            Database db,
            Dimension dim,
            string layerName,
            string? wallCode = null)
        {
            dim.Layer = layerName;
            if (!string.IsNullOrWhiteSpace(wallCode))
                AttachWallXData(db, tr, dim, wallCode);

            ms.AppendEntity(dim);
            tr.AddNewlyCreatedDBObject(dim, true);
            NormalizeDimensionAppearance(dim, db);
        }

        private static void NormalizeDimensionAppearance(Dimension dim, Database db)
        {
            ApplyAnnotativeContexts(dim, db);

            if (dim is RotatedDimension rotated)
            {
                rotated.UsingDefaultTextPosition = true;
            }

            TryApplyFixedExtensionLineOverride(dim);

            try
            {
                dim.RecomputeDimensionBlock(true);
            }
            catch { }

            dim.RecordGraphicsModified(true);
        }

        private static void TryApplyFixedExtensionLineOverride(Dimension dim)
        {
            try
            {
                dynamic acadDim = dim.AcadObject;
                acadDim.ExtLineFixedLenSuppress = true;
                acadDim.ExtLineFixedLen = GetPaperFixedExtensionLineLength();
                acadDim.Update();
            }
            catch
            {
                // ActiveX override is best-effort. If unavailable, the style/system-variable
                // defaults configured in EnsureDimStyle remain the fallback.
            }
        }

        private static void ApplyFixedExtensionLineOverrides(Database db, IEnumerable<ObjectId> dimensionIds)
        {
            var ids = dimensionIds
                .Where(id => !id.IsNull && id.IsValid)
                .Distinct()
                .ToList();
            if (ids.Count == 0)
                return;

            using var tr = db.TransactionManager.StartTransaction();
            foreach (ObjectId id in ids)
            {
                if (tr.GetObject(id, OpenMode.ForWrite, false) is not Dimension dim)
                    continue;

                TryApplyFixedExtensionLineOverride(dim);
                try
                {
                    dim.RecomputeDimensionBlock(true);
                }
                catch { }

                dim.RecordGraphicsModified(true);
            }

            tr.Commit();
        }

        private static void ApplyFixedExtensionLineOverridesForWall(Database db, string wallCode)
        {
            var ids = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in ms)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is not Dimension dim)
                        continue;
                    if (dim.Layer != DIM_LAYER_PANEL && dim.Layer != DIM_LAYER_OPENING)
                        continue;

                    string? code = ReadWallXData(dim);
                    if (!string.Equals(code, wallCode, StringComparison.OrdinalIgnoreCase))
                        continue;

                    ids.Add(id);
                }

                tr.Commit();
            }

            ApplyFixedExtensionLineOverrides(db, ids);
        }

        private static string? ReadWallXData(Entity ent)
        {
            var xd = ent.GetXDataForApplication(XDATA_APP);
            if (xd == null) return null;
            var vals = xd.AsArray();
            return vals.Length >= 2 ? vals[1].Value?.ToString() : null;
        }

        // ═══════════════════════════════════════════════════
        // SCAN WALL CODES (cho dropdown palette)
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Scan ModelSpace → tìm unique wall codes từ SD_TAG text.
        /// Tag format: "W1-01", "W2-03" → trả về ["W1", "W2"].
        /// </summary>
        public static List<string> ScanWallCodes()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return [];

            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var db = doc.Database;

            using var tr = db.TransactionManager.StartTransaction();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent is not DBText txt) continue;
                if (txt.Layer != "SD_TAG") continue;

                // "W1-01 / SPEC" → lấy "W1"
                string text = txt.TextString.Split('/')[0].Trim();
                int dash = text.IndexOf('-');
                if (dash > 0)
                    codes.Add(text[..dash]);
            }

            tr.Commit();
            return codes.OrderBy(c => c).ToList();
        }

        /// <summary>
        /// Scan wall codes only within a specific model-space region.
        /// Returns unique wall codes found in that area (e.g. ["W1"]).
        /// </summary>
        public static List<string> ScanWallCodesInRegion(Extents3d region)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return [];

            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var db = doc.Database;

            using var tr = db.TransactionManager.StartTransaction();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent is not DBText txt) continue;
                if (txt.Layer != "SD_TAG") continue;

                // Check if tag is inside the selected region
                var pt = txt.Position;
                if (pt.X < region.MinPoint.X || pt.X > region.MaxPoint.X ||
                    pt.Y < region.MinPoint.Y || pt.Y > region.MaxPoint.Y)
                    continue;

                // "W1-01 / SPEC" → lấy "W1"
                string text = txt.TextString.Split('/')[0].Trim();
                int dash = text.IndexOf('-');
                if (dash > 0)
                    codes.Add(text[..dash]);
            }

            tr.Commit();
            return codes.OrderBy(c => c).ToList();
        }

        // ═══════════════════════════════════════════════════
        // CLEAR EXISTING DIMS (P1 — anti-duplicate)
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Xóa tất cả dim có XData wallCode trên layer SD_DIM_PANEL + SD_DIM_OPENING.
        /// </summary>
        public static int ClearWallDims(string wallCode)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return 0;

            using (doc.LockDocument())
            {
                return ClearWallDimsCore(doc.Database, wallCode);
            }
        }

        private static int ClearWallDimsCore(Database db, string wallCode)
        {
            int count = 0;

            using var tr = db.TransactionManager.StartTransaction();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            foreach (ObjectId id in ms)
            {
                var ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                if (ent.Layer != DIM_LAYER_PANEL && ent.Layer != DIM_LAYER_OPENING) continue;

                var code = ReadWallXData(ent);
                if (code != null && code.Equals(wallCode, StringComparison.OrdinalIgnoreCase))
                {
                    ent.UpgradeOpen();
                    ent.Erase();
                    count++;
                }
            }

            tr.Commit();
            return count;
        }

        // ═══════════════════════════════════════════════════
        // AUTO MODE — Main entry (P1-P7)
        // ═══════════════════════════════════════════════════

        public record AutoDimRequest(
            string WallCode,
            bool DimWidth     = true,
            bool DimHeight    = true,
            bool DimOpening   = true,
            bool DimElevation = true,
            bool HeightOnRight = true  // true=phải, false=trái
        );

        public record AutoDimResult(int DimCount, string Message);

        /// <summary>
        /// Auto dim toàn bộ cho 1 wall: xóa cũ → vẽ mới.
        /// </summary>
        public AutoDimResult AutoDimWall(AutoDimRequest req)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return new(0, "No document");

            using var docLock = doc.LockDocument();
            var db = doc.Database;

            // P1: Xóa dim cũ trước
            int cleared = ClearWallDimsCore(db, req.WallCode);

            int count = 0;
            using var tr = db.TransactionManager.StartTransaction();
            try
            {
                EnsureDimLayer(db, tr, DIM_LAYER_PANEL, 4);    // Cyan
                EnsureDimLayer(db, tr, DIM_LAYER_OPENING, 6);  // Magenta
                var styleId = EnsureDimStyle(db, tr);

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // Scan panels
                var panels = ScanPanelPositions(ms, tr, req.WallCode);
                if (panels.Count < 1)
                {
                    tr.Commit();
                    return new(0, $"Không tìm thấy panel {req.WallCode}.");
                }

                // Detect wall orientation: ngang (X spread > Y spread) hay dọc
                var wallBounds = GetWallBounds(panels);
                double wW = wallBounds.MaxPoint.X - wallBounds.MinPoint.X;
                double wH = wallBounds.MaxPoint.Y - wallBounds.MinPoint.Y;
                bool isHorizontal = wW >= wH;
                var occupiedBounds = GetOccupiedBounds(panels);
                var openings = (req.DimOpening || req.DimElevation || (req.DimHeight && isHorizontal && req.HeightOnRight))
                    ? ScanOpenings(ms, tr, wallBounds)
                    : new List<OpeningPos>();
                int rightOpeningHeightLaneCount = isHorizontal && req.DimOpening ? openings.Count : 0;
                int rightOpeningSillLaneCount = isHorizontal && req.DimOpening
                    ? openings.Count(o => Math.Abs(o.Bounds.MinPoint.Y - wallBounds.MinPoint.Y) > 1.0)
                    : 0;

                double textH = panels[0].textH;
                double dimGap = GetModelFirstDimOffset(db);

                // ── P4: Dim chiều rộng (ngang hoặc dọc tùy orientation) ──
                if (req.DimWidth)
                {
                    count += DimPanelWidths(ms, tr, db, styleId, panels, wallBounds, occupiedBounds,
                                            isHorizontal, dimGap, req.WallCode);
                }

                // ── P5: Dim chiều cao ──
                if (req.DimHeight)
                {
                    count += DimPanelHeight(ms, tr, db, styleId, panels, wallBounds, occupiedBounds,
                                            isHorizontal, dimGap, req.HeightOnRight,
                                            req.HeightOnRight && rightOpeningHeightLaneCount + rightOpeningSillLaneCount > 0
                                                ? rightOpeningHeightLaneCount + rightOpeningSillLaneCount + 1
                                                : 0,
                                            req.WallCode);
                }

                // ── P6: Dim opening ──
                if (req.DimOpening)
                {
                    count += DimOpenings(ms, tr, db, styleId, openings, wallBounds, occupiedBounds,
                                         isHorizontal, dimGap, req.HeightOnRight, req.WallCode);
                }

                // ── P7: Dim elevation ──
                if (req.DimElevation)
                {
                    count += DimElevations(ms, tr, db, styleId, wallBounds, openings,
                                           dimGap, req.WallCode);
                }

                tr.Commit();
                ApplyFixedExtensionLineOverridesForWall(db, req.WallCode);
            }
            catch (Exception ex)
            {
                tr.Abort();
                return new(0, $"Lỗi: {ex.Message}");
            }

            string msg = cleared > 0
                ? $"✅ Xóa {cleared} dim cũ, vẽ {count} dim mới cho {req.WallCode}."
                : $"✅ Đã vẽ {count} dim cho {req.WallCode}.";
            return new(count, msg);
        }

        // ═══════════════════════════════════════════════════
        // DIM PANEL WIDTHS (P4 — cả 2 chiều)
        // ═══════════════════════════════════════════════════

        private int DimPanelWidths(
            BlockTableRecord ms, Transaction tr, Database db, ObjectId styleId,
            List<PanelPos> panels, Extents3d wallBounds, Extents3d occupiedBounds,
            bool isHorizontal, double dimGap, string wallCode)
        {
            int count = 0;

            if (isHorizontal)
            {
                // Panel ngang → dim chiều rộng theo X (ở phía trên)
                panels.Sort((a, b) => a.bounds.MinPoint.X.CompareTo(b.bounds.MinPoint.X));
                double dimY = GetTopDimLineY(wallBounds, occupiedBounds, db);
                double lineSpacing = GetModelDimLineSpacing(db);

                // Hàng 1: Từng panel width
                foreach (var p in panels)
                {
                    var pt1 = new Point3d(p.bounds.MinPoint.X, wallBounds.MaxPoint.Y, 0);
                    var pt2 = new Point3d(p.bounds.MaxPoint.X, wallBounds.MaxPoint.Y, 0);
                    var dim = CreateLinearDim(pt1, pt2, new Point3d(pt1.X, dimY, 0), styleId);
                    AppendDimension(ms, tr, db, dim, DIM_LAYER_PANEL, wallCode);
                    count++;
                }

                // Hàng 2: Overall
                double overallY = dimY + lineSpacing;
                var first = panels.First();
                var last = panels.Last();
                var oDim = CreateLinearDim(
                    new Point3d(first.bounds.MinPoint.X, wallBounds.MaxPoint.Y, 0),
                    new Point3d(last.bounds.MaxPoint.X, wallBounds.MaxPoint.Y, 0),
                    new Point3d(first.bounds.MinPoint.X, overallY, 0), styleId);
                AppendDimension(ms, tr, db, oDim, DIM_LAYER_PANEL, wallCode);
                count++;
            }
            else
            {
                // Panel dọc → dim chiều rộng theo Y (bên trái)
                panels.Sort((a, b) => a.bounds.MinPoint.Y.CompareTo(b.bounds.MinPoint.Y));
                double dimX = GetLeftDimLineX(wallBounds, occupiedBounds, db);
                double lineSpacing = GetModelDimLineSpacing(db);

                foreach (var p in panels)
                {
                    var pt1 = new Point3d(wallBounds.MinPoint.X, p.bounds.MinPoint.Y, 0);
                    var pt2 = new Point3d(wallBounds.MinPoint.X, p.bounds.MaxPoint.Y, 0);
                    var dim = CreateLinearDim(pt1, pt2, new Point3d(dimX, pt1.Y, 0), styleId);
                    AppendDimension(ms, tr, db, dim, DIM_LAYER_PANEL, wallCode);
                    count++;
                }

                double overallX = dimX - lineSpacing;
                var first = panels.First();
                var last = panels.Last();
                var oDim = CreateLinearDim(
                    new Point3d(wallBounds.MinPoint.X, first.bounds.MinPoint.Y, 0),
                    new Point3d(wallBounds.MinPoint.X, last.bounds.MaxPoint.Y, 0),
                    new Point3d(overallX, first.bounds.MinPoint.Y, 0), styleId);
                AppendDimension(ms, tr, db, oDim, DIM_LAYER_PANEL, wallCode);
                count++;
            }

            return count;
        }

        // ═══════════════════════════════════════════════════
        // DIM PANEL HEIGHT (P5)
        // ═══════════════════════════════════════════════════

        private int DimPanelHeight(
            BlockTableRecord ms, Transaction tr, Database db, ObjectId styleId,
            List<PanelPos> panels, Extents3d wallBounds, Extents3d occupiedBounds,
            bool isHorizontal, double dimGap, bool onRight, int outerLaneOffset, string wallCode)
        {
            Point3d pt1;
            Point3d pt2;
            Point3d dimPt;

            if (isHorizontal)
            {
                double dimX = onRight
                    ? GetRightDimLineX(wallBounds, occupiedBounds, db) + outerLaneOffset * GetModelDimLineSpacing(db)
                    : GetLeftDimLineX(wallBounds, occupiedBounds, db);

                pt1 = new Point3d(wallBounds.MaxPoint.X, wallBounds.MinPoint.Y, 0);
                pt2 = new Point3d(wallBounds.MaxPoint.X, wallBounds.MaxPoint.Y, 0);
                dimPt = new Point3d(dimX, wallBounds.MinPoint.Y, 0);
            }
            else
            {
                // Với tường dọc, vẽ cross-dimension theo phương ngang.
                // Mapping pragmatic: "Phải" -> đặt dim phía trên, "Trái" -> phía dưới.
                double dimY = onRight
                    ? GetTopDimLineY(wallBounds, occupiedBounds, db)
                    : GetBottomDimLineY(wallBounds, occupiedBounds, db);

                pt1 = new Point3d(wallBounds.MinPoint.X, wallBounds.MaxPoint.Y, 0);
                pt2 = new Point3d(wallBounds.MaxPoint.X, wallBounds.MaxPoint.Y, 0);
                dimPt = new Point3d(wallBounds.MinPoint.X, dimY, 0);
            }

            var dim = CreateLinearDim(pt1, pt2, dimPt, styleId);
            AppendDimension(ms, tr, db, dim, DIM_LAYER_PANEL, wallCode);

            return 1;
        }

        // ═══════════════════════════════════════════════════
        // DIM OPENINGS (P6)
        // ═══════════════════════════════════════════════════

        private record OpeningPos(Extents3d Bounds);

        private List<OpeningPos> ScanOpenings(BlockTableRecord ms, Transaction tr, Extents3d wallBounds)
        {
            var result = new List<OpeningPos>();

            foreach (ObjectId id in ms)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                if (!string.Equals(ent.Layer, "SD_OPENING", StringComparison.OrdinalIgnoreCase)) continue;
                if (!TryGetOpeningBounds(ent, out var ext)) continue;

                // Check overlap with wall bounds
                bool overlaps = ext.MinPoint.X < wallBounds.MaxPoint.X &&
                                ext.MaxPoint.X > wallBounds.MinPoint.X &&
                                ext.MinPoint.Y < wallBounds.MaxPoint.Y &&
                                ext.MaxPoint.Y > wallBounds.MinPoint.Y;
                if (overlaps && !ContainsOpening(result, ext))
                    result.Add(new OpeningPos(ext));
            }

            return result;
        }

        private int DimOpenings(
            BlockTableRecord ms, Transaction tr, Database db, ObjectId styleId,
            List<OpeningPos> openings, Extents3d wallBounds, Extents3d occupiedBounds,
            bool isHorizontal, double dimGap, bool heightOnRight, string wallCode)
        {
            if (openings.Count == 0) return 0;
            int count = 0;
            double outsideOffset = GetModelFirstDimOffset(db);
            double lineSpacing   = GetModelDimLineSpacing(db);
            var orderedOpenings = isHorizontal
                ? openings.OrderBy(o => o.Bounds.MinPoint.X).ThenBy(o => o.Bounds.MinPoint.Y).ToList()
                : openings.OrderBy(o => o.Bounds.MinPoint.Y).ThenBy(o => o.Bounds.MinPoint.X).ToList();

            if (isHorizontal)
            {
                double dimY       = wallBounds.MinPoint.Y - outsideOffset;
                double baseRightX = GetRightDimLineX(wallBounds, occupiedBounds, db) + lineSpacing;

                // ── Loop 1: Width / location dims — in position order (left → right) ──
                double cursorX = wallBounds.MinPoint.X;
                foreach (var op in orderedOpenings)  // orderedOpenings sorted by X
                {
                    var b = op.Bounds;

                    // Gap segment before this opening
                    if (b.MinPoint.X - cursorX > 1.0)
                    {
                        var dGap = CreateLinearDim(
                            new Point3d(cursorX, wallBounds.MinPoint.Y, 0),
                            new Point3d(b.MinPoint.X, wallBounds.MinPoint.Y, 0),
                            new Point3d(cursorX, dimY, 0), styleId);
                        AppendDimension(ms, tr, db, dGap, DIM_LAYER_OPENING, wallCode);
                        count++;
                    }

                    // Opening width
                    var dW = CreateLinearDim(
                        new Point3d(b.MinPoint.X, wallBounds.MinPoint.Y, 0),
                        new Point3d(b.MaxPoint.X, wallBounds.MinPoint.Y, 0),
                        new Point3d(b.MinPoint.X, dimY, 0), styleId);
                    AppendDimension(ms, tr, db, dW, DIM_LAYER_OPENING, wallCode);
                    count++;
                    cursorX = Math.Max(cursorX, b.MaxPoint.X);
                }

                // Tail gap after last opening
                if (wallBounds.MaxPoint.X - cursorX > 1.0)
                {
                    var dTail = CreateLinearDim(
                        new Point3d(cursorX, wallBounds.MinPoint.Y, 0),
                        new Point3d(wallBounds.MaxPoint.X, wallBounds.MinPoint.Y, 0),
                        new Point3d(cursorX, dimY, 0), styleId);
                    AppendDimension(ms, tr, db, dTail, DIM_LAYER_OPENING, wallCode);
                    count++;
                }

                // ── Loop 2: Height dims — sorted SHORT → LONG (closest to wall = shortest) ──
                // Sort by opening height ascending; sill height secondary (ascending).
                var sortedRightOpenings = openings
                    .Select(op =>
                    {
                        var b = op.Bounds;
                        return new
                        {
                            Bounds = b,
                            SillHeight = Math.Abs(b.MinPoint.Y - wallBounds.MinPoint.Y),
                            OpeningHeight = Math.Abs(b.MaxPoint.Y - b.MinPoint.Y),
                            TopHeight = Math.Abs(b.MaxPoint.Y - wallBounds.MinPoint.Y)
                        };
                    })
                    .OrderBy(o => o.OpeningHeight)
                    .ThenBy(o => o.SillHeight)
                    .ThenBy(o => o.TopHeight)
                    .ToList();
                var sortedRightSills = sortedRightOpenings
                    .Where(o => o.SillHeight > 1.0)
                    .OrderBy(o => o.SillHeight)
                    .ThenBy(o => o.Bounds.MinPoint.Y)
                    .ThenBy(o => o.TopHeight)
                    .ToList();

                for (int rightLane = 0; rightLane < sortedRightOpenings.Count; rightLane++)
                {
                    var op = sortedRightOpenings[rightLane];
                    var b = op.Bounds;
                    double dimX = baseRightX + rightLane * lineSpacing;
                    var dHeight = CreateLinearDim(
                        new Point3d(wallBounds.MaxPoint.X, b.MinPoint.Y, 0),
                        new Point3d(wallBounds.MaxPoint.X, b.MaxPoint.Y, 0),
                        new Point3d(dimX, b.MinPoint.Y, 0), styleId);
                    AppendDimension(ms, tr, db, dHeight, DIM_LAYER_OPENING, wallCode);
                    count++;

                    if (rightLane == sortedRightOpenings.Count - 1)
                    {
                        double sillBaseX = baseRightX + sortedRightOpenings.Count * lineSpacing;
                        for (int sillLane = 0; sillLane < sortedRightSills.Count; sillLane++)
                        {
                            var sillOp = sortedRightSills[sillLane];
                            var sillBounds = sillOp.Bounds;
                            double sillDimX = sillBaseX + sillLane * lineSpacing;
                            var dBase = CreateLinearDim(
                                new Point3d(wallBounds.MaxPoint.X, wallBounds.MinPoint.Y, 0),
                                new Point3d(wallBounds.MaxPoint.X, sillBounds.MinPoint.Y, 0),
                                new Point3d(sillDimX, wallBounds.MinPoint.Y, 0), styleId);
                            AppendDimension(ms, tr, db, dBase, DIM_LAYER_OPENING, wallCode);
                            count++;
                        }
                    }

                    // Opening clear height: same fix — anchor at wall right edge, not b.MaxPoint.X
                }
            }
            else
            {
                // VERTICAL WALL — opening height dims go RIGHT, opening width dims go BOTTOM.
                // Panel width dims are on LEFT, panel height dim is on TOP — no conflict.

                // ── Loop 1: Height / location dims along Y — in position order (bottom → top) ──
                double dimX    = wallBounds.MaxPoint.X + outsideOffset;
                double cursorY = wallBounds.MinPoint.Y;

                foreach (var op in orderedOpenings)  // orderedOpenings sorted by Y
                {
                    var b = op.Bounds;

                    // Gap segment before this opening
                    if (b.MinPoint.Y - cursorY > 1.0)
                    {
                        var dGap = CreateLinearDim(
                            new Point3d(wallBounds.MaxPoint.X, cursorY, 0),
                            new Point3d(wallBounds.MaxPoint.X, b.MinPoint.Y, 0),
                            new Point3d(dimX, cursorY, 0), styleId);
                        AppendDimension(ms, tr, db, dGap, DIM_LAYER_OPENING, wallCode);
                        count++;
                    }

                    // Opening height along Y
                    var dH = CreateLinearDim(
                        new Point3d(wallBounds.MaxPoint.X, b.MinPoint.Y, 0),
                        new Point3d(wallBounds.MaxPoint.X, b.MaxPoint.Y, 0),
                        new Point3d(dimX, b.MinPoint.Y, 0), styleId);
                    AppendDimension(ms, tr, db, dH, DIM_LAYER_OPENING, wallCode);
                    count++;
                    cursorY = Math.Max(cursorY, b.MaxPoint.Y);
                }

                // Tail gap after last opening
                if (wallBounds.MaxPoint.Y - cursorY > 1.0)
                {
                    var dTail = CreateLinearDim(
                        new Point3d(wallBounds.MaxPoint.X, cursorY, 0),
                        new Point3d(wallBounds.MaxPoint.X, wallBounds.MaxPoint.Y, 0),
                        new Point3d(dimX, cursorY, 0), styleId);
                    AppendDimension(ms, tr, db, dTail, DIM_LAYER_OPENING, wallCode);
                    count++;
                }

                // ── Loop 2: Width dims — sorted SHORT → LONG, stacked below wall ──
                var sortedByWidth = openings
                    .OrderBy(o => o.Bounds.MaxPoint.X - o.Bounds.MinPoint.X)
                    .ToList();

                double baseBottomY = heightOnRight
                    ? wallBounds.MinPoint.Y - outsideOffset
                    : GetBottomDimLineY(wallBounds, occupiedBounds, db) - lineSpacing;
                int bottomLane = 0;

                foreach (var op in sortedByWidth)
                {
                    var b = op.Bounds;
                    double dimWY = baseBottomY - bottomLane * lineSpacing;

                    // Opening width — anchor at wall BOTTOM edge (not b.MinPoint.Y inside wall)
                    var dW = CreateLinearDim(
                        new Point3d(b.MinPoint.X, wallBounds.MinPoint.Y, 0),
                        new Point3d(b.MaxPoint.X, wallBounds.MinPoint.Y, 0),
                        new Point3d(b.MinPoint.X, dimWY, 0), styleId);
                    AppendDimension(ms, tr, db, dW, DIM_LAYER_OPENING, wallCode);
                    count++;
                    bottomLane++;
                }
            }

            return count;
        }

        // ═══════════════════════════════════════════════════
        // DIM ELEVATION (P7)
        // ═══════════════════════════════════════════════════

        private int DimElevations(
            BlockTableRecord ms, Transaction tr, Database db, ObjectId styleId,
            Extents3d wallBounds, List<OpeningPos> openings,
            double dimGap, string wallCode)
        {
            // Elevation = text markers bên trái, ngoài wall bounds
            double textX = wallBounds.MinPoint.X - dimGap * 2.5;
            double textH = GetModelTextHeight(db);

            var elevations = new SortedSet<double>();
            elevations.Add(wallBounds.MinPoint.Y); // Đáy tường
            elevations.Add(wallBounds.MaxPoint.Y); // Đỉnh tường

            double baseY = wallBounds.MinPoint.Y; // EL +0
            int count = 0;

            foreach (double y in elevations)
            {
                double elevMm = Math.Round(y - baseY);
                string label = elevMm >= 0 ? $"EL +{elevMm:F0}" : $"EL {elevMm:F0}";

                // Vẽ leader line ngắn + text
                var txt = new DBText
                {
                    TextString = label,
                    Position   = new Point3d(textX, y, 0),
                    Height     = textH,
                    Layer      = DIM_LAYER_PANEL,
                    TextStyleId = BlockManager.EnsureArialStyle(db, tr),
                    HorizontalMode = TextHorizontalMode.TextRight,
                    VerticalMode   = TextVerticalMode.TextVerticalMid,
                    AlignmentPoint = new Point3d(textX, y, 0),
                };
                AttachWallXData(db, tr, txt, wallCode);
                ms.AppendEntity(txt);
                tr.AddNewlyCreatedDBObject(txt, true);

                // Dashes line from text to wall
                var line = new Line(
                    new Point3d(textX + textH, y, 0),
                    new Point3d(wallBounds.MinPoint.X, y, 0));
                line.Layer = DIM_LAYER_PANEL;
                line.ColorIndex = 4; // Cyan
                AttachWallXData(db, tr, line, wallCode);
                ms.AppendEntity(line);
                tr.AddNewlyCreatedDBObject(line, true);

                count += 2;
            }

            return count;
        }

        // ═══════════════════════════════════════════════════
        // MANUAL MODE (P3 — loop tới ESC)
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Manual dim loop: pick liên tục cho đến ESC.
        /// Trả về số dim đã vẽ.
        /// </summary>
        public int ManualDimLoop(double? rotationRad = null)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return 0;

            int total = 0;
            while (true)
            {
                bool ok = ManualDim(rotationRad);
                if (!ok) break; // User pressed ESC
                total++;
            }
            return total;
        }

        /// <summary>
        /// Single manual dim: pick 3 điểm.
        /// rotation = 0 → ngang; PI/2 → đứng; null → aligned.
        /// </summary>
        public bool ManualDim(double? rotationRad = null)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;

            var ed = doc.Editor;
            var db = doc.Database;

            var opt1 = new PromptPointOptions("\nChọn điểm đầu (ESC để thoát): ");
            opt1.AllowNone = true;
            var res1 = ed.GetPoint(opt1);
            if (res1.Status != PromptStatus.OK) return false;
            var pt1 = res1.Value;

            var opt2 = new PromptPointOptions("\nChọn điểm cuối: ")
            {
                UseBasePoint = true,
                BasePoint    = pt1
            };
            var res2 = ed.GetPoint(opt2);
            if (res2.Status != PromptStatus.OK) return false;
            var pt2 = res2.Value;

            var opt3 = new PromptPointOptions("\nVị trí đường dim: ")
            {
                UseBasePoint = true,
                BasePoint    = pt1
            };
            var res3 = ed.GetPoint(opt3);
            if (res3.Status != PromptStatus.OK) return false;
            var ptDim = res3.Value;

            using var docLock = doc.LockDocument();
            using var tr = db.TransactionManager.StartTransaction();
            try
            {
                ObjectId dimId = ObjectId.Null;
                EnsureDimLayer(db, tr, DIM_LAYER_PANEL, 4);
                var styleId = EnsureDimStyle(db, tr);

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                Dimension dim;
                if (rotationRad.HasValue)
                {
                    dim = new RotatedDimension(rotationRad.Value, pt1, pt2, ptDim, "", styleId);
                }
                else
                {
                    dim = new AlignedDimension(pt1, pt2, ptDim, "", styleId);
                }

                AppendDimension(ms, tr, db, dim, DIM_LAYER_PANEL);
                dimId = dim.ObjectId;
                tr.Commit();
                ApplyFixedExtensionLineOverrides(db, new[] { dimId });
                return true;
            }
            catch { tr.Abort(); return false; }
        }

        // ═══════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════

        private record PanelPos(
            double start, double length,
            Extents3d bounds, Extents3d tagBounds,
            Point3d tagAnchor, double textH);

        private List<PanelPos> ScanPanelPositions(
            BlockTableRecord ms, Transaction tr, string wallCode)
        {
            var result = new List<PanelPos>();
            string prefix = wallCode + "-";

            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent is not DBText txt) continue;
                if (txt.Layer != "SD_TAG") continue;

                string text = txt.TextString.Split('/')[0].Trim();
                if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var pos   = GetTextAnchor(txt);
                double textH = txt.Height;

                if (TryFindPanelForTag(ms, tr, pos, textH, out var matchedPanel))
                {
                    result.Add(new PanelPos(
                        matchedPanel.start,
                        matchedPanel.length,
                        matchedPanel.bounds,
                        GetTextBounds(txt, pos, textH),
                        pos,
                        textH));
                    continue;
                }

                // Tìm polyline SD_PANEL chứa text
                foreach (ObjectId id2 in ms)
                {
                    var ent2 = tr.GetObject(id2, OpenMode.ForRead);
                    if (ent2 is not Polyline pl) continue;
                    if (!pl.Layer.StartsWith("SD_PANEL")) continue;

                    var ext = pl.GeometricExtents;
                    bool contains = pos.X >= ext.MinPoint.X && pos.X <= ext.MaxPoint.X &&
                                    pos.Y >= ext.MinPoint.Y && pos.Y <= ext.MaxPoint.Y;
                    if (!contains) continue;

                    double w = ext.MaxPoint.X - ext.MinPoint.X;
                    double h = ext.MaxPoint.Y - ext.MinPoint.Y;

                    // startPt/endPt theo chiều rộng lớn hơn
                    double start;

                    if (w >= h) // Ngang
                    {
                        start   = ext.MinPoint.X;
                    }
                    else // Dọc
                    {
                        start   = ext.MinPoint.Y;
                    }

                    result.Add(new PanelPos(
                        start,
                        Math.Max(w, h),
                        ext,
                        GetTextBounds(txt, pos, textH),
                        pos,
                        textH));
                    break;
                }
            }

            return result;
        }

        private static Point3d GetTextAnchor(DBText txt)
        {
            return txt.HorizontalMode == TextHorizontalMode.TextLeft &&
                   txt.VerticalMode == TextVerticalMode.TextBase
                ? txt.Position
                : txt.AlignmentPoint;
        }

        private static bool TryFindPanelForTag(
            BlockTableRecord ms,
            Transaction tr,
            Point3d tagPoint,
            double textH,
            out PanelPos panel)
        {
            PanelPos bestPanel = default;
            bool found = false;
            double bestScore = double.MaxValue;
            double maxGap = Math.Max(textH * 4.0, 150.0);

            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent is not Polyline pl) continue;
                if (!pl.Layer.StartsWith("SD_PANEL", StringComparison.OrdinalIgnoreCase)) continue;
                if (!TryBuildPanelPos(pl, textH, out var candidate)) continue;

                bool contains = IsPointInside(candidate.bounds, tagPoint, 1.0);
                double distance = contains ? 0.0 : DistanceToExtents(tagPoint, candidate.bounds);
                if (!contains)
                {
                    bool alignedX = tagPoint.X >= candidate.bounds.MinPoint.X - textH &&
                                    tagPoint.X <= candidate.bounds.MaxPoint.X + textH;
                    bool alignedY = tagPoint.Y >= candidate.bounds.MinPoint.Y - textH &&
                                    tagPoint.Y <= candidate.bounds.MaxPoint.Y + textH;
                    if (!(alignedX || alignedY) || distance > maxGap)
                        continue;
                }

                double score = contains ? 0.0 : distance;
                if (!found || score < bestScore)
                {
                    bestScore = score;
                    bestPanel = candidate;
                    found = true;
                }
            }

            panel = bestPanel;
            return found;
        }

        private static bool TryBuildPanelPos(Polyline pl, double textH, out PanelPos panel)
        {
            try
            {
                var ext = pl.GeometricExtents;
                double w = ext.MaxPoint.X - ext.MinPoint.X;
                double h = ext.MaxPoint.Y - ext.MinPoint.Y;

                double start;

                if (w >= h)
                {
                    start   = ext.MinPoint.X;
                }
                else
                {
                    start   = ext.MinPoint.Y;
                }

                panel = new PanelPos(start, Math.Max(w, h), ext, ext, Point3d.Origin, textH);
                return true;
            }
            catch
            {
                panel = default;
                return false;
            }
        }

        private static Extents3d GetWallBounds(List<PanelPos> panels)
        {
            double minX = panels.Min(p => p.bounds.MinPoint.X);
            double minY = panels.Min(p => p.bounds.MinPoint.Y);
            double maxX = panels.Max(p => p.bounds.MaxPoint.X);
            double maxY = panels.Max(p => p.bounds.MaxPoint.Y);
            return new Extents3d(new Point3d(minX, minY, 0), new Point3d(maxX, maxY, 0));
        }

        private static double GetTagClearance(Database db)
            => Math.Max(GetPaperTextHeight() * 2.0 * GetScaleFactor(db), 120.0);

        private static double GetTopDimLineY(Extents3d wallBounds, Extents3d occupiedBounds, Database db)
        {
            return Math.Max(
                wallBounds.MaxPoint.Y + GetModelFirstDimOffset(db),
                occupiedBounds.MaxPoint.Y + GetTagClearance(db));
        }

        private static double GetBottomDimLineY(Extents3d wallBounds, Extents3d occupiedBounds, Database db)
        {
            return Math.Min(
                wallBounds.MinPoint.Y - GetModelFirstDimOffset(db),
                occupiedBounds.MinPoint.Y - GetTagClearance(db));
        }

        private static double GetRightDimLineX(Extents3d wallBounds, Extents3d occupiedBounds, Database db)
        {
            double defaultX = wallBounds.MaxPoint.X + GetModelFirstDimOffset(db);
            bool hasOutsideTag = occupiedBounds.MaxPoint.X > wallBounds.MaxPoint.X + 5.0;
            return hasOutsideTag
                ? Math.Max(defaultX, occupiedBounds.MaxPoint.X + GetModelDimLineSpacing(db) * 0.6)
                : defaultX;
        }

        private static double GetLeftDimLineX(Extents3d wallBounds, Extents3d occupiedBounds, Database db)
        {
            double defaultX = wallBounds.MinPoint.X - GetModelFirstDimOffset(db);
            bool hasOutsideTag = occupiedBounds.MinPoint.X < wallBounds.MinPoint.X - 5.0;
            return hasOutsideTag
                ? Math.Min(defaultX, occupiedBounds.MinPoint.X - GetModelDimLineSpacing(db) * 0.6)
                : defaultX;
        }

        private static Extents3d GetOccupiedBounds(List<PanelPos> panels)
        {
            double minX = panels.Min(p => Math.Min(p.bounds.MinPoint.X, p.tagBounds.MinPoint.X));
            double minY = panels.Min(p => Math.Min(p.bounds.MinPoint.Y, p.tagBounds.MinPoint.Y));
            double maxX = panels.Max(p => Math.Max(p.bounds.MaxPoint.X, p.tagBounds.MaxPoint.X));
            double maxY = panels.Max(p => Math.Max(p.bounds.MaxPoint.Y, p.tagBounds.MaxPoint.Y));
            return new Extents3d(new Point3d(minX, minY, 0), new Point3d(maxX, maxY, 0));
        }

        private static Extents3d GetTextBounds(DBText txt, Point3d anchor, double textH)
        {
            try
            {
                return txt.GeometricExtents;
            }
            catch
            {
                double width = Math.Max(textH * Math.Max(txt.TextString?.Length ?? 1, 1) * 0.6, textH);
                bool vertical = Math.Abs(txt.Rotation - Math.PI / 2) < 0.01;

                return vertical
                    ? new Extents3d(
                        new Point3d(anchor.X - textH * 0.6, anchor.Y - width * 0.5, 0),
                        new Point3d(anchor.X + textH * 0.6, anchor.Y + width * 0.5, 0))
                    : new Extents3d(
                        new Point3d(anchor.X - width * 0.5, anchor.Y - textH * 0.6, 0),
                        new Point3d(anchor.X + width * 0.5, anchor.Y + textH * 0.6, 0));
            }
        }

        private static bool IsPointInside(Extents3d extents, Point3d point, double tolerance)
        {
            return point.X >= extents.MinPoint.X - tolerance &&
                   point.X <= extents.MaxPoint.X + tolerance &&
                   point.Y >= extents.MinPoint.Y - tolerance &&
                   point.Y <= extents.MaxPoint.Y + tolerance;
        }

        private static double DistanceToExtents(Point3d point, Extents3d extents)
        {
            double dx = 0;
            if (point.X < extents.MinPoint.X) dx = extents.MinPoint.X - point.X;
            else if (point.X > extents.MaxPoint.X) dx = point.X - extents.MaxPoint.X;

            double dy = 0;
            if (point.Y < extents.MinPoint.Y) dy = extents.MinPoint.Y - point.Y;
            else if (point.Y > extents.MaxPoint.Y) dy = point.Y - extents.MaxPoint.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool TryGetOpeningBounds(Entity ent, out Extents3d extents)
        {
            try
            {
                switch (ent)
                {
                    case Polyline pl when pl.Closed:
                        extents = pl.GeometricExtents;
                        return true;
                    case Hatch hatch:
                        extents = hatch.GeometricExtents;
                        return true;
                    default:
                        extents = default;
                        return false;
                }
            }
            catch
            {
                extents = default;
                return false;
            }
        }

        private static bool ContainsOpening(IEnumerable<OpeningPos> openings, Extents3d candidate)
        {
            const double tolerance = 1.0;

            return openings.Any(existing =>
                Math.Abs(existing.Bounds.MinPoint.X - candidate.MinPoint.X) <= tolerance &&
                Math.Abs(existing.Bounds.MinPoint.Y - candidate.MinPoint.Y) <= tolerance &&
                Math.Abs(existing.Bounds.MaxPoint.X - candidate.MaxPoint.X) <= tolerance &&
                Math.Abs(existing.Bounds.MaxPoint.Y - candidate.MaxPoint.Y) <= tolerance);
        }

        private static Dimension CreateLinearDim(
            Point3d pt1, Point3d pt2, Point3d dimPt, ObjectId styleId)
        {
            Dimension dim;
            const double tolerance = 1e-6;

            if (Math.Abs(pt1.Y - pt2.Y) <= tolerance)
            {
                dim = new RotatedDimension(0, pt1, pt2, dimPt, "", styleId);
            }
            else if (Math.Abs(pt1.X - pt2.X) <= tolerance)
            {
                dim = new RotatedDimension(Math.PI / 2, pt1, pt2, dimPt, "", styleId);
            }
            else
            {
                dim = new AlignedDimension(pt1, pt2, dimPt, "", styleId);
            }

            dim.Annotative = AnnotativeStates.True;
            return dim;
        }
    }
}
