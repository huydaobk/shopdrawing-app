using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(ShopDrawing.Plugin.Commands.SmartDimCommandGroup))]

namespace ShopDrawing.Plugin.Commands
{
    public sealed class SmartDimCommandGroup
    {
        private readonly SmartDimCommandActions _actions = new();

        [CommandMethod("SD_SMART_DIM")]
        public void ToggleSmartDimPalette()
        {
            _actions.TogglePalette();
        }

        [CommandMethod("_SD_UPDATE_TEXT")]
        public void UpdateAllPluginText()
        {
            _actions.UpdateAllPluginText();
        }
    }
}
