using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Runtime;

namespace ShopDrawing.Plugin.Modules.Panel
{
    internal sealed class QuickWallCommandService
    {
        private readonly QuickPanelLayoutCommandService _commandService = new();

        public void Run(
            ShopDrawingRuntimeSettings settings,
            LayoutEngine layoutEngine,
            WasteRepository? wasteRepo,
            BlockManager blockManager,
            BomManager bomManager)
        {
            _commandService.Run(
                PanelLayoutScope.Wall,
                settings,
                layoutEngine,
                wasteRepo,
                blockManager,
                bomManager);
        }
    }
}
