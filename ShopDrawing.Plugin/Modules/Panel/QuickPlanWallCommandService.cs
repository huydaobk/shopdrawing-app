using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.Runtime;
using ShopDrawing.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ShopDrawing.Plugin.Modules.Panel
{
    internal sealed class QuickPlanWallCommandService
    {
        public void Run(
            PanelLayoutScope scope,
            ShopDrawingRuntimeSettings settings,
            LayoutEngine layoutEngine,
            WasteRepository? wasteRepo,
            BlockManager blockManager,
            BomManager bomManager)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            Editor ed = doc.Editor;

            try
            {
                string panelCode = ResolvePanelCode(scope, settings, doc);
                var request = BuildRequest(settings, panelCode, scope);

                ed.WriteMessage(
                    $"\n[{GetCommandName(scope)}] {GetScopeLabel(scope)}: {request.WallCode} | {request.PanelWidthMm:F0}mm | {request.ThicknessMm}mm | {request.Direction}");

                var boundaryId = PromptPlanInputs(doc, ed, settings, out var openings);
                if (boundaryId == ObjectId.Null)
                {
                    return;
                }

                var layout = CalculateLayout(doc, ed, layoutEngine, request, boundaryId, openings);
                if (layout == null)
                {
                    return;
                }

                ClearGeneratedWaste(ed, wasteRepo, request.WallCode);
                ProcessWasteMatching(ed, settings, wasteRepo, request, layout);
                RecordStepWaste(ed, settings, wasteRepo, request, layout);
                RecordOpeningWaste(ed, settings, wasteRepo, request, layout);
                DrawLayout(doc, ed, blockManager, layout, openings, request, scope, settings, boundaryId);

                bomManager.Refresh();
                if (scope == PanelLayoutScope.Wall)
                {
                    settings.SetDefaultWallCode(FindNextAvailableCode(doc, "W"));
                }
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException?.Message ?? ex.Message;
                ed.WriteMessage($"\nLỗi {GetCommandName(scope)}: {msg}");
            }
        }

        private static LayoutRequest BuildRequest(
            ShopDrawingRuntimeSettings settings,
            string panelCode,
            PanelLayoutScope scope)
        {
            return new LayoutRequest
            {
                WallCode = panelCode,
                ThicknessMm = settings.DefaultThickness,
                PanelWidthMm = settings.DefaultPanelWidth,
                Spec = settings.DefaultSpec,
                JointGapMm = settings.DefaultJointGap,
                Direction = settings.DefaultDirection,
                StartEdge = settings.DefaultStartEdge,
                Application = settings.DefaultApplication,
                TopPanelTreatment = settings.DefaultWallTopPanelTreatment,
                StartPanelTreatment = settings.DefaultWallStartPanelTreatment,
                EndPanelTreatment = settings.DefaultWallEndPanelTreatment,
                BottomEdgeEnabled = settings.DefaultWallBottomEdgeEnabled,
                IsCeilingLayout = scope == PanelLayoutScope.Ceiling,
                CeilingSuspensionDirection = settings.DefaultCeilingSuspensionDirection,
                CeilingDivideFromMaxSide = settings.DefaultCeilingDivideFromMaxSide,
                CeilingTSpacingMm = settings.DefaultCeilingTSpacingMm,
                CeilingTClearGapMm = settings.DefaultCeilingTClearGapMm,
                CeilingMushroomDivisionCount = settings.DefaultCeilingMushroomDivisionCount,
                CeilingBaySpansMm = settings.DefaultCeilingBaySpansMm.ToList(),
                CeilingBayHasMushroomFlags = settings.DefaultCeilingBayHasMushroomFlags.ToList()
            };
        }

        private static ObjectId PromptPlanInputs(Document doc, Editor ed, ShopDrawingRuntimeSettings settings, out List<Opening> openings)
        {
            openings = new List<Opening>();

            var ppo1 = new PromptPointOptions("\nChọn điểm bắt đầu dải tường trên mặt bằng: ");
            var pt1Res = ed.GetPoint(ppo1);
            if (pt1Res.Status != PromptStatus.OK) return ObjectId.Null;
            var ptStart = pt1Res.Value;
            var currentPt = ptStart;

            var segments = new List<(Autodesk.AutoCAD.Geometry.Point3d Start, Autodesk.AutoCAD.Geometry.Point3d End, double Length, double Height)>();
            double currentHeight = settings.DefaultWallHeight;
            int segmentIndex = 1;

            while (true)
            {
                var ppo2 = new PromptPointOptions($"\nĐoạn {segmentIndex}: Chọn điểm tiếp theo (Enter để kết thúc dải tuyến): ")
                {
                    UseBasePoint = true,
                    BasePoint = currentPt,
                    UseDashedLine = true,
                    AllowNone = true
                };
                var pt2Res = ed.GetPoint(ppo2);
                if (pt2Res.Status == PromptStatus.None || pt2Res.Status == PromptStatus.Cancel)
                {
                    break;
                }

                if (pt2Res.Status == PromptStatus.OK)
                {
                    var ptEnd = pt2Res.Value;
                    double segLen = currentPt.DistanceTo(ptEnd);
                    if (segLen <= 0)
                    {
                        ed.WriteMessage("\nChiều dài đoạn tường phải > 0! Thử lại.");
                        continue;
                    }

                    var pdo = new PromptDoubleOptions($"\nĐoạn {segmentIndex} dài {segLen:F0}: Nhập chiều cao thiết kế (H): ")
                    {
                        DefaultValue = currentHeight,
                        UseDefaultValue = true,
                        AllowZero = false,
                        AllowNegative = false
                    };
                    var heightResult = ed.GetDouble(pdo);
                    if (heightResult.Status == PromptStatus.Cancel) break;

                    if (heightResult.Status == PromptStatus.OK)
                    {
                        currentHeight = heightResult.Value;
                        if (segmentIndex == 1)
                        {
                            settings.DefaultWallHeight = currentHeight;
                        }
                    }

                    segments.Add((currentPt, ptEnd, segLen, currentHeight));
                    currentPt = ptEnd;
                    segmentIndex++;
                }
            }

            if (segments.Count == 0)
            {
                return ObjectId.Null;
            }

            double totalLength = segments.Sum(s => s.Length);

            var planPolyline = new Polyline();
            planPolyline.AddVertexAt(0, new Autodesk.AutoCAD.Geometry.Point2d(segments[0].Start.X, segments[0].Start.Y), 0, 0, 0);
            for (int i = 0; i < segments.Count; i++)
            {
                planPolyline.AddVertexAt(i + 1, new Autodesk.AutoCAD.Geometry.Point2d(segments[i].End.X, segments[i].End.Y), 0, 0, 0);
            }

            var tempOpenings = new List<(double st, double w, double sill, double oh, string type)>();
            
            int openIndex = 1;
            while (true)
            {
                var ppoOp1 = new PromptPointOptions($"\nLỗ mở #{openIndex}: Chọn điểm thứ nhất lọt lòng lỗ mở (Enter/Chuột phải để bỏ qua/kết thúc): ")
                {
                    AllowNone = true
                };
                var ppoOp1Res = ed.GetPoint(ppoOp1);
                
                if (ppoOp1Res.Status == PromptStatus.None || ppoOp1Res.Status == PromptStatus.Cancel) break;
                
                if (ppoOp1Res.Status == PromptStatus.OK)
                {
                    var ptOp1 = ppoOp1Res.Value;
                    
                    var ppoOp2 = new PromptPointOptions($"\nLỗ mở #{openIndex}: Chọn điểm thứ hai lọt lòng lỗ mở: ")
                    {
                        UseBasePoint = true,
                        BasePoint = ptOp1,
                        UseDashedLine = true,
                        AllowNone = true
                    };
                    var ppoOp2Res = ed.GetPoint(ppoOp2);
                    
                    if (ppoOp2Res.Status == PromptStatus.None || ppoOp2Res.Status == PromptStatus.Cancel) break;
                    
                    if (ppoOp2Res.Status == PromptStatus.OK)
                    {
                        var ptOp2 = ppoOp2Res.Value;

                        using var curve = planPolyline.Clone() as Curve;
                        if (curve != null)
                        {
                            var closest1 = curve.GetClosestPointTo(ptOp1, false);
                            var closest2 = curve.GetClosestPointTo(ptOp2, false);
                            
                            double dist1 = curve.GetDistAtPoint(closest1);
                            double dist2 = curve.GetDistAtPoint(closest2);

                            double startOffset = Math.Min(dist1, dist2);
                            double endOffset = Math.Max(dist1, dist2);
                            double openW = endOffset - startOffset;

                            if (openW > 10)
                            {
                                string openingType = "Cửa sổ/LKT";
                                double sill = 0;
                                double openH = 2200;
                                
                                var pkoType = new PromptKeywordOptions($"\nLỗ mở #{openIndex} (Rộng {openW:F0}): Chọn loại lỗ mở [cửa Đi(D)/cửa Sổ(S)] <S>: ")
                                {
                                    AllowNone = true
                                };
                                pkoType.Keywords.Add("D");
                                pkoType.Keywords.Add("S");
                                pkoType.Keywords.Default = "S";
                                
                                var pkoRes = ed.GetKeywords(pkoType);
                                if (pkoRes.Status == PromptStatus.Cancel) break;
                                
                                if (pkoRes.StringResult == "D")
                                {
                                    openingType = "Cửa đi";
                                }
                                
                                if (openingType == "Cửa sổ/LKT")
                                {
                                    var pdoSill = new PromptDoubleOptions($"\nLỗ mở #{openIndex} (Rộng {openW:F0}). Nhập khoảng cách chân sàn (Sill): ") 
                                    { DefaultValue = 0, UseDefaultValue = true };
                                    var rSill = ed.GetDouble(pdoSill);
                                    if (rSill.Status == PromptStatus.Cancel) break;
                                    if (rSill.Status == PromptStatus.OK) sill = rSill.Value;
                                }
                                
                                var pdoHeight = new PromptDoubleOptions($"\nLỗ mở #{openIndex} (Rộng {openW:F0}). Nhập chiều cao lỗ mở H: ") 
                                { DefaultValue = 2200, UseDefaultValue = true };
                                var rHeight = ed.GetDouble(pdoHeight);
                                if (rHeight.Status == PromptStatus.Cancel) break;
                                if (rHeight.Status == PromptStatus.OK) openH = rHeight.Value;

                                tempOpenings.Add((startOffset, openW, sill, openH, openingType));
                                openIndex++;
                            }
                            else
                            {
                                ed.WriteMessage("\nKích thước lỗ mở quá bé (< 10), vui lòng chọn lại!");
                            }
                        }
                    }
                }
            }
            
            planPolyline.Dispose();

            var ppoIns = new PromptPointOptions("\nChọn vị trí click gốc trục tọa độ để Drop xuất Shopdrawing mặt đứng tường:");
            var insRes = ed.GetPoint(ppoIns);
            if (insRes.Status != PromptStatus.OK) return ObjectId.Null;
            var insertPt = insRes.Value;
            
            foreach(var op in tempOpenings)
            {
                openings.Add(new Opening
                {
                    X = insertPt.X + op.st,
                    Y = insertPt.Y + op.sill,
                    Width = op.w,
                    Height = op.oh,
                    OpeningType = op.type
                });
            }

            ObjectId boundaryId = ObjectId.Null;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead)!;
                var ms = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite)!;

                var polyline = new Polyline();
                polyline.AddVertexAt(0, new Autodesk.AutoCAD.Geometry.Point2d(insertPt.X, insertPt.Y), 0, 0, 0);
                polyline.AddVertexAt(1, new Autodesk.AutoCAD.Geometry.Point2d(insertPt.X + totalLength, insertPt.Y), 0, 0, 0);

                int vertexIdx = 2;
                double currentRunningLength = totalLength;
                
                for (int i = segments.Count - 1; i >= 0; i--)
                {
                    var seg = segments[i];
                    polyline.AddVertexAt(vertexIdx++, new Autodesk.AutoCAD.Geometry.Point2d(insertPt.X + currentRunningLength, insertPt.Y + seg.Height), 0, 0, 0);
                    currentRunningLength -= seg.Length;
                    polyline.AddVertexAt(vertexIdx++, new Autodesk.AutoCAD.Geometry.Point2d(insertPt.X + currentRunningLength, insertPt.Y + seg.Height), 0, 0, 0);
                }

                polyline.Closed = true;

                boundaryId = ms.AppendEntity(polyline);
                tr.AddNewlyCreatedDBObject(polyline, true);
                
                tr.Commit();
            }

            return boundaryId;
        }

        private static LayoutResult? CalculateLayout(
            Document doc,
            Editor ed,
            LayoutEngine layoutEngine,
            LayoutRequest request,
            ObjectId boundaryId,
            List<Opening> openings)
        {
            using var tr = doc.Database.TransactionManager.StartTransaction();
            if (tr.GetObject(boundaryId, OpenMode.ForRead) is not Polyline boundaryPolyline)
            {
                ed.WriteMessage("\nLỗi: không phải Polyline.");
                return null;
            }

            request.BoundaryPolyline = boundaryPolyline;
            request.Openings = openings;

            var layout = layoutEngine.Calculate(request);
            tr.Commit();
            return layout;
        }

        private static void ProcessWasteMatching(
            Editor ed,
            ShopDrawingRuntimeSettings settings,
            WasteRepository? wasteRepo,
            LayoutRequest request,
            LayoutResult layout)
        {
            var remnantPanel = layout.RemnantPanel ?? FindRemnantCandidate(layout, request.PanelWidthMm);
            if (remnantPanel == null)
            {
                return;
            }

            if (wasteRepo == null)
            {
                ed.WriteMessage("\nWaste DB chưa sẵn sàng, bỏ qua bước tìm tấm lẻ.");
                return;
            }

            ed.WriteMessage(
                $"\nTấm lẻ cần: Spec={remnantPanel.Spec} | Dày={remnantPanel.ThickMm}mm | Rộng={remnantPanel.WidthMm:F0}mm | Dài={remnantPanel.LengthMm:F0}mm");

            var matcher = new WasteMatcher(wasteRepo);
            var matchResult = matcher.FindBestMatchWithDirection(remnantPanel);
            bool usedFromStock = false;

            if (matchResult.Panel != null)
            {
                ed.WriteMessage($"\nTìm thấy tấm khớp: {matchResult.Panel.PanelCode} | Hướng: {matchResult.Direction}");

                string remnantJoints = $"{SignOf(remnantPanel.JointLeft)}/{SignOf(remnantPanel.JointRight)}";
                string foundJoints = $"{SignOf(matchResult.Panel.JointLeft)}/{SignOf(matchResult.Panel.JointRight)}";
                ed.WriteMessage($"\n   Remnant cần: [{remnantJoints}] | Kho có: [{foundJoints}] | Direction: {matchResult.Direction}");

                var suggestDialog = new WasteSuggestionDialog(remnantPanel, matchResult.Panel, matchResult.Direction);
                if (Application.ShowModalWindow(suggestDialog) == true && suggestDialog.UseFromStock)
                {
                    usedFromStock = true;
                    remnantPanel.IsReused = true;
                    remnantPanel.SourceId = matchResult.Panel.PanelCode;

                    ApplyMatchedJoints(remnantPanel, matchResult);
                    FlipPanelsIfNeeded(ed, layout, matchResult.Direction);

                    ed.WriteMessage($"\nTận dụng tấm kho {matchResult.Panel.PanelCode} ({matchResult.Panel.WidthMm:F0}mm).");

                    var reuseLeftover = matcher.CreateReuseLeftover(matchResult.Panel, remnantPanel, matchResult.Direction);
                    if (reuseLeftover != null)
                    {
                        matcher.SaveReuseLeftover(reuseLeftover);
                        settings.NotifyWasteUpdated();
                        ed.WriteMessage(
                            $"\nCập nhật phần còn lại {reuseLeftover.WidthMm:F0}mm [{SignOf(reuseLeftover.JointLeft)}/{SignOf(reuseLeftover.JointRight)}] vào kho.");
                    }
                    else
                    {
                        matcher.AcceptReuse(matchResult.Panel.Id);
                        settings.NotifyWasteUpdated();
                        ed.WriteMessage("\nTấm kho đã dùng hết, chuyển trạng thái sang Đã dùng.");
                    }
                }
            }
            else
            {
                LogAvailableWasteDebug(ed, wasteRepo, remnantPanel);
            }

            if (usedFromStock)
            {
                return;
            }

            var leftover = matcher.CreateLeftover(remnantPanel, request.PanelWidthMm, request.WallCode, "current");
            if (leftover == null)
            {
                return;
            }

            matcher.SaveLeftover(leftover);
            settings.NotifyWasteUpdated();

            string leftJoint = SignOf(leftover.JointLeft);
            string rightJoint = SignOf(leftover.JointRight);
            ed.WriteMessage($"\nLưu tấm lẻ {leftover.WidthMm:F0}mm [{leftJoint}/{rightJoint}] vào kho.");
        }

        private static void ClearGeneratedWaste(
            Editor ed,
            WasteRepository? wasteRepo,
            string wallCode)
        {
            if (wasteRepo == null || string.IsNullOrWhiteSpace(wallCode))
            {
                return;
            }

            int deleted = wasteRepo.DeleteGeneratedBySourceWall(wallCode);
            if (deleted > 0)
            {
                ed.WriteMessage($"\nLam sach {deleted} muc kho le cu cua {wallCode} truoc khi cap nhat lai.");
            }
        }

        private static ShopDrawing.Plugin.Models.Panel? FindRemnantCandidate(
            LayoutResult layout,
            double fullPanelWidth)
        {
            const double tolerance = 1.0;
            return layout.AllPanels.FirstOrDefault(panel =>
                !panel.IsReused &&
                string.IsNullOrWhiteSpace(panel.SourceId) &&
                string.IsNullOrWhiteSpace(panel.ParentPanelId) &&
                panel.WidthMm > tolerance &&
                panel.WidthMm < fullPanelWidth - tolerance &&
                ((panel.JointLeft == JointType.Cut) ^ (panel.JointRight == JointType.Cut)));
        }

        private static void ApplyMatchedJoints(ShopDrawing.Plugin.Models.Panel remnantPanel, WasteMatchResult matchResult)
        {
            if (matchResult.Panel == null)
            {
                return;
            }

            if (matchResult.Direction == MatchDirection.Flipped)
            {
                remnantPanel.JointLeft = matchResult.Panel.JointRight;
                remnantPanel.JointRight = matchResult.Panel.JointLeft;
                return;
            }

            remnantPanel.JointLeft = matchResult.Panel.JointLeft;
            remnantPanel.JointRight = matchResult.Panel.JointRight;
        }

        private static void FlipPanelsIfNeeded(Editor ed, LayoutResult layout, MatchDirection direction)
        {
            if (direction != MatchDirection.Flipped)
            {
                return;
            }

            int flippedCount = 0;
            foreach (var panel in layout.FullPanels.Concat(layout.CutPanels))
            {
                (panel.JointLeft, panel.JointRight) = (panel.JointRight, panel.JointLeft);
                flippedCount++;
            }

            ed.WriteMessage($"\nĐã đổi chiều ngàm {flippedCount} tấm.");
        }

        private static void LogAvailableWasteDebug(
            Editor ed,
            WasteRepository wasteRepo,
            ShopDrawing.Plugin.Models.Panel remnantPanel)
        {
            var availableWaste = wasteRepo.GetAll().Where(w => w.Status == "available").ToList();
            ed.WriteMessage($"\nKhông tìm thấy tấm khớp trong kho. Kho hiện có {availableWaste.Count} tấm available.");

            if (availableWaste.Count == 0)
            {
                return;
            }

            var first = availableWaste.First();
            ed.WriteMessage(
                $"\n   VD: {first.PanelCode} | Spec={first.PanelSpec} | Dày={first.ThickMm}mm | Rộng={first.WidthMm:F0}mm | Dài={first.LengthMm:F0}mm");
            ed.WriteMessage(
                $"\n   Cần: Spec={remnantPanel.Spec} | Dày={remnantPanel.ThickMm}mm | Rộng<={remnantPanel.WidthMm:F0}mm | Dài<={remnantPanel.LengthMm:F0}mm");
        }

        private static void RecordStepWaste(
            Editor ed,
            ShopDrawingRuntimeSettings settings,
            WasteRepository? wasteRepo,
            LayoutRequest request,
            LayoutResult layout)
        {
            if (wasteRepo == null)
            {
                return;
            }

            var allPanels = layout.AllPanels.Where(p => !layout.CutPanels.Contains(p)).ToList();
            int stepCount = 0;

            foreach (var panel in allPanels)
            {
                if (panel.StepWasteWidth <= 50.0 || panel.StepWasteHeight <= 50.0)
                {
                    continue;
                }

                stepCount++;
                var stepWaste = new WastePanel
                {
                    PanelCode = $"{panel.PanelId}-STEP",
                    WidthMm = panel.StepWasteWidth,
                    LengthMm = panel.StepWasteHeight,
                    ThickMm = panel.ThickMm,
                    PanelSpec = panel.Spec,
                    JointLeft = JointType.Cut,
                    JointRight = JointType.Cut,
                    SourceWall = request.WallCode,
                    Project = "current",
                    Status = "available",
                    SourceType = "STEP",
                    SourcePanelX = panel.X,
                    SourcePanelY = panel.Y
                };
                wasteRepo.AddPanel(stepWaste);
            }

            if (stepCount > 0)
            {
                ed.WriteMessage($"\nGhi nhận {stepCount} phần cắt bậc thang vào kho.");
                settings.NotifyWasteUpdated();
            }
        }

        private static void RecordOpeningWaste(
            Editor ed,
            ShopDrawingRuntimeSettings settings,
            WasteRepository? wasteRepo,
            LayoutRequest request,
            LayoutResult layout)
        {
            if (wasteRepo == null || request.Openings.Count == 0)
            {
                return;
            }

            var openingWasteEntries = BuildOpeningWasteEntries(layout.AllPanels, request.Openings);
            if (openingWasteEntries.Count == 0)
            {
                return;
            }

            int wasteCount = 0;
            foreach (var (panelId, wasteWidth, wasteHeight, panelX, panelY) in openingWasteEntries)
            {
                var openWaste = new WastePanel
                {
                    PanelCode = $"{panelId}-OPEN",
                    WidthMm = wasteWidth,
                    LengthMm = wasteHeight,
                    ThickMm = request.ThicknessMm,
                    PanelSpec = request.Spec,
                    JointLeft = JointType.Cut,
                    JointRight = JointType.Cut,
                    SourceWall = request.WallCode,
                    Project = "current",
                    Status = "discarded",
                    SourceType = "OPEN",
                    SourcePanelX = panelX,
                    SourcePanelY = panelY
                };
                wasteRepo.AddPanel(openWaste);
                wasteCount++;
            }

            ed.WriteMessage($"\nGhi nhận {wasteCount} phần cắt vùng mở vào kho.");
            settings.NotifyWasteUpdated();
        }

        private static List<(string PanelId, double WidthMm, double HeightMm, double PanelX, double PanelY)> BuildOpeningWasteEntries(
            IReadOnlyList<ShopDrawing.Plugin.Models.Panel> panels,
            IReadOnlyList<Opening> openings)
        {
            var result = new List<(string PanelId, double WidthMm, double HeightMm, double PanelX, double PanelY)>();
            foreach (var panel in panels)
            {
                foreach (var opening in openings)
                {
                    if (!TryGetOpeningOverlap(panel, opening, out double wasteWidth, out double wasteHeight))
                    {
                        continue;
                    }

                    string panelId = string.IsNullOrWhiteSpace(panel.PanelId) ? "OPEN" : panel.PanelId;
                    result.Add((panelId, wasteWidth, wasteHeight, panel.X, panel.Y));
                }
            }

            return result;
        }

        private static bool TryGetOpeningOverlap(
            ShopDrawing.Plugin.Models.Panel panel,
            Opening opening,
            out double wasteWidth,
            out double wasteHeight)
        {
            double drawWidth = panel.IsHorizontal ? panel.LengthMm : panel.WidthMm;
            double drawHeight = panel.IsHorizontal ? panel.WidthMm : panel.LengthMm;

            double overlapLeft = Math.Max(panel.X, opening.X);
            double overlapRight = Math.Min(panel.X + drawWidth, opening.X + opening.Width);
            double overlapBottom = Math.Max(panel.Y, opening.Y);
            double overlapTop = Math.Min(panel.Y + drawHeight, opening.Y + opening.Height);

            double overlapWidth = Math.Max(0, overlapRight - overlapLeft);
            double overlapHeight = Math.Max(0, overlapTop - overlapBottom);
            if (overlapWidth <= 10.0 || overlapHeight <= 10.0)
            {
                wasteWidth = 0;
                wasteHeight = 0;
                return false;
            }

            wasteWidth = Math.Round(panel.IsHorizontal ? overlapHeight : overlapWidth, 0);
            wasteHeight = Math.Round(panel.IsHorizontal ? overlapWidth : overlapHeight, 0);
            return true;
        }

        private static void DrawLayout(
            Document doc,
            Editor ed,
            BlockManager blockManager,
            LayoutResult layout,
            List<Opening> openings,
            LayoutRequest request,
            PanelLayoutScope scope,
            ShopDrawingRuntimeSettings settings,
            ObjectId boundaryId)
        {
            using var tr = doc.Database.TransactionManager.StartTransaction();
            blockManager.DrawAllPanels(layout.AllPanels, tr);
            if (scope == PanelLayoutScope.Ceiling
                && tr.GetObject(boundaryId, OpenMode.ForRead) is Polyline boundaryPolyline)
            {
                blockManager.DrawCeilingHardware(
                    layout.AllPanels,
                    boundaryPolyline,
                    request.CeilingSuspensionDirection,
                    request.CeilingDivideFromMaxSide,
                    request.CeilingTSpacingMm,
                    request.CeilingBaySpansMm,
                    request.CeilingBayHasMushroomFlags,
                    request.CeilingMushroomDivisionCount,
                    tr);
            }

            if (openings.Count > 0)
            {
                blockManager.DrawOpenings(openings, tr);
            }

            tr.Commit();
            ed.WriteMessage($"\nTạo xong {layout.AllPanels.Count} tấm cho {GetScopeLabel(scope).ToLowerInvariant()} [{request.WallCode}].");
            if (openings.Count > 0)
            {
                ed.WriteMessage($"\nĐã vẽ {openings.Count} vùng cắt.");
            }
        }

        private static string ResolvePanelCode(PanelLayoutScope scope, ShopDrawingRuntimeSettings settings, Document doc)
        {
            if (scope == PanelLayoutScope.Wall && !string.IsNullOrWhiteSpace(settings.DefaultWallCode))
            {
                return settings.DefaultWallCode;
            }

            string prefix = scope == PanelLayoutScope.Ceiling ? "C" : "W";
            return FindNextAvailableCode(doc, prefix);
        }

        private static string FindNextAvailableCode(Document doc, string prefix)
        {
            var usedNumbers = new HashSet<int>();

            try
            {
                using var tr = doc.Database.TransactionManager.StartOpenCloseTransaction();
                if (tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) is not BlockTable blockTable ||
                    tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead) is not BlockTableRecord modelSpace)
                {
                    return $"{prefix}1";
                }

                foreach (ObjectId id in modelSpace)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is not DBText textEntity || textEntity.Layer != "SD_TAG")
                    {
                        continue;
                    }

                    string text = textEntity.TextString;
                    if (string.IsNullOrEmpty(text) || !text.Contains('-'))
                    {
                        continue;
                    }

                    string panelPart = text.Substring(0, text.LastIndexOf('-'));
                    if (panelPart.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(panelPart.Substring(1), out int num))
                    {
                        usedNumbers.Add(num);
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
                return $"{prefix}1";
            }

            int next = 1;
            while (usedNumbers.Contains(next))
            {
                next++;
            }

            return $"{prefix}{next}";
        }

        private static string SignOf(JointType jointType)
        {
            return jointType switch
            {
                JointType.Male => "+",
                JointType.Female => "-",
                _ => "0"
            };
        }

        private static string GetCommandName(PanelLayoutScope scope)
            => "SD_WALL_PLAN_QUICK";

        private static string GetScopeLabel(PanelLayoutScope scope)
            => scope == PanelLayoutScope.Ceiling ? "Trần" : "Tường";

        private static string GetBoundaryLabel(PanelLayoutScope scope)
            => scope == PanelLayoutScope.Ceiling ? "trần" : "tường";

        private static string GetOpeningLabel(PanelLayoutScope scope)
            => scope == PanelLayoutScope.Ceiling ? "lỗ mở / ô trống trần" : "lỗ mở";
    }
}
