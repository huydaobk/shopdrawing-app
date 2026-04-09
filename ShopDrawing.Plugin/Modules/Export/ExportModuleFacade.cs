using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Windows;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ShopDrawing.Plugin.Modules.Export
{
    internal sealed class ExportModuleFacade
    {
        private PaletteSet? _paletteSet;

        public void TogglePalette()
        {
            if (_paletteSet == null || _paletteSet.IsDisposed)
            {
                _paletteSet = new PaletteSet("Export PDF");
                _paletteSet.Style = PaletteSetStyles.ShowCloseButton
                    | PaletteSetStyles.ShowPropertiesMenu
                    | PaletteSetStyles.ShowAutoHideButton;
                _paletteSet.MinimumSize = new System.Drawing.Size(300, 450);
                _paletteSet.AddVisual("Export PDF", new ExportPdfPaletteControl());
            }

            _paletteSet.Visible = !_paletteSet.Visible;
        }

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
            var palette = pending.GetPalette();

            var engine = new PdfExportEngine();
            try
            {
                engine.ExportDrawingsToPdf(doc, options, (msg, _, _) =>
                {
                    palette?.ReportProgress(msg, false, string.Empty, false);
                });

                palette?.ReportProgress(
                    $"✅ Đã xuất PDF thành công ({options.OutputFilePath})",
                    true,
                    options.OutputFilePath,
                    options.OpenAfterExport);
            }
            catch (System.Exception ex)
            {
                palette?.ReportProgress($"❌ Lỗi xuất PDF: {ex.Message}", true, string.Empty, false);
                ed.WriteMessage($"\n[SD_EXPORT] ERROR: {ex.Message}\n{ex.StackTrace}\n");
            }
        }
    }
}
