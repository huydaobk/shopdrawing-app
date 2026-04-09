using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Plugin.Commands
{
    internal sealed class SystemCommandActions
    {
        private readonly ProjectInitializationService _projectInitializationService = new();

        public void InitializeProject()
        {
            _projectInitializationService.InitializeInteractive();
        }
    }
}
