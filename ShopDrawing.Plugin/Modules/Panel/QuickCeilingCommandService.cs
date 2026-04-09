using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Runtime;

namespace ShopDrawing.Plugin.Modules.Panel
{
    internal sealed class QuickCeilingCommandService
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
                PanelLayoutScope.Ceiling,
                settings,
                layoutEngine,
                wasteRepo,
                blockManager,
                bomManager);
        }
    }
}
