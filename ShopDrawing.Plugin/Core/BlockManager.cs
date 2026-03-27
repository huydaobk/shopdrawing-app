using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Internal;
using ShopDrawing.Plugin.Models;
using Panel = ShopDrawing.Plugin.Models.Panel;
using Opening = ShopDrawing.Plugin.Models.Opening;

namespace ShopDrawing.Plugin.Core
{
    public class BlockManager
    {
        // -----------------------------------------------------------
        // Hatch theo chieu day — MOI CHIEU DAY LA 1 HINH DANG KHAC HAN
        // Nhin tu xa cung phan biet duoc ngay, in den trang van ro
        //   50mm  : ANSI31   → Đường chéo đơn (mỏng nhẹ nhất)
        //   60mm  : DOTS     → Chấm bi (khác hẳn đường kẻ)
        //   75mm  : HEX      → Lục giác (hình học rõ ràng)
        //   80mm  : EARTH    → Đất/cát (texture tự nhiên)
        //   100mm : HONEY    → Tổ ong (pattern đặc trưng nhất)
        //   125mm : NET      → Ô lưới vuông
        //   150mm : CROSS    → Dấu cộng +++ (cực dễ nhận)
        //   175mm : ANSI37   → Đa đường chéo dày
        //   180mm : BRICK    → Gạch xây (ngang dọc)
        //   200mm : SACNCR   → Bê tông (nặng nhất)
        // -----------------------------------------------------------
        // Tuple: (pattern, scale, angleDegrees)
        // ---------------------------------------------------------------
        // PatternScale = baseScale × scaleFactor (annotation scale, VD: 100 cho 1:100)
        // → Hatch luôn có mật độ như nhau trên giấy dù thay đổi tỷ lệ bản vẽ.
        // Công thức: ANSI31 spacing (mm/paper) = baseScale × scaleFactor × 3.175
        // VD: baseScale=0.15 | scale=1:100 → 0.15×100×3.175 = 47.6mm model = 0.48mm paper ✓
        // ---------------------------------------------------------------
        private static readonly Dictionary<int, (string pattern, double scale, double angleDeg)> HatchMap = new()
        {
            { 50,  ("ANSI31", 0.50,   0) },  // ~1.6mm trên paper — thoáng, dễ đọc
            { 60,  ("DOTS",   0.20,   0) },
            { 75,  ("HEX",    0.25,   0) },
            { 80,  ("EARTH",  0.25,   0) },
            { 100, ("HONEY",  0.25,   0) },
            { 125, ("NET",    0.30,   0) },
            { 150, ("CROSS",  0.25,   0) },
            { 175, ("ANSI37", 0.30,   0) },
            { 180, ("BRICK",  0.20,   0) },
            { 200, ("SACNCR", 0.20,   0) },
        };


        /// <summary>
        /// Public API: Tao tat ca layer chuan SD_* neu chua co.
        /// Goi truoc khi su dung bat ky layer nao.
        /// </summary>
        public void EnsureLayers(Transaction tr)
        {
            Database db = HostApplicationServices.WorkingDatabase;
            EnsureLayersExist(db, tr);
        }

        public void DrawAllPanels(List<Panel> panels, Transaction tr)
        {
            Database db = HostApplicationServices.WorkingDatabase;
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            EnsureLayersExist(db, tr);

            // Thu thập tất cả hatch IDs để MoveToBottom MỘT LẦN sau loop
            // (gọi MoveToBottom từng entity trong loop → AutoCAD fail silently một số tấm đầu)
            var allHatchIds = new ObjectIdCollection();
            foreach (var panel in panels)
            {
                ObjectId hatchId = DrawPanel(panel, tr, ms);
                if (!hatchId.IsNull) allHatchIds.Add(hatchId);
            }

            // Đưa toàn bộ hatch xuống dưới cùng — SAU KHI tất cả panel đã được tạo
            if (allHatchIds.Count > 0)
            {
                DrawOrderTable dot = (DrawOrderTable)tr.GetObject(ms.DrawOrderTableId, OpenMode.ForWrite);
                dot.MoveToBottom(allHatchIds);
            }
        }

