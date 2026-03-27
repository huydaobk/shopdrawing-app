using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(ShopDrawing.Plugin.Commands.ShopDrawingCommands))]


namespace ShopDrawing.Plugin.Commands
{
    public class ShopDrawingCommands
    {
        public static readonly SpecConfigManager SpecManager = new();
        // DB path: luu vao public/ folder trong workspace de tranh mat DB khi AutoCAD doi thu muc
        private static readonly string WasteDbPath = System.IO.Path.Combine(
            @"c:\my_project\shopdrawing-app\public", "shopdrawing_waste.db");
        public static WasteRepository WasteRepo { get; private set; } = null!;
        public static readonly BomManager BomManager = new();
        private static readonly LayoutEngine LayoutEngine = new();
        public static readonly BlockManager BlockManager = new();

        // Phase 09C: Global Settings
        public static int DefaultThickness { get; set; } = 50;
        public static string DefaultAnnotationScales { get; set; } = "1:100";
        public static double DefaultTextHeightMm { get; set; } = 1.8; // chiều cao chữ trên giấy (mm)
        public static bool EnableOpeningCut { get; set; } = false;
        public static ShopDrawing.Plugin.Models.DetailType CurrentDetailType { get; set; } = ShopDrawing.Plugin.Models.DetailType.All;

        /// <summary>
        /// Fire khi AutoCAD đổi Annotation Scale → Palette tự cập nhật dropdown.
        /// </summary>
        public static event System.Action<string>? AnnotationScaleChanged;
        public static void FireAnnotationScaleChanged(string scaleName)
        {
            DefaultAnnotationScales = scaleName;
            AnnotationScaleChanged?.Invoke(scaleName);
        }

        /// <summary>
        /// Fire sau mỗi lần lưu tấm lẻ vào kho — WasteManagerDialog subscribe để tự refresh.
        /// </summary>
        public static event System.Action? WasteUpdated;
        public static void NotifyWasteUpdated() => WasteUpdated?.Invoke();

        // Palette Quick Settings
        public static int WallCounter { get; set; } = 1;
        public static string DefaultWallCode { get; set; } = "W1";
        public static event Action<string>? WallCodeChanged;
        public static double DefaultPanelWidth { get; set; } = 1100;
        public static string DefaultSpec { get; set; } = "";
        public static double DefaultJointGap { get; set; } = 2;
        public static Core.LayoutDirection DefaultDirection { get; set; } = Core.LayoutDirection.Horizontal;
        public static Core.StartEdge DefaultStartEdge { get; set; } = Core.StartEdge.Left;
        
#pragma warning disable CS8618
        private static PaletteSet _paletteSet;
#pragma warning restore CS8618

        static ShopDrawingCommands()
        {
            // Khởi tạo WasteRepo an toàn, không crash nếu SQLite lỗi
            try
            {
                WasteRepo = new WasteRepository(WasteDbPath);
            }
            catch (System.Exception ex)
            {
                // Log lỗi, nhưng không crash. WasteRepo sẽ là null.
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(@"c:\my_project\shopdrawing-app\public", "plugin_errors.log"),
                    $"[{System.DateTime.Now}] WasteRepo init failed: {ex.Message}\n{ex.StackTrace}\n\n");
            }

            try
            {
                BomManager.RegisterReactor();
            }
            catch (System.Exception)
            {
                // AutoCAD static constructors can be tricky
            }
        }

        [CommandMethod("SD_PANEL")]
        public void TogglePalette()
        {
            if (_paletteSet == null)
            {
                _paletteSet = new PaletteSet("ShopDrawing Tools");
                _paletteSet.Style = PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.ShowAutoHideButton;
                _paletteSet.MinimumSize = new System.Drawing.Size(250, 400);

                // Thêm UI panel vào Palette
                var myControl = new MainPaletteControl();
                
                // AddVisual cho WPF FrameworkElement
                _paletteSet.AddVisual("Tools", myControl);
            }
            // Toggle visibility
            _paletteSet.Visible = !_paletteSet.Visible;
        }

#pragma warning disable CS8618
        private static PaletteSet _smartDimPaletteSet;
#pragma warning restore CS8618

