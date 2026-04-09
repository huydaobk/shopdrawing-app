using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Plugin.Commands
{
    internal sealed class UpdateCommandActions
    {
        public void CheckForUpdates()
        {
            PluginUpdateService.BeginInteractiveCheck();
        }
    }
}