        // Trả về ObjectId của hatch (để DrawAllPanels gộp MoveToBottom)
        private ObjectId DrawPanel(Panel panel, Transaction tr, BlockTableRecord ms)
        {
            ObjectId outlineId  = DrawOutline(panel, tr, ms);
            ObjectId hatchId    = DrawHatch(outlineId, panel, tr, ms);
            var jointIds        = DrawJointLines(panel, tr, ms);
            ObjectId tagId      = InsertTag(panel, tr, ms);   // Tag+Spec gộp 1 text: "W1-01 / Spec1"
            var signIds         = DrawJointSigns(panel, tr, ms);

            // Gom vào group
            var allIds = new ObjectIdCollection { outlineId, hatchId, tagId }.JoinWith(jointIds).JoinWith(signIds);
            EntityIdsToGroup(allIds, panel.PanelId, tr);
            return hatchId;
        }

        private ObjectId DrawOutline(Panel panel, Transaction tr, BlockTableRecord ms)
        {
            // Ngang: Length chạy theo X, Width chạy theo Y
            // Dọc:   Width chạy theo X, Length chạy theo Y (mặc định)
            double drawW = panel.IsHorizontal ? panel.LengthMm : panel.WidthMm;
            double drawH = panel.IsHorizontal ? panel.WidthMm : panel.LengthMm;

            Polyline pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(panel.X, panel.Y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(panel.X + drawW, panel.Y), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(panel.X + drawW, panel.Y + drawH), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(panel.X, panel.Y + drawH), 0, 0, 0);
            pl.Closed = true;
            pl.Layer = "SD_PANEL";

            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            return pl.ObjectId;
        }

        private ObjectId DrawHatch(ObjectId outlineId, Panel panel, Transaction tr, BlockTableRecord ms)
        {
            Hatch hatch = new Hatch();
            ms.AppendEntity(hatch);
            tr.AddNewlyCreatedDBObject(hatch, true);

            var (pattern, baseScale, angleDeg) = HatchMap.TryGetValue(panel.ThickMm, out var val)
                ? val
                : ("ANSI31", 0.15, 45.0); // default

            // PatternScale = baseScale × scaleFactor → scale-aware hatch:
            // mật độ đường kẻ luôn đồng nhất trên giấy dù tỷ lệ 1:50 hay 1:200.
            double scaleFactor = GetDrawingScale(ms.Database);
            hatch.SetHatchPattern(HatchPatternType.PreDefined, pattern);
            hatch.Layer = "SD_HATCH";
            hatch.PatternScale = baseScale * scaleFactor;

            // Goc hatch theo chieu day (de phan biet bang mat thuong)
            hatch.PatternAngle = angleDeg * Math.PI / 180.0;

            // Mau: ByLayer (mac dinh) de in den trang dung chuan.
            // Tam tai su dung: danh dau bang duong cheo nguoc + mau xam
            // Tấm tận dụng (IsReused): góc 135° để phân biệt với tấm mới.
            // KHÔNG dùng ACI 8 (xám tối) vì sẽ invisible trên nền AutoCAD tối.
            // Dùng ACI 253 (xám nhạt) giống tấm cắt, chỉ khác góc.
            if (panel.IsReused)
            {
                hatch.PatternAngle = Math.PI * 0.75; // 135° — ngược chiều tấm mới
                hatch.Color = Color.FromColorIndex(ColorMethod.ByAci, 253); // xám nhạt, visible
            }
            // Tấm cắt (không tái sử dụng): xám nhạt, giữ góc từ HatchMap
            else if (panel.IsCutPanel)
            {
                hatch.Color = Color.FromColorIndex(ColorMethod.ByAci, 253);
            }

            // Transparency 70% de text hien qua hatch
            hatch.Transparency = new Transparency(70);

            hatch.AppendLoop(HatchLoopTypes.Outermost, new ObjectIdCollection { outlineId });
            hatch.EvaluateHatch(true);

            // NOTE: KHÔNG gọi MoveToBottom ở đây.
            // DrawAllPanels sẽ gom tất cả hatch IDs và gọi MoveToBottom 1 lần sau loop.
            return hatch.ObjectId;
        }

