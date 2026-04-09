using ShopDrawing.Plugin.Modules.Accessories;
using ShopDrawing.Plugin.Modules.Export;
using ShopDrawing.Plugin.Modules.Layout;
using ShopDrawing.Plugin.Modules.Panel;
using ShopDrawing.Plugin.Modules.SmartDim;
using ShopDrawing.Plugin.Modules.Tender;

namespace ShopDrawing.Plugin.Modules
{
    internal static class ShopDrawingModuleRegistry
    {
        public static AccessoryModuleFacade Accessories { get; } = new();

        public static PanelModuleFacade Panel { get; } = new();

        public static SmartDimModuleFacade SmartDim { get; } = new();

        public static LayoutModuleFacade Layout { get; } = new();

        public static ExportModuleFacade Export { get; } = new();

        public static TenderModuleFacade Tender { get; } = new();
    }
}
