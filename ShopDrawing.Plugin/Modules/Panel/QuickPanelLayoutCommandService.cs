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
    internal sealed class QuickPanelLayoutCommandService
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

                var boundaryId = PromptBoundaryPolyline(ed, scope);
                if (boundaryId == ObjectId.Null)
                {
                    return;
                }

                if (!ApplyScopeConfiguration(scope, doc, settings, boundaryId, request))
                {
                    return;
                }

                var openings = settings.EnableOpeningCut
                    ? PromptOpenings(doc, ed, scope, settings)
                    : new List<Opening>();

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
                ed.WriteMessage($"\nLá»—i {GetCommandName(scope)}: {msg}");
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

        private static bool ApplyScopeConfiguration(
            PanelLayoutScope scope,
            Document doc,
            ShopDrawingRuntimeSettings settings,
            ObjectId boundaryId,
            LayoutRequest request)
        {
            if (scope != PanelLayoutScope.Ceiling)
            {
                return true;
            }

            Polyline boundarySnapshot;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                if (tr.GetObject(boundaryId, OpenMode.ForRead) is not Polyline boundary)
                {
                    return false;
                }

                boundarySnapshot = (Polyline)boundary.Clone();
                tr.Commit();
            }

            try
            {
                var dialog = new ShopdrawingCeilingOptionsDialog(
                    boundarySnapshot,
                    settings.DefaultThickness,
                    settings.DefaultDirection,
                    settings.DefaultStartEdge,
                    settings.DefaultCeilingSuspensionDirection,
                    settings.DefaultCeilingDivideFromMaxSide,
                    settings.DefaultCeilingTSpacingMm,
                    settings.DefaultCeilingTClearGapMm,
                    settings.DefaultCeilingMushroomDivisionCount,
                    settings.DefaultCeilingBaySpansMm,
                    settings.DefaultCeilingBayHasMushroomFlags);

                bool? accepted = Application.ShowModalWindow(dialog);
                if (accepted != true || dialog.Result == null)
                {
                    return false;
                }

                request.Direction = dialog.Result.PanelDirection;
                request.StartEdge = dialog.Result.PanelStartEdge;
                request.IsCeilingLayout = true;
                request.CeilingSuspensionDirection = dialog.Result.SuspensionDirection;
                request.CeilingDivideFromMaxSide = dialog.Result.DivideFromMaxSide;
                request.CeilingTSpacingMm = dialog.Result.TSpacingMm;
                request.CeilingTClearGapMm = dialog.Result.TClearGapMm;
                request.CeilingMushroomDivisionCount = dialog.Result.MushroomDivisionCount;
                request.CeilingBaySpansMm = dialog.Result.BaySpansMm.ToList();
                request.CeilingBayHasMushroomFlags = dialog.Result.BayHasMushroomFlags.ToList();

                settings.DefaultDirection = dialog.Result.PanelDirection;
                settings.DefaultStartEdge = dialog.Result.PanelStartEdge;
                settings.DefaultCeilingSuspensionDirection = dialog.Result.SuspensionDirection;
                settings.DefaultCeilingDivideFromMaxSide = dialog.Result.DivideFromMaxSide;
                settings.DefaultCeilingTSpacingMm = dialog.Result.TSpacingMm;
                settings.DefaultCeilingTClearGapMm = dialog.Result.TClearGapMm;
                settings.DefaultCeilingMushroomDivisionCount = dialog.Result.MushroomDivisionCount;
                settings.DefaultCeilingBaySpansMm = dialog.Result.BaySpansMm.ToList();
                settings.DefaultCeilingBayHasMushroomFlags = dialog.Result.BayHasMushroomFlags.ToList();
                return true;
            }
            finally
            {
                boundarySnapshot.Dispose();
            }
        }

        private static ObjectId PromptBoundaryPolyline(Editor ed, PanelLayoutScope scope)
        {
            var options = new PromptEntityOptions($"\nClick Polyline biÃªn {GetBoundaryLabel(scope)}:");
            options.SetRejectMessage("\nPháº£i lÃ  Polyline!");
            options.AddAllowedClass(typeof(Polyline), true);

            var result = ed.GetEntity(options);
            return result.Status == PromptStatus.OK ? result.ObjectId : ObjectId.Null;
        }

        private static List<Opening> PromptOpenings(Document doc, Editor ed, PanelLayoutScope scope, ShopDrawingRuntimeSettings settings)
        {
            var openings = new List<Opening>();
            ed.WriteMessage($"\nClick cÃ¡c Polyline {GetOpeningLabel(scope)} (Enter Ä‘á»ƒ bá» qua):");

            var selectionOptions = new PromptSelectionOptions
            {
                MessageForAdding = "\n   Chá»n vÃ¹ng cáº¯t:",
                MessageForRemoval = string.Empty
            };

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") });
            var selection = ed.GetSelection(selectionOptions, filter);
            if (selection.Status != PromptStatus.OK)
            {
                return openings;
            }

            using var tr = doc.Database.TransactionManager.StartTransaction();
            foreach (ObjectId id in selection.Value.GetObjectIds())
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Polyline polyline)
                {
                    continue;
                }

                var ext = polyline.GeometricExtents;
                openings.Add(new Opening
                {
                    X = ext.MinPoint.X,
                    Y = ext.MinPoint.Y,
                    Width = ext.MaxPoint.X - ext.MinPoint.X,
                    Height = ext.MaxPoint.Y - ext.MinPoint.Y,
                    OpeningType = scope == PanelLayoutScope.Wall ? settings.DefaultOpeningType : "Cá»­a sá»•/LKT"
                });
            }

            tr.Commit();
            return openings;
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
                ed.WriteMessage("\nLá»—i: khÃ´ng pháº£i Polyline.");
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
                ed.WriteMessage("\nWaste DB chÆ°a sáºµn sÃ ng, bá» qua bÆ°á»›c tÃ¬m táº¥m láº».");
                return;
            }

            ed.WriteMessage(
                $"\nTáº¥m láº» cáº§n: Spec={remnantPanel.Spec} | DÃ y={remnantPanel.ThickMm}mm | Rá»™ng={remnantPanel.WidthMm:F0}mm | DÃ i={remnantPanel.LengthMm:F0}mm");

            var matcher = new WasteMatcher(wasteRepo);
            var matchResult = matcher.FindBestMatchWithDirection(remnantPanel);
            bool usedFromStock = false;

            if (matchResult.Panel != null)
            {
                ed.WriteMessage($"\nTÃ¬m tháº¥y táº¥m khá»›p: {matchResult.Panel.PanelCode} | HÆ°á»›ng: {matchResult.Direction}");

                string remnantJoints = $"{SignOf(remnantPanel.JointLeft)}/{SignOf(remnantPanel.JointRight)}";
                string foundJoints = $"{SignOf(matchResult.Panel.JointLeft)}/{SignOf(matchResult.Panel.JointRight)}";
                ed.WriteMessage($"\n   Remnant cáº§n: [{remnantJoints}] | Kho cÃ³: [{foundJoints}] | Direction: {matchResult.Direction}");

                var suggestDialog = new WasteSuggestionDialog(remnantPanel, matchResult.Panel, matchResult.Direction);
                if (Application.ShowModalWindow(suggestDialog) == true && suggestDialog.UseFromStock)
                {
                    usedFromStock = true;
                    remnantPanel.IsReused = true;
                    remnantPanel.SourceId = matchResult.Panel.PanelCode;

                    ApplyMatchedJoints(remnantPanel, matchResult);
                    FlipPanelsIfNeeded(ed, layout, matchResult.Direction);

                    ed.WriteMessage($"\nTáº­n dá»¥ng táº¥m kho {matchResult.Panel.PanelCode} ({matchResult.Panel.WidthMm:F0}mm).");

                    var reuseLeftover = matcher.CreateReuseLeftover(matchResult.Panel, remnantPanel, matchResult.Direction);
                    if (reuseLeftover != null)
                    {
                        matcher.SaveReuseLeftover(reuseLeftover);
                        settings.NotifyWasteUpdated();
                        ed.WriteMessage(
                            $"\nCáº­p nháº­t pháº§n cÃ²n láº¡i {reuseLeftover.WidthMm:F0}mm [{SignOf(reuseLeftover.JointLeft)}/{SignOf(reuseLeftover.JointRight)}] vÃ o kho.");
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
            ed.WriteMessage($"\nLÆ°u táº¥m láº» {leftover.WidthMm:F0}mm [{leftJoint}/{rightJoint}] vÃ o kho.");
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

            ed.WriteMessage($"\nÄÃ£ Ä‘á»•i chiá»u ngÃ m {flippedCount} táº¥m.");
        }

        private static void LogAvailableWasteDebug(
            Editor ed,
            WasteRepository wasteRepo,
            ShopDrawing.Plugin.Models.Panel remnantPanel)
        {
            var availableWaste = wasteRepo.GetAll().Where(w => w.Status == "available").ToList();
            ed.WriteMessage($"\nKhÃ´ng tÃ¬m tháº¥y táº¥m khá»›p trong kho. Kho hiá»‡n cÃ³ {availableWaste.Count} táº¥m available.");

            if (availableWaste.Count == 0)
            {
                return;
            }

            var first = availableWaste.First();
            ed.WriteMessage(
                $"\n   VD: {first.PanelCode} | Spec={first.PanelSpec} | DÃ y={first.ThickMm}mm | Rá»™ng={first.WidthMm:F0}mm | DÃ i={first.LengthMm:F0}mm");
            ed.WriteMessage(
                $"\n   Cáº§n: Spec={remnantPanel.Spec} | DÃ y={remnantPanel.ThickMm}mm | Rá»™ng<={remnantPanel.WidthMm:F0}mm | DÃ i<={remnantPanel.LengthMm:F0}mm");
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
                ed.WriteMessage($"\nGhi nháº­n {stepCount} pháº§n cáº¯t báº­c thang vÃ o kho.");
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

            ed.WriteMessage($"\nGhi nháº­n {wasteCount} pháº§n cáº¯t vÃ¹ng má»Ÿ vÃ o kho.");
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
            ed.WriteMessage($"\nTáº¡o xong {layout.AllPanels.Count} táº¥m cho {GetScopeLabel(scope).ToLowerInvariant()} [{request.WallCode}].");
            if (openings.Count > 0)
            {
                ed.WriteMessage($"\nÄÃ£ váº½ {openings.Count} vÃ¹ng cáº¯t.");
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
            => scope == PanelLayoutScope.Ceiling ? "SD_CEILING_QUICK" : "SD_WALL_QUICK";

        private static string GetScopeLabel(PanelLayoutScope scope)
            => scope == PanelLayoutScope.Ceiling ? "Tráº§n" : "TÆ°á»ng";

        private static string GetBoundaryLabel(PanelLayoutScope scope)
            => scope == PanelLayoutScope.Ceiling ? "tráº§n" : "tÆ°á»ng";

        private static string GetOpeningLabel(PanelLayoutScope scope)
            => scope == PanelLayoutScope.Ceiling ? "lá»— má»Ÿ / Ã´ trá»‘ng tráº§n" : "lá»— má»Ÿ";
    }
}