        private List<ObjectId> DrawJointLines(Panel panel, Transaction tr, BlockTableRecord ms)
        {
            var ids = new List<ObjectId>();
            double drawW = panel.IsHorizontal ? panel.LengthMm : panel.WidthMm;
            double drawH = panel.IsHorizontal ? panel.WidthMm : panel.LengthMm;

            // Gán layer theo loại ngàm
            string layerLeft  = panel.JointLeft  == JointType.Cut ? "SD_JOINT_MALE" : "SD_JOINT_FEMALE";
            string layerRight = panel.JointRight == JointType.Cut ? "SD_JOINT_MALE" : "SD_JOINT_FEMALE";

            if (panel.IsHorizontal)
            {
                // Ngang: ngàm ở cạnh trên/dưới (theo Y)
                // "Left" = cạnh dưới, "Right" = cạnh trên
                Line bottom = new Line(new Point3d(panel.X, panel.Y, 0), new Point3d(panel.X + drawW, panel.Y, 0));
                bottom.Layer = layerLeft;
                ids.Add(ms.AppendEntity(bottom));
                tr.AddNewlyCreatedDBObject(bottom, true);

                Line top = new Line(new Point3d(panel.X, panel.Y + drawH, 0), new Point3d(panel.X + drawW, panel.Y + drawH, 0));
                top.Layer = layerRight;
                ids.Add(ms.AppendEntity(top));
                tr.AddNewlyCreatedDBObject(top, true);
            }
            else
            {
                // Dọc: ngàm ở cạnh trái/phải (theo X) — mặc định
                Line left = new Line(new Point3d(panel.X, panel.Y, 0), new Point3d(panel.X, panel.Y + drawH, 0));
                left.Layer = layerLeft;
                ids.Add(ms.AppendEntity(left));
                tr.AddNewlyCreatedDBObject(left, true);

                Line right = new Line(new Point3d(panel.X + drawW, panel.Y, 0), new Point3d(panel.X + drawW, panel.Y + drawH, 0));
                right.Layer = layerRight;
                ids.Add(ms.AppendEntity(right));
                tr.AddNewlyCreatedDBObject(right, true);
            }

            return ids;
        }

