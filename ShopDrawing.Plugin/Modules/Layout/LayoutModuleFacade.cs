using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ShopDrawing.Plugin.Modules.Layout
{
    internal sealed class LayoutModuleFacade
    {
        private PaletteSet? _paletteSet;

        public void TogglePalette()
        {
            if (_paletteSet == null || _paletteSet.IsDisposed)
            {
                _paletteSet = new PaletteSet("Layout Manager");
                _paletteSet.Style = PaletteSetStyles.ShowCloseButton
                    | PaletteSetStyles.ShowPropertiesMenu
                    | PaletteSetStyles.ShowAutoHideButton;
                _paletteSet.MinimumSize = new System.Drawing.Size(320, 560);
                _paletteSet.AddVisual("Layout", new LayoutManagerPaletteControl());
            }

            _paletteSet.Visible = !_paletteSet.Visible;
        }

        public void CreateLayoutFromPalette()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var pending = LayoutManagerPaletteControl.TakePendingRequest(doc.Name);
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
                    MarginLeft = request.MarginLeft,
                    MarginRight = request.MarginRight,
                    MarginTop = request.MarginTop,
                    MarginBot = request.MarginBot,
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
                    palette?.Dispatcher.BeginInvoke(new Action(() => palette.SetStatusPublic("⚠️ Đã huỷ.")));
                    return;
                }

                var layouts = engine.CreateLayoutsFromRegion(request, revisionDialog.RevisionContent);

                palette?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (layouts.Count == 1)
                        palette.SetStatusPublic($"✅ Tạo: \"{layouts[0]}\"");
                    else
                        palette.SetStatusPublic($"✅ {layouts.Count} trang:\n" + string.Join("\n", layouts));
                    palette.RefreshLayoutListPublic();
                }));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[SD] ❌ Layout error: {ex.GetType().Name}: {ex.Message}");
                ed.WriteMessage($"\n[SD] StackTrace: {ex.StackTrace}");
                palette?.Dispatcher.BeginInvoke(new Action(() => palette.SetStatusPublic($"❌ {ex.Message}")));
            }
        }
    }
}