        /// <summary>
        /// SD_SMART_DIM — Mở/tắt Smart Dimension Palette.
        /// Không dùng command line; user chọn mode (Auto/Manual) trong palette.
        /// </summary>
        [CommandMethod("SD_SMART_DIM")]
        public void ToggleSmartDimPalette()
        {
            if (_smartDimPaletteSet == null)
            {
                _smartDimPaletteSet = new PaletteSet("Smart Dimension");
                _smartDimPaletteSet.Style = PaletteSetStyles.ShowCloseButton
                                          | PaletteSetStyles.ShowPropertiesMenu
                                          | PaletteSetStyles.ShowAutoHideButton;
                _smartDimPaletteSet.MinimumSize = new System.Drawing.Size(260, 380);
                _smartDimPaletteSet.AddVisual("Smart Dim", new SmartDimPaletteControl());
            }
            _smartDimPaletteSet.Visible = !_smartDimPaletteSet.Visible;
        }

#pragma warning disable CS8618
        private static PaletteSet _layoutPaletteSet;
#pragma warning restore CS8618

        /// <summary>
        /// SD_LAYOUT — Mở/tắt Layout Manager Palette.
        /// Cho phép tạo Layout tabs tự động theo wall groups.
        /// </summary>
        [CommandMethod("SD_LAYOUT")]
        public void ToggleLayoutPalette()
        {
            if (_layoutPaletteSet == null)
            {
                _layoutPaletteSet = new PaletteSet("Layout Manager");
                _layoutPaletteSet.Style = PaletteSetStyles.ShowCloseButton
                                        | PaletteSetStyles.ShowPropertiesMenu
                                        | PaletteSetStyles.ShowAutoHideButton;
                _layoutPaletteSet.MinimumSize = new System.Drawing.Size(320, 560);
                _layoutPaletteSet.AddVisual("Layout", new LayoutManagerPaletteControl());
            }
            _layoutPaletteSet.Visible = !_layoutPaletteSet.Visible;
        }

#pragma warning disable CS8618
        private static PaletteSet _exportPdfPaletteSet;
#pragma warning restore CS8618

        /// <summary>
        /// SD_EXPORT — Mở/tắt Export PDF Palette.
        /// </summary>
        [CommandMethod("SD_EXPORT")]
        public void ToggleExportPdfPalette()
        {
            if (_exportPdfPaletteSet == null)
            {
                _exportPdfPaletteSet = new PaletteSet("Export PDF");
                _exportPdfPaletteSet.Style = PaletteSetStyles.ShowCloseButton
                                        | PaletteSetStyles.ShowPropertiesMenu
                                        | PaletteSetStyles.ShowAutoHideButton;
                _exportPdfPaletteSet.MinimumSize = new System.Drawing.Size(300, 450);
                _exportPdfPaletteSet.AddVisual("Export PDF", new ExportPdfPaletteControl());
            }
            _exportPdfPaletteSet.Visible = !_exportPdfPaletteSet.Visible;
        }

        /// <summary>
        /// Wrapper command để bypass UI thread context issue khi export pdf
        /// </summary>
        [CommandMethod("_SD_PLOT_API_WRAPPER", CommandFlags.Session)]
        public void ExportPdfWrapper()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            if (doc == null || ed == null) return;
            
            var pending = ExportPdfPaletteControl.TakePendingRequest();
            if (pending == null) 
            {
                ed.WriteMessage("\n[SD] Không có yêu cầu xuất PDF.\n");
                return;
            }
            
            var options = pending.Options;
            var palette = pending.Palette;
            