        /// <summary>
        /// Vẽ dấu ngàm -/+ ở 2 mép tấm (annotative theo tỷ lệ bản vẽ).
        /// </summary>
        private List<ObjectId> DrawJointSigns(Panel panel, Transaction tr, BlockTableRecord ms)
        {
            var ids = new List<ObjectId>();
            double drawW = panel.IsHorizontal ? panel.LengthMm : panel.WidthMm;
            double drawH = panel.IsHorizontal ? panel.WidthMm : panel.LengthMm;
            double scaleFactor = GetDrawingScale(ms.Database);
            var arialStyleId = EnsureArialStyle(ms.Database, tr);

            double textH  = Math.Max(2.5 * scaleFactor, 15.0);
            double offset    = textH * 0.6;   // dấu ngàm +/- trong tấm
            double offsetZero = textH * 0.8;  // dấu "0" ngoài cạnh — xa hơn để không đè tag

            // Quy tắc: dấu "+"/"-" (ngàm nhà máy) luôn ở TRONG tấm.
            // Chỉ dấu "0" (mép cắt tự do) mới đưa ra NGOÀI để tránh che đè/lẫn với ngàm kế bên.
            string JointSign(JointType j) => j switch
            {
                JointType.Male   => "+",
                JointType.Female => "-",
                _                => panel.IsCutPanel ? "0" : "-"
            };

            // Helper: vị trí dấu theo cạnh — zero = mép cut → ra ngoài; còn lại → trong
            bool IsZeroSign(string sign) => sign == "0";

            if (panel.IsHorizontal)
            {
                double midX = panel.X + drawW / 2;

                // Bottom (JointLeft)
                string signBottom = JointSign(panel.JointLeft);
                // Dấu "0" ở cạnh dưới → ra ngoài phía dưới; dấu ngàm → trong tấm
                double yBottom = IsZeroSign(signBottom)
                    ? panel.Y - offsetZero      // ngoài cạnh dưới
                    : panel.Y + offset;         // trong tấm

                var txtBottom = new DBText();
                txtBottom.Layer       = "SD_TAG";
                txtBottom.TextStyleId = arialStyleId;
                txtBottom.Height      = textH;
                txtBottom.TextString  = signBottom;
                txtBottom.HorizontalMode = TextHorizontalMode.TextCenter;
                txtBottom.VerticalMode   = TextVerticalMode.TextVerticalMid;
                txtBottom.AlignmentPoint = new Point3d(midX, yBottom, 0);
                ids.Add(ms.AppendEntity(txtBottom));
                tr.AddNewlyCreatedDBObject(txtBottom, true);

                // Top (JointRight)
                string signTop = JointSign(panel.JointRight);
                double yTop = IsZeroSign(signTop)
                    ? panel.Y + drawH + offsetZero  // ngoài cạnh trên
                    : panel.Y + drawH - offset;     // trong tấm

                var txtTop = new DBText();
                txtTop.Layer       = "SD_TAG";
                txtTop.TextStyleId = arialStyleId;
                txtTop.Height      = textH;
                txtTop.TextString  = signTop;
                txtTop.HorizontalMode = TextHorizontalMode.TextCenter;
                txtTop.VerticalMode   = TextVerticalMode.TextVerticalMid;
                txtTop.AlignmentPoint = new Point3d(midX, yTop, 0);
                ids.Add(ms.AppendEntity(txtTop));
                tr.AddNewlyCreatedDBObject(txtTop, true);
            }
            else
            {
                double midY = panel.Y + drawH / 2;

                // Left (JointLeft) — dấu ngàm nhà máy, LUÔN trong tấm
                string signL = JointSign(panel.JointLeft);
                double xLeft = IsZeroSign(signL)
                    ? panel.X - offsetZero      // ngoài cạnh trái (hiếm: tấm bị cắt cả 2 đầu)
                    : panel.X + offset;         // trong tấm (phổ biến)

                var txtL = new DBText();
                txtL.Layer       = "SD_TAG";
                txtL.TextStyleId = arialStyleId;
                txtL.Height      = textH;
                txtL.TextString  = signL;
                txtL.HorizontalMode = TextHorizontalMode.TextCenter;
                txtL.VerticalMode   = TextVerticalMode.TextVerticalMid;
                txtL.AlignmentPoint = new Point3d(xLeft, midY, 0);
                ids.Add(ms.AppendEntity(txtL));
                tr.AddNewlyCreatedDBObject(txtL, true);

                // Right (JointRight) — nếu là "0" thì ra NGOÀI phải
                string signR = JointSign(panel.JointRight);
                double xRight = IsZeroSign(signR)
                    ? panel.X + drawW + offsetZero  // ngoài cạnh phải (mép cắt tự do)
                    : panel.X + drawW - offset;     // trong tấm (ngàm nhà máy)

                var txtR = new DBText();
                txtR.Layer       = "SD_TAG";
                txtR.TextStyleId = arialStyleId;
                txtR.Height      = textH;
                txtR.TextString  = signR;
                txtR.HorizontalMode = TextHorizontalMode.TextCenter;
                txtR.VerticalMode   = TextVerticalMode.TextVerticalMid;
                txtR.AlignmentPoint = new Point3d(xRight, midY, 0);
                ids.Add(ms.AppendEntity(txtR));
                tr.AddNewlyCreatedDBObject(txtR, true);
            }

            return ids;
        }

