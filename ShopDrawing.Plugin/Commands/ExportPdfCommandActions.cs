using ShopDrawing.Plugin.Modules.Export;
using ShopDrawing.Plugin.Modules;

namespace ShopDrawing.Plugin.Commands
{
    internal sealed class ExportPdfCommandActions
    {
        public void TogglePalette()
        {
            ShopDrawingModuleRegistry.Export.TogglePalette();
        }

        public void ExportPdfWrapper()
        {
            ShopDrawingModuleRegistry.Export.ExportPdfWrapper();
        }
    }
}
