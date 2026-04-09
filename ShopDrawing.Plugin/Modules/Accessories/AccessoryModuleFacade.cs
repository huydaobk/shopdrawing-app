using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Runtime;

namespace ShopDrawing.Plugin.Modules.Accessories
{
    internal sealed class AccessoryModuleFacade
    {
        private readonly CeilingHangerPointCommandService _hangerPointCommandService = new();
        private readonly WallCornerMarkerCommandService _wallCornerMarkerCommandService = new();

        public void PickCeilingHangerPoints(BlockManager blockManager, CeilingHangerPointKind pointKind)
        {
            _hangerPointCommandService.Run(blockManager, pointKind);
        }

        public void PickWallCornerMarker(BlockManager blockManager, ShopDrawingRuntimeSettings settings, WallCornerMarkerKind kind)
        {
            _wallCornerMarkerCommandService.Run(blockManager, settings, kind);
        }
    }
}