        private ObjectId InsertTag(Panel panel, Transaction tr, BlockTableRecord ms)
        {
            double scaleFactor = GetDrawingScale(ms.Database);
            var arialStyleId = EnsureArialStyle(ms.Database, tr);
            double drawW = panel.IsHorizontal ? panel.LengthMm : panel.WidthMm;
            double drawH = panel.IsHorizontal ? panel.WidthMm : panel.LengthMm;

            // Text height cố định theo tỷ lệ bản vẽ (không scale theo panel)
            double paperHeightMm = ShopDrawing.Plugin.Commands.ShopDrawingCommands.DefaultTextHeightMm;
            double textHeight = paperHeightMm * scaleFactor;
            textHeight = Math.Max(textHeight, 20.0);

            // Nội dung: PanelId + Spec gộp 1 dòng (đã chốt: không tách 2 text riêng)
            string statusIcon = panel.IsCutPanel ? " \u2702" : (panel.IsReused ? " \u267b" : "");
            string specPart = string.IsNullOrEmpty(panel.Spec) ? "" : $" / {panel.Spec}";
            string tagText = $"{panel.PanelId}{statusIcon}{specPart}";

            // Ngưỡng: nếu khổ rộng tấm < 500mm → text đặt bên ngoài (đồng bộ với DrawJointSigns)
            // Dùng panel.WidthMm (khổ rộng thực tế) thay vì drawW (vì drawW = LengthMm cho tấm ngang)
            bool isNarrow = panel.WidthMm < 500;

            var txt = new DBText();
            txt.Layer = "SD_TAG";
            txt.TextStyleId = arialStyleId;
            txt.Height = textHeight;
            txt.TextString = tagText;

            // Tấm ngang: text nằm ngang (rotation=0).  Tấm dọc: xoay 90°.
            double textRotation = panel.IsHorizontal ? 0 : Math.PI / 2;

            if (isNarrow)
            {
                // Tấm hẹp: tag đặt NGOÀI tấm.
                txt.HorizontalMode = TextHorizontalMode.TextCenter;
                txt.VerticalMode   = TextVerticalMode.TextVerticalMid;

                if (panel.IsHorizontal)
                {
                    // Tấm ngang hẹp: tag PHÍA TRÊN tấm, nằm ngang
                    double midX    = panel.X + drawW / 2;
                    double aboveY  = panel.Y + drawH + textHeight * 2.5;
                    txt.AlignmentPoint = new Point3d(midX, aboveY, 0);
                }
                else
                {
                    // Tấm dọc hẹp: tag BÊN PHẢI tấm, xoay 90°
                    // "0" ở X+W+0.8*textH, tag ở X+W+2.5*textH → gap tránh đè nhau.
                    double outsideX = panel.X + drawW + textHeight * 2.5;
                    double midY     = panel.Y + drawH / 2;
                    txt.AlignmentPoint = new Point3d(outsideX, midY, 0);
                }
                txt.Rotation = textRotation;
            }
            else
            {
                // Tấm bình thường: căn giữa tấm
                txt.HorizontalMode = TextHorizontalMode.TextCenter;
                txt.VerticalMode   = TextVerticalMode.TextVerticalMid;
                double centerX = panel.X + drawW / 2;
                double centerY = panel.Y + drawH / 2;
                txt.AlignmentPoint = new Point3d(centerX, centerY, 0);
                txt.Rotation = textRotation;
            }

            ms.AppendEntity(txt);
            tr.AddNewlyCreatedDBObject(txt, true);
            return txt.ObjectId;
        }

