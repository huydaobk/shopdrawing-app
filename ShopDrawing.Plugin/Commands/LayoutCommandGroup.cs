using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(ShopDrawing.Plugin.Commands.LayoutCommandGroup))]

namespace ShopDrawing.Plugin.Commands
{
    public sealed class LayoutCommandGroup
    {
        private readonly LayoutCommandActions _actions = new();

        [CommandMethod("SD_LAYOUT")]
        public void ToggleLayoutPalette()
        {
            _actions.TogglePalette();
        }

        [CommandMethod("_SD_LAYOUT_CREATE", CommandFlags.Session)]
        public void CreateLayoutFromPalette()
        {
            _actions.CreateLayoutFromPalette();
        }
    }
}
