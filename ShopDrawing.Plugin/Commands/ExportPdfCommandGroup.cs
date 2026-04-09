using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(ShopDrawing.Plugin.Commands.ExportPdfCommandGroup))]

namespace ShopDrawing.Plugin.Commands
{
    public sealed class ExportPdfCommandGroup
    {
        private readonly ExportPdfCommandActions _actions = new();

        [CommandMethod("SD_EXPORT")]
        public void ToggleExportPdfPalette()
        {
            _actions.TogglePalette();
        }

        [CommandMethod("_SD_PLOT_API_WRAPPER", CommandFlags.Session)]
        public void ExportPdfWrapper()
        {
            _actions.ExportPdfWrapper();
        }
    }
}
