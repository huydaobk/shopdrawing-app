using ShopDrawing.Plugin.Modules;
using ShopDrawing.Plugin.Modules.Accessories;
using ShopDrawing.Plugin.Runtime;

namespace ShopDrawing.Plugin.Commands
{
    internal sealed class ShopdrawingCommandActions
    {
        public void TogglePalette()
        {
            ShopDrawingModuleRegistry.Panel.TogglePalette();
        }

        public void CreateWallQuick()
        {
            ShopDrawingModuleRegistry.Panel.CreateWallQuick(
                ShopDrawingRuntimeServices.Settings,
                ShopDrawingRuntimeServices.LayoutEngine,
                ShopDrawingRuntimeServices.WasteRepo,
                ShopDrawingRuntimeServices.BlockManager,
                ShopDrawingRuntimeServices.BomManager);
        }

        public void CreateCeilingQuick()
        {
            ShopDrawingModuleRegistry.Panel.CreateCeilingQuick(
                ShopDrawingRuntimeServices.Settings,
                ShopDrawingRuntimeServices.LayoutEngine,
                ShopDrawingRuntimeServices.WasteRepo,
                ShopDrawingRuntimeServices.BlockManager,
                ShopDrawingRuntimeServices.BomManager);
        }

        public void PlaceDetail()
        {
            ShopDrawingModuleRegistry.Panel.PlaceDetail(
                ShopDrawingRuntimeServices.BlockManager,
                ShopDrawingRuntimeServices.Settings.CurrentDetailType);
        }

        public void PickTHangerPoints()
        {
            ShopDrawingModuleRegistry.Accessories.PickCeilingHangerPoints(
                ShopDrawingRuntimeServices.BlockManager,
                CeilingHangerPointKind.TBar);
        }

        public void PickMushroomHangerPoints()
        {
            ShopDrawingModuleRegistry.Accessories.PickCeilingHangerPoints(
                ShopDrawingRuntimeServices.BlockManager,
                CeilingHangerPointKind.Mushroom);
        }

        public void PickOutsideCornerMarkers()
        {
            ShopDrawingModuleRegistry.Accessories.PickWallCornerMarker(
                ShopDrawingRuntimeServices.BlockManager,
                ShopDrawingRuntimeServices.Settings,
                WallCornerMarkerKind.Outside);
        }

        public void PickInsideCornerMarkers()
        {
            ShopDrawingModuleRegistry.Accessories.PickWallCornerMarker(
                ShopDrawingRuntimeServices.BlockManager,
                ShopDrawingRuntimeServices.Settings,
                WallCornerMarkerKind.Inside);
        }
    }
}
