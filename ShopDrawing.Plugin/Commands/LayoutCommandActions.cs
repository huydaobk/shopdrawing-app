using ShopDrawing.Plugin.Modules.Layout;
using ShopDrawing.Plugin.Modules;

namespace ShopDrawing.Plugin.Commands
{
    internal sealed class LayoutCommandActions
    {
        public void TogglePalette()
        {
            ShopDrawingModuleRegistry.Layout.TogglePalette();
        }

        public void CreateLayoutFromPalette()
        {
            ShopDrawingModuleRegistry.Layout.CreateLayoutFromPalette();
        }
    }
}
