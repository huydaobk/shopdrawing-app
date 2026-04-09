using ShopDrawing.Plugin.Modules.SmartDim;
using ShopDrawing.Plugin.Modules;
using ShopDrawing.Plugin.Runtime;

namespace ShopDrawing.Plugin.Commands
{
    internal sealed class SmartDimCommandActions
    {
        public void TogglePalette()
        {
            ShopDrawingModuleRegistry.SmartDim.TogglePalette();
        }

        public void UpdateAllPluginText()
        {
            ShopDrawingModuleRegistry.SmartDim.UpdateAllPluginText(ShopDrawingRuntimeServices.Settings.DefaultTextHeightMm);
        }
    }
}
