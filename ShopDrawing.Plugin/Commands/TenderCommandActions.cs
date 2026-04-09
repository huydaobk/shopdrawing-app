using ShopDrawing.Plugin.Modules.Tender;
using ShopDrawing.Plugin.Modules;

namespace ShopDrawing.Plugin.Commands
{
    internal sealed class TenderCommandActions
    {
        public void TogglePalette()
        {
            ShopDrawingModuleRegistry.Tender.TogglePalette();
        }
    }
}