        private string GetValueForAttr(string tag, Panel p)
        {
            return tag switch
            {
                "PANEL_ID"   => p.PanelId,
                "SPEC"       => p.Spec,
                "DIMENSIONS" => $"{p.WidthMm:F0} × {p.LengthMm:F0}",
                "THICKNESS"  => $"{p.ThickMm}mm",
                "STATUS"     => p.IsReused ? "TÁI SỬ DỤNG" : "MỚI",
                "SOURCE_ID"  => p.SourceId ?? "-",
                _ => ""
            };
        }

        // InsertSpecTag đã được gộp vào InsertTag — 1 DBText duy nhất "W1-01 / Spec1"

        // ========================================
        // HE THONG LAYER CHUAN — MOI LAYER 1 MAU RIENG
        // ========================================
        // ACI Color Index Reference (AutoCAD standard):
        //   1=Red, 2=Yellow, 3=Green, 4=Cyan, 5=Blue,
        //   6=Magenta, 7=White, 8=DarkGray, 9=LightGray
        //   30=Orange, 40=OrangeYellow, 140=LightCyan,
        //   150=Teal, 200=Lavender, 210=LightPurple, 253=VeryLightGray
        // ========================================

        private static readonly Dictionary<string, (int colorAci, LineWeight lineWeight, bool isPlottable, string description)> LayerConfig = new()
        {
            // TẤM PANEL — Xanh dương đậm — nét chính 0.30mm — IN ĐƯỢC
            { "SD_PANEL",       (5,   LineWeight.LineWeight030, true,  "Đường viền tấm panel") },

            // HATCH TẤM — Xám nhạt 253 — nét mỏng — KHÔNG IN (trang trí)
            { "SD_HATCH",       (253, LineWeight.LineWeight013, false, "Hatch pattern tấm panel") },

            // ĐƯỜNG NGÀM MALE (Cut) — Đỏ 1 — nét 0.25mm — IN ĐƯỢC
            { "SD_JOINT_MALE",  (1,   LineWeight.LineWeight025, true,  "Ngàm Male / Cut (đỏ)") },

            // ĐƯỜNG NGÀM FEMALE — Cyan 4 — nét 0.25mm — IN ĐƯỢC
            { "SD_JOINT_FEMALE",(4,   LineWeight.LineWeight025, true,  "Ngàm Female (cyan)") },

            // TAG TEXT — Xanh lá — nét mặc định — KHÔNG IN (chỉ xem trên model)
            { "SD_TAG",         (3,   LineWeight.ByLineWeightDefault, false, "Mã tấm, spec, trạng thái") },

            // LỖ MỞ (OPENING) — Cam 30 — nét dày 0.50mm — IN ĐƯỢC
            { "SD_OPENING",     (30,  LineWeight.LineWeight050, true,  "Đường viền lỗ mở (cửa, cửa sổ)") },

            // CHI TIẾT PHỤ KIỆN — Tím 200 — nét 0.18mm — IN ĐƯỢC
            { "SD_DETAIL",      (200, LineWeight.LineWeight018, true,  "Block chi tiết phụ kiện (Base-U, Top Cap, Corner)") },

            // KÍCH THƯỚC — Cyan 4 — nét mỏng — IN ĐƯỢC
            { "SD_DIM",         (4,   LineWeight.LineWeight013, true,  "Kích thước ghi chú") },
        };

        private void EnsureLayersExist(Database db, Transaction tr)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

