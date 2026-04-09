using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(ShopDrawing.Plugin.Commands.TenderCommandGroup))]

namespace ShopDrawing.Plugin.Commands
{
    public sealed class TenderCommandGroup
    {
        private readonly TenderCommandActions _actions = new();

        [CommandMethod("SD_TENDER")]
        public void ToggleTenderPalette()
        {
            _actions.TogglePalette();
        }
    }
}
