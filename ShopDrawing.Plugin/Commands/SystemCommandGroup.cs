using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(ShopDrawing.Plugin.Commands.SystemCommandGroup))]

namespace ShopDrawing.Plugin.Commands
{
    public sealed class SystemCommandGroup
    {
        private readonly SystemCommandActions _actions = new();

        [CommandMethod("SD_INIT_PROJECT")]
        public void InitializeProject()
        {
            _actions.InitializeProject();
        }

        [CommandMethod("SD_INPUT")]
        public void InputProjectInfo()
        {
            _actions.InputProjectInfo();
        }
    }
}