            foreach (var kvp in LayerConfig)
            {
                string layerName = kvp.Key;
                var (colorAci, lineWeight, isPlottable, _) = kvp.Value;

                if (!lt.Has(layerName))
                {
                    var ltr = new LayerTableRecord();
                    ltr.Name = layerName;
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)colorAci);
                    ltr.LineWeight = lineWeight;
                    ltr.IsPlottable = isPlottable;
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }
                else
                {
                    // Layer da ton tai — cap nhat mau va LineWeight cho dung chuan
                    var ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForWrite);
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)colorAci);
                    ltr.LineWeight = lineWeight;
                    ltr.IsPlottable = isPlottable;
                }
            }
        }

        /// <summary>
        /// Tạo hoặc lấy TextStyle "SD_Arial" dùng font Arial TTF.
        /// Gán vào txt.TextStyleId khi tạo DBText.
        /// </summary>
        internal static ObjectId EnsureArialStyle(Database db, Transaction tr)
        {
            const string styleName = "SD_Arial";
            var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (tst.Has(styleName))
                return tst[styleName];

            tst.UpgradeOpen();
            var style = new TextStyleTableRecord
            {
                Name            = styleName,
                FileName        = "arial.ttf",
                BigFontFileName = "",
                TextSize        = 0,   // 0 = dùng height của entity
                XScale          = 1.0,
                ObliquingAngle  = 0
            };
            ObjectId id = tst.Add(style);
            tr.AddNewlyCreatedDBObject(style, true);
            return id;
        }

        /// Dung de tinh kich thuoc text tren Model Space.
        /// </summary>
        /// <summary>
        /// Đọc tỷ lệ annotation hiện hành từ AutoCAD (db.Cannoscale).
        /// Fallback về DefaultAnnotationScales nếu không đọc được.
        /// </summary>
        private static double GetDrawingScale(Database db)
        {
            try
            {
                // Cannoscale = Current Annotation Scale đang hiện trên thanh trạng thái AutoCAD
                var acScale = db.Cannoscale;
                if (acScale != null && acScale.DrawingUnits > 0)
                    return acScale.DrawingUnits / acScale.PaperUnits; // VD: 100/1 = 100 cho 1:100
            }
            catch { }
            // Fallback: dùng setting thủ công từ palette
            return ParsePrimaryScale(ShopDrawing.Plugin.Commands.ShopDrawingCommands.DefaultAnnotationScales);
        }

        private static double ParsePrimaryScale(string annotationScales)
        {
            try
            {
                string primary = annotationScales.Split(',')[0].Trim();
                var parts = primary.Split(':');
                if (parts.Length == 2 && double.TryParse(parts[1], out double denominator) && denominator > 0)
                    return denominator;
            }
            catch { }
            return 100.0;
        }

        /// <summary>
        /// Tu dong gan cac ti le Annotation cho mot entity.
        /// Vi du: 1:100, 1:80. Text se tu scale dung kich thuoc tren moi viewport.
        /// </summary>
        private void AssignAnnotationScales(Entity entity, Database db, params string[] scaleNames)
        {
            try
            {
                ObjectContextManager ocm = db.ObjectContextManager;
                ObjectContextCollection occ = ocm.GetContextCollection("ACDB_ANNOTATIONSCALES");

                foreach (var scaleName in scaleNames)
                {
                    // GetContext tra ve ObjectContext, AddContext nhan ObjectContext
                    ObjectContext scale = occ.GetContext(scaleName);
                    if (scale != null)
                    {
                        ObjectContexts.AddContext(entity, scale);
                    }
                }
            }
            catch (System.Exception)
            {
                // Scale chua co trong danh sach CAD -> bo qua, khong crash
            }
        }

        private void EntityIdsToGroup(ObjectIdCollection ids, string name, Transaction tr)
        {
            Database db = HostApplicationServices.WorkingDatabase;
            DBDictionary gd = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForWrite);
            Group grp = new Group(name, true);
            gd.SetAt("*", grp); // Tên tự động hoặc đặt tên theo name
            tr.AddNewlyCreatedDBObject(grp, true);
            foreach (ObjectId id in ids)
            {
                grp.Append(id);
            }
        }

        // ========================================
        // Feature A+D: Ve duong vien + hatch lo mo
        // ========================================

        /// <summary>
        /// Ve tat ca cac lo mo len ban ve (duong vien cam + hatch cross nhat)
        /// </summary>
        public void DrawOpenings(List<Opening> openings, Transaction tr)
        {
            if (openings == null || openings.Count == 0) return;

            Database db = HostApplicationServices.WorkingDatabase;
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            EnsureLayersExist(db, tr);

            foreach (var opening in openings)
            {
                DrawOpeningBoundary(opening, tr, ms);
            }
        }

        /// <summary>
        /// Ve 1 lo mo: polyline vien cam + hatch cross nhat + label kich thuoc
        /// </summary>
        private void DrawOpeningBoundary(Opening o, Transaction tr, BlockTableRecord ms)
        {
            double cx = o.X + o.Width / 2;
            double cy = o.Y + o.Height / 2;
            double scaleFactor = GetDrawingScale(ms.Database);
            var arialStyleId = EnsureArialStyle(ms.Database, tr);

            // 1. Polyline viền cam — nét dày 0.50mm
            Polyline pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(o.X, o.Y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(o.X + o.Width, o.Y), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(o.X + o.Width, o.Y + o.Height), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(o.X, o.Y + o.Height), 0, 0, 0);
            pl.Closed = true;
            pl.Layer = "SD_OPENING";
            pl.LineWeight = LineWeight.LineWeight050;
            pl.Color = Color.FromColorIndex(ColorMethod.ByAci, 30); // Cam

            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);

            // 2. Hatch ANSI31 (chéo đơn) — cam, 40% trong suốt (dễ thấy hơn 80%)
            Hatch hatch = new Hatch();
            ms.AppendEntity(hatch);
            tr.AddNewlyCreatedDBObject(hatch, true);

            hatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
            hatch.Layer = "SD_OPENING";
            hatch.PatternScale = 400; // cố định model-space (mm), KHÔNG nhân scaleFactor
            hatch.Color = Color.FromColorIndex(ColorMethod.ByAci, 30); // Cam
            hatch.Transparency = new Transparency(40); // 40% — thấy rõ trên nền tối

            hatch.AppendLoop(HatchLoopTypes.Outermost, new ObjectIdCollection { pl.ObjectId });
            hatch.EvaluateHatch(true);

            // Đưa hatch xuống dưới cùng
            DrawOrderTable dot = (DrawOrderTable)tr.GetObject(ms.DrawOrderTableId, OpenMode.ForWrite);
            dot.MoveToBottom(new ObjectIdCollection { hatch.ObjectId });

            // 3. Label kích thước — 2 dòng ở giữa lỗ mở
            //    Dòng 1: "LỖ MỞ"  Dòng 2: "W × H"
            double textH = Math.Max(ShopDrawing.Plugin.Commands.ShopDrawingCommands.DefaultTextHeightMm * scaleFactor, 30.0);
            double lineSpacing = textH * 1.8;

            string[] lines =
            {
                "LỖ MỞ",
                $"{o.Width:F0} × {o.Height:F0}"
            };

            // Bắt đầu từ trên giữa, đi xuống
            double totalH = lineSpacing * (lines.Length - 1);
            double startY  = cy + totalH / 2;

            foreach (var line in lines)
            {
                var txt = new DBText();
                txt.Layer = "SD_OPENING";
                txt.TextStyleId = arialStyleId;
                txt.Height = (line == "OPENING") ? textH * 1.2 : textH;
 // Tiêu đề to hơn
                txt.TextString = line;
                txt.HorizontalMode = TextHorizontalMode.TextCenter;
                txt.VerticalMode   = TextVerticalMode.TextVerticalMid;
                txt.AlignmentPoint = new Point3d(cx, startY, 0);
                txt.Color = Color.FromColorIndex(ColorMethod.ByAci, 30); // Cam

                ms.AppendEntity(txt);
                tr.AddNewlyCreatedDBObject(txt, true);
                startY -= lineSpacing;
            }
        }

    }

    public static class ObjectIdCollectionExtensions
    {
        public static ObjectIdCollection JoinWith(this ObjectIdCollection coll, IEnumerable<ObjectId> other)
        {
            foreach (var id in other) coll.Add(id);
            return coll;
        }
    }
}