            var engine = new PdfExportEngine();
            try
            {
                engine.ExportDrawingsToPdf(doc, options, (msg, curr, total) => 
                {
                    palette?.ReportProgress(msg, false, string.Empty, false);
                });
                
                palette?.ReportProgress($"✅ Đã xuất PDF thành công ({options.OutputFilePath})", true, options.OutputFilePath, options.OpenAfterExport);
            }
            catch (System.Exception ex)
            {
                palette?.ReportProgress($"❌ Lỗi xuất PDF: {ex.Message}", true, string.Empty, false);
                ed.WriteMessage($"\n[SD_EXPORT] ERROR: {ex.Message}\n{ex.StackTrace}\n");
            }
        }

        /// <summary>
        /// SD_LAYOUT_CREATE — gọi từ palette qua SendStringToExecute.
        /// Chạy trong Session context → LayoutManager.CreateLayout hoạt động.
        /// </summary>
        [CommandMethod("_SD_LAYOUT_CREATE", CommandFlags.Session)]
        public void CreateLayoutFromPalette()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var pending = LayoutManagerPaletteControl.TakePendingRequest();
            var request = pending?.Request;
            var palette = pending?.GetSource();

            if (request == null)
            {
                ed.WriteMessage("\n[SD] No pending layout request.");
                return;
            }

            try
            {
                var engine = new LayoutManagerEngine
                {
                    MarginLeft  = request.MarginLeft,
                    MarginRight = request.MarginRight,
                    MarginTop   = request.MarginTop,
                    MarginBot   = request.MarginBot,
                };

                var plan = engine.BuildLayoutPlan(request);
                int existingCount = plan.Count(item => DrawingListManager.LayoutExists(doc, item.LayoutName));
                int newCount = plan.Count - existingCount;
                string initialRevisionContent = plan
                    .Where(item => DrawingListManager.LayoutExists(doc, item.LayoutName))
                    .Select(item => DrawingListManager.GetLayoutMeta(doc, item.LayoutName).GhiChu)
                    .FirstOrDefault(content => !string.IsNullOrWhiteSpace(content))
                    ?? string.Empty;

                var revisionDialog = new LayoutRevisionDialog(newCount, existingCount, initialRevisionContent);
                if (Application.ShowModalWindow(revisionDialog) != true)
                {
                    palette?.Dispatcher.Invoke(() => palette.SetStatusPublic("⚠️ Đã huỷ."));
                    return;
                }

                var layouts = engine.CreateLayoutsFromRegion(request, revisionDialog.RevisionContent);

                palette?.Dispatcher.Invoke(() =>
                {
                    if (layouts.Count == 1)
                        palette.SetStatusPublic($"✅ Tạo: \"{layouts[0]}\"");
                    else
                        palette.SetStatusPublic($"✅ {layouts.Count} trang:\n" + string.Join("\n", layouts));
                    palette.RefreshLayoutListPublic();
                });
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[SD] ❌ Layout error: {ex.GetType().Name}: {ex.Message}");
                ed.WriteMessage($"\n[SD] StackTrace: {ex.StackTrace}");
                palette?.Dispatcher.Invoke(() => palette.SetStatusPublic($"❌ {ex.Message}"));
            }
        }

        /// <summary>
        /// SD_WALL_QUICK — Palette-driven: đọc settings từ palette, không hiện dialog.
        /// Chỉ prompt click polyline(s) trên bản vẽ.
        /// </summary>
        [CommandMethod("_SD_WALL_QUICK")]
        public void CreateWallQuick()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                // Đọc settings từ palette (static props)
                var request = new Core.LayoutRequest
                {
                    WallCode       = DefaultWallCode,
                    ThicknessMm    = DefaultThickness,
                    PanelWidthMm   = DefaultPanelWidth,
                    Spec           = DefaultSpec,
                    JointGapMm     = DefaultJointGap,
                    Direction      = DefaultDirection,
                    StartEdge      = DefaultStartEdge
                };

                ed.WriteMessage($"\n[SD_WALL_QUICK] Tường: {request.WallCode} | {request.PanelWidthMm:F0}mm | {request.ThicknessMm}mm | {request.Direction}");

                // Chọn Boundary Polyline
                var opt = new PromptEntityOptions("\n→ Click Polyline BIÊN TƯỜNG:");
                opt.SetRejectMessage("\nPhải là Polyline!");
                opt.AddAllowedClass(typeof(Polyline), true);
                var entRes = ed.GetEntity(opt);
                if (entRes.Status != PromptStatus.OK) return;

                // Chọn Opening Polylines (nếu bật)
                var openings = new List<Opening>();
                if (EnableOpeningCut)
                {
                    ed.WriteMessage("\n→ Click các Polyline LỖ MỞ (Enter bỏ qua):");
                    var pso = new PromptSelectionOptions { MessageForAdding = "\n   Chọn lỗ mở:", MessageForRemoval = "" };
                    var filter = new SelectionFilter(new TypedValue[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") });
                    var psr = ed.GetSelection(pso, filter);
                    if (psr.Status == PromptStatus.OK)
                    {
                        using var trX = doc.Database.TransactionManager.StartTransaction();
                        foreach (ObjectId id in psr.Value.GetObjectIds())
                        {
                            if (trX.GetObject(id, OpenMode.ForRead) is Polyline opl)
                            {
                                var ext = opl.GeometricExtents;
                                openings.Add(new Opening { X = ext.MinPoint.X, Y = ext.MinPoint.Y, Width = ext.MaxPoint.X - ext.MinPoint.X, Height = ext.MaxPoint.Y - ext.MinPoint.Y });
                            }
                        }
                        trX.Commit();
                    }
                }

                // Tính layout
                LayoutResult layout;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (tr.GetObject(entRes.ObjectId, OpenMode.ForRead) is not Polyline pl)
                    { ed.WriteMessage("\nLỗi: không phải Polyline."); return; }
                    request.BoundaryPolyline = pl;
                    request.Openings = openings;
                    layout = LayoutEngine.Calculate(request);
                    tr.Commit();
                }

                // Waste Matching — Logic mới
                if (layout.RemnantPanel != null && WasteRepo != null)
                {
                    ed.WriteMessage($"\n🔎 Tấm lẻ cần: Spec={layout.RemnantPanel.Spec} | Dày={layout.RemnantPanel.ThickMm}mm | Rộng={layout.RemnantPanel.WidthMm:F0}mm | Dài={layout.RemnantPanel.LengthMm:F0}mm");
                    
                    var matcher = new Core.WasteMatcher(WasteRepo);
                    var matchResult = matcher.FindBestMatchWithDirection(layout.RemnantPanel);

                    bool usedFromStock = false;

                    if (matchResult.Panel != null)
                    {
                        ed.WriteMessage($"\n✅ Tìm thấy tấm khớp: {matchResult.Panel.PanelCode} | Hướng: {matchResult.Direction}");

                        // DEBUG: hiện ngàm remnant vs ngàm kho
                        string remJ  = $"{SignOf(layout.RemnantPanel.JointLeft)}/{SignOf(layout.RemnantPanel.JointRight)}";
                        string fndJ  = $"{SignOf(matchResult.Panel.JointLeft)}/{SignOf(matchResult.Panel.JointRight)}";
                        ed.WriteMessage($"\n   Remnant cần: [{remJ}] | Kho có: [{fndJ}] | Direction: {matchResult.Direction}");
                        
                        // Hiện dialog đề xuất (gồm hướng lắp)
                        var suggestDialog = new UI.WasteSuggestionDialog(
                            layout.RemnantPanel, matchResult.Panel, matchResult.Direction);
                        if (Application.ShowModalWindow(suggestDialog) == true && suggestDialog.UseFromStock)
                        {
                            usedFromStock = true;
                            layout.RemnantPanel.IsReused = true;
                            layout.RemnantPanel.SourceId = matchResult.Panel.PanelCode;

                            // Cập nhật joints remnant theo hướng lắp
                            if (matchResult.Direction == Core.MatchDirection.Flipped)
                            {
                                // Flipped → đảo ngàm kho: 0/+ → +/0
                                layout.RemnantPanel.JointLeft = matchResult.Panel.JointRight;
                                layout.RemnantPanel.JointRight = matchResult.Panel.JointLeft;
                            }
                            else
                            {
                                // Direct → giữ nguyên ngàm kho
                                layout.RemnantPanel.JointLeft = matchResult.Panel.JointLeft;
                                layout.RemnantPanel.JointRight = matchResult.Panel.JointRight;
                            }

                            // Nếu Flipped → đảo ngàm ALL panels trừ remnant: -/+ → +/-
                            // Bao gồm: FullPanels + CutPanels (Case A: biên notch, Case B: split mảnh)
                            if (matchResult.Direction == Core.MatchDirection.Flipped)
                            {
                                int flippedCount = 0;
                                var panelsToFlip = layout.FullPanels.Concat(layout.CutPanels);
                                foreach (var p in panelsToFlip)
                                {
                                    var tmp = p.JointLeft;
                                    p.JointLeft = p.JointRight;
                                    p.JointRight = tmp;
                                    flippedCount++;
                                }
                                ed.WriteMessage($"\n🔄 Đã đổi chiều ngàm {flippedCount} tấm (chuẩn + biên): -/+ → +/-");
                            }

                            // Xóa tấm khỏi kho
                            matcher.AcceptReuse(matchResult.Panel.Id);
                            ed.WriteMessage($"\n♻️ Tận dụng tấm kho {matchResult.Panel.PanelCode} ({matchResult.Panel.WidthMm:F0}mm).");

                            // === THỐNG KÊ HAO HỤT: Cắt tận dụng dư (TRIM) ===
                            double trimWidth = matchResult.Panel.WidthMm - layout.RemnantPanel.WidthMm - Core.WasteMatcher.CUT_KERF_MM;
                            if (trimWidth > 1.0)
                            {
                                var trimWaste = new Models.WastePanel
                                {
                                    PanelCode = $"{matchResult.Panel.PanelCode}-TRIM",
                                    WidthMm = trimWidth,
                                    LengthMm = matchResult.Panel.LengthMm,
                                    ThickMm = matchResult.Panel.ThickMm,
                                    PanelSpec = matchResult.Panel.PanelSpec,
                                    JointLeft = Models.JointType.Cut,
                                    JointRight = Models.JointType.Cut,
                                    SourceWall = request.WallCode,
                                    Project = "current",
                                    Status = "discarded",
                                    SourceType = "TRIM"
                                };
                                WasteRepo.AddPanel(trimWaste);
                                NotifyWasteUpdated();
                                ed.WriteMessage($"\n🗑️ Phần dư tận dụng {trimWidth:F0}mm [0/0] → Đã bỏ (TRIM).");
                            }
                        }
                    }
                    else
                    {
                        // Debug: kiểm tra kho có gì không
                        var allWaste = WasteRepo.GetAll().Where(w => w.Status == "available").ToList();
                        ed.WriteMessage($"\n📋 Không tìm thấy tấm khớp trong kho. Kho hiện có {allWaste.Count} tấm available.");
                        if (allWaste.Count > 0)
                        {
                            var first = allWaste.First();
                            ed.WriteMessage($"\n   VD: {first.PanelCode} | Spec={first.PanelSpec} | Dày={first.ThickMm}mm | Rộng={first.WidthMm:F0}mm | Dài={first.LengthMm:F0}mm");
                            ed.WriteMessage($"\n   Cần: Spec={layout.RemnantPanel.Spec} | Dày={layout.RemnantPanel.ThickMm}mm | Rộng≤{layout.RemnantPanel.WidthMm:F0}mm | Dài≤{layout.RemnantPanel.LengthMm:F0}mm");
                        }
                    }

                    // Nếu không dùng kho → cắt tấm mới, lưu leftover vào kho
                    if (!usedFromStock)
                    {
                        var leftover = matcher.CreateLeftover(
                            layout.RemnantPanel, request.PanelWidthMm, request.WallCode, "current");
                        if (leftover != null)
                        {
                            matcher.SaveLeftover(leftover);
                            NotifyWasteUpdated();
                            string lj = leftover.JointLeft == Models.JointType.Cut ? "0" :
                                        leftover.JointLeft == Models.JointType.Male ? "+" : "-";
                            string rj = leftover.JointRight == Models.JointType.Cut ? "0" :
                                        leftover.JointRight == Models.JointType.Male ? "+" : "-";
                            ed.WriteMessage($"\n📦 Lưu tấm lẻ {leftover.WidthMm:F0}mm [{lj}/{rj}] vào kho.");
                        }
                    }
                }
                else if (layout.RemnantPanel != null && WasteRepo == null)
                {
                    ed.WriteMessage("\n⚠️ Waste DB chưa sẵn sàng - bỏ qua bước tìm tấm lẻ.");
                }

                // === THỐNG KÊ HAO HỤT: Bậc thang (STEP) ===
                if (WasteRepo != null)
                {
                    var allPanels = layout.AllPanels
                        .Where(p => !layout.CutPanels.Contains(p))
                        .ToList();
                    int stepCount = 0;
                    foreach (var p in allPanels)
                    {
                        // Chỉ tấm tại bậc nhảy mới có waste (StepWasteWidth > 0)
                        if (p.StepWasteWidth > 50.0 && p.StepWasteHeight > 50.0)
                        {
                            stepCount++;
                            var stepWaste = new Models.WastePanel
                            {
                                PanelCode = $"{p.PanelId}-STEP",
                                WidthMm = p.StepWasteWidth,     // partial width (edge→step)
                                LengthMm = p.StepWasteHeight,   // height diff
                                ThickMm = p.ThickMm,
                                PanelSpec = p.Spec,
                                JointLeft = Models.JointType.Cut,
                                JointRight = Models.JointType.Cut,
                                SourceWall = request.WallCode,
                                Project = "current",
                                Status = "discarded",
                                SourceType = "STEP"
                            };
                            WasteRepo.AddPanel(stepWaste);
                        }
                    }
                    if (stepCount > 0)
                    {
                        ed.WriteMessage($"\n📐 Ghi nhận {stepCount} phần cắt bậc thang vào kho (Đã bỏ).");
                        NotifyWasteUpdated();
                    }
                }

                // === THỐNG KÊ HAO HỤT: Lỗ mở — chỉ tấm BIÊN bị cắt ===
                if (WasteRepo != null && layout.OpeningWasteEntries.Count > 0)
                {
                    int wasteCount = 0;
                    foreach (var (panelId, wasteW, wasteH) in layout.OpeningWasteEntries)
                    {
                        var openWaste = new Models.WastePanel
                        {
                            PanelCode = $"{panelId}-OPEN",
                            WidthMm = wasteW,
                            LengthMm = wasteH,
                            ThickMm = request.ThicknessMm,
                            PanelSpec = request.Spec,
                            JointLeft = Models.JointType.Cut,
                            JointRight = Models.JointType.Cut,
                            SourceWall = request.WallCode,
                            Project = "current",
                            Status = "discarded",
                            SourceType = "OPEN"
                        };
                        WasteRepo.AddPanel(openWaste);
                        wasteCount++;
                    }
                    ed.WriteMessage($"\n🔳 Ghi nhận {wasteCount} phần cắt lỗ mở (tấm biên) vào kho (Đã bỏ).");
                    NotifyWasteUpdated();
                }
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    BlockManager.DrawAllPanels(layout.AllPanels, tr);
                    if (openings.Count > 0)
                        BlockManager.DrawOpenings(openings, tr);
                    tr.Commit();
                    ed.WriteMessage($"\n✅ Tạo xong {layout.AllPanels.Count} tấm cho tường [{request.WallCode}].");
                    if (openings.Count > 0)
                        ed.WriteMessage($"\n🔳 Đã visualize {openings.Count} lỗ mở.");
                }

                BomManager.Refresh();

                // Auto: tìm số Wall Code trống tiếp theo (lấp khoảng trống)
                DefaultWallCode = FindNextAvailableWallCode(doc);
                WallCodeChanged?.Invoke(DefaultWallCode);
            }
            catch (System.Exception ex)
            {
                string msg = ex.InnerException?.Message ?? ex.Message;
                ed.WriteMessage($"\n❌ Lỗi SD_WALL_QUICK: {msg}");
            }
        }

        private static string SignOf(Models.JointType j) => j switch
        {
            Models.JointType.Male   => "+",
            Models.JointType.Female => "-",
            _                       => "0"
        };

        /// <summary>
        /// Scan bản vẽ, tìm tất cả Wall Code dạng "W{n}" đang dùng, rồi trả về số nhỏ nhất chưa dùng.
        /// VD: W1, W3, W5 → trả về "W2" (lấp khoảng trống)
        /// </summary>
        private static string FindNextAvailableWallCode(Document doc)
        {
            var usedNumbers = new HashSet<int>();
            try
            {
                using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
                {
                    if (tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) is BlockTable bt &&
                        tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) is BlockTableRecord ms)
                    {
                        foreach (ObjectId id in ms)
                        {
                            if (tr.GetObject(id, OpenMode.ForRead) is DBText txt && txt.Layer == "SD_TAG")
                            {
                                // Panel ID dạng "W1-01" → wall code = phần trước dấu '-' cuối
                                string text = txt.TextString;
                                if (string.IsNullOrEmpty(text) || !text.Contains('-')) continue;
                                string wallPart = text.Substring(0, text.LastIndexOf('-')); // "W1"
                                if (wallPart.StartsWith("W") && int.TryParse(wallPart.Substring(1), out int num))
                                {
                                    usedNumbers.Add(num);
                                }
                            }
                        }
                    }
                }
            }
            catch { /* Không crash */ }

            // Tìm số nhỏ nhất chưa dùng
            int next = 1;
            while (usedNumbers.Contains(next)) next++;
            return $"W{next}";
        }

        
        public void ManageSpecs()
        {
            try
            {
                var dialog = new SpecManagerDialog(SpecManager);
                Application.ShowModalWindow(dialog);
            }
            catch (System.Exception ex)
            {
                string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n❌ Lỗi mở Spec Manager: {msg}");
            }
        }

        [CommandMethod("_SD_DETAIL")]
        public void PlaceDetail()
        {
            Document? doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                var opt = new PromptEntityOptions("\nChọn Polyline biên tường để chèn Detail:");
                opt.SetRejectMessage("\nPhải là Polyline!");
                opt.AddAllowedClass(typeof(Polyline), true);
                var entRes = ed.GetEntity(opt);
                if (entRes.Status != PromptStatus.OK) return;

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (tr.GetObject(entRes.ObjectId, OpenMode.ForRead) is not Polyline pl) return;

                    // Dam bao layer SD_DETAIL ton tai truoc khi chen block
                    BlockManager.EnsureLayers(tr);

                    var placer = new DetailPlacer();
                    int count = placer.PlaceDetails(pl, CurrentDetailType, tr);
                    
                    tr.Commit();
                    ed.WriteMessage($"\n✅ Đã chèn {count} details (loại {CurrentDetailType}).");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi chèn detail: {ex.Message}");
            }
        }

        public void RefreshBom()
        {
            BomManager.Refresh();
        }

        public void ExportBom()
        {
            try
            {
                Document? doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                // Hỏi vị trí lưu file
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Chọn nơi lưu file BOM Excel",
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"ShopDrawing_BOM_{System.DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                    InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop)
                };
                if (dlg.ShowDialog() != true) return;
                
                List<BomRow> rows;
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    rows = BomManager.ScanDocumentForPanels(tr, doc.Database);
                    tr.Commit();
                }
                
                if (rows.Count == 0)
                {
                    doc.Editor.WriteMessage("\n⚠️ Không tìm thấy tấm Panel nào để xuất Excel.");
                    return;
                }

                var exporter = new ExcelExporter();
                exporter.ExportFullBom(rows, WasteRepo, dlg.FileName);
                doc.Editor.WriteMessage($"\n✅ Xuất BOM thành công ra: {dlg.FileName}");
            }
            catch (System.Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n❌ Lỗi xuất BOM: {ex.Message}");
            }
        }

        private static WasteManagerDialog? _wasteDialog;

        public void ShowWaste()
        {
            try
            {
                // Singleton: nếu đã mở → đưa lên trước
                if (_wasteDialog != null && _wasteDialog.IsLoaded)
                {
                    _wasteDialog.Activate();
                    return;
                }

                _wasteDialog = new WasteManagerDialog();
                _wasteDialog.Closed += (s, e) => _wasteDialog = null;
                Application.ShowModelessWindow(_wasteDialog);
            }
            catch (System.Exception ex)
            {
                string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n❌ Lỗi mở Waste Manager: {msg}");
            }
        }
        /// <summary>
        /// SD_UPDATE_TEXT — Cập nhật chiều cao toàn bộ text plugin đã sinh ra.
        /// Layer: SD_TAG, SD_OPENING. Dùng DefaultTextHeightMm × Cannoscale hiện tại.
        /// </summary>
        [CommandMethod("_SD_UPDATE_TEXT")]
        public void UpdateAllPluginText()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            // Tính chiều cao mới
            double scaleFactor = 100.0;
            try
            {
                var cs = db.Cannoscale;
                if (cs != null && cs.DrawingUnits > 0)
                    scaleFactor = cs.DrawingUnits / cs.PaperUnits;
            }
            catch { }

            double newHeight = DefaultTextHeightMm * scaleFactor;
            double minH = 20.0; // tối thiểu 20mm model space
            if (newHeight < minH) newHeight = minH;

            var targetLayers = new System.Collections.Generic.HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase)
            { "SD_TAG", "SD_OPENING" };

            int count = 0;
            int dimCount = 0;
            try
            {
                using var docLock = doc.LockDocument();
                using var tr = db.TransactionManager.StartTransaction();
                var ms = (BlockTableRecord)tr.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);

                // Lấy/tạo style Arial để đổi font text cũ
                var arialStyleId = Core.BlockManager.EnsureArialStyle(db, tr);
                var dimStyleId = Core.SmartDimEngine.EnsureDimStyle(db, tr);

                foreach (ObjectId id in ms)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead);
                    if (ent is DBText txt && targetLayers.Contains(txt.Layer))
                    {
                        txt.UpgradeOpen();
                        txt.Height = newHeight;
                        txt.TextStyleId = arialStyleId; // Đổi sang Arial
                        count++;
                    }
                    else if (ent is DBText dimTxt &&
                             dimTxt.GetXDataForApplication(Core.SmartDimEngine.XDATA_APP) != null)
                    {
                        dimTxt.UpgradeOpen();
                        dimTxt.Height = newHeight;
                        dimTxt.TextStyleId = arialStyleId;
                        count++;
                    }
                    else if (ent is Dimension dim &&
                             (string.Equals(dim.Layer, Core.SmartDimEngine.DIM_LAYER_PANEL, System.StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(dim.Layer, Core.SmartDimEngine.DIM_LAYER_OPENING, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        dim.UpgradeOpen();
                        dim.DimensionStyle = dimStyleId;
                        Core.SmartDimEngine.ApplyAnnotativeContextsForUpdate(dim, db);
                        Core.SmartDimEngine.NormalizeDimensionAppearanceForUpdate(dim, db);
                        dim.RecordGraphicsModified(true);
                        dimCount++;
                    }
                }
                tr.Commit();
                ed.WriteMessage($"\n✅ SD_UPDATE_TEXT: Đã cập nhật {count} text objects, {dimCount} dim objects → {DefaultTextHeightMm}mm × {scaleFactor:F0} = {newHeight:F0}mm | Font: Arial");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ SD_UPDATE_TEXT lỗi: {ex.Message}");
            }
        }

#pragma warning disable CS8618
        private static PaletteSet _tenderPaletteSet;
#pragma warning restore CS8618

        /// <summary>
        /// SD_TENDER — Mở/tắt Tender Palette.
        /// Quản lý chào giá: nhập vách, tính khối lượng, xuất Excel.
        /// </summary>
        [CommandMethod("SD_TENDER")]
        public void ToggleTenderPalette()
        {
            if (_tenderPaletteSet == null)
            {
                _tenderPaletteSet = new PaletteSet("Tender - Chào Giá");
                _tenderPaletteSet.Style = PaletteSetStyles.ShowCloseButton
                                        | PaletteSetStyles.ShowPropertiesMenu
                                        | PaletteSetStyles.ShowAutoHideButton;
                _tenderPaletteSet.MinimumSize = new System.Drawing.Size(280, 450);
                _tenderPaletteSet.AddVisual("Tender", new TenderPaletteControl());
            }
            _tenderPaletteSet.Visible = !_tenderPaletteSet.Visible;
        }
    }
}
