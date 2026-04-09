using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(ShopDrawing.Plugin.Commands.UpdateCommandGroup))]

namespace ShopDrawing.Plugin.Commands
{
    public sealed class UpdateCommandGroup
    {
        private readonly UpdateCommandActions _actions = new();

        [CommandMethod("SD_CHECK_UPDATE")]
        public void CheckForUpdates()
        {
            _actions.CheckForUpdates();
        }
    }
}
