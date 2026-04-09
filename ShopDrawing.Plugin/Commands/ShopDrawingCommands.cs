using System;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.Modules;
using ShopDrawing.Plugin.Runtime;

namespace ShopDrawing.Plugin.Commands
{
    public class ShopDrawingCommands
    {
        public static SpecConfigManager SpecManager => ShopDrawingRuntimeServices.SpecManager;

        public static WasteRepository? WasteRepo => ShopDrawingRuntimeServices.WasteRepo;

        public static BomManager BomManager => ShopDrawingRuntimeServices.BomManager;

        public static BlockManager BlockManager => ShopDrawingRuntimeServices.BlockManager;

        private static ShopDrawingRuntimeSettings Settings => ShopDrawingRuntimeServices.Settings;

        public static int DefaultThickness
        {
            get => Settings.DefaultThickness;
            set => Settings.DefaultThickness = value;
        }

        public static string DefaultAnnotationScales
        {
            get => Settings.DefaultAnnotationScales;
            set => Settings.DefaultAnnotationScales = value;
        }

        public static double DefaultTextHeightMm
        {
            get => Settings.DefaultTextHeightMm;
            set => Settings.DefaultTextHeightMm = value;
        }

        public static bool EnableOpeningCut
        {
            get => Settings.EnableOpeningCut;
            set => Settings.EnableOpeningCut = value;
        }

        public static DetailType CurrentDetailType
        {
            get => Settings.CurrentDetailType;
            set => Settings.CurrentDetailType = value;
        }

        public static event Action<string>? AnnotationScaleChanged
        {
            add => Settings.AnnotationScaleChanged += value;
            remove => Settings.AnnotationScaleChanged -= value;
        }

        public static void FireAnnotationScaleChanged(string scaleName)
        {
            Settings.SetAnnotationScale(scaleName);
        }

        public static event Action? WasteUpdated
        {
            add => Settings.WasteUpdated += value;
            remove => Settings.WasteUpdated -= value;
        }

        public static void NotifyWasteUpdated()
        {
            Settings.NotifyWasteUpdated();
        }

        public static int WallCounter
        {
            get => Settings.WallCounter;
            set => Settings.WallCounter = value;
        }

        public static string DefaultWallCode
        {
            get => Settings.DefaultWallCode;
            set => Settings.SetDefaultWallCode(value);
        }

        public static event Action<string>? WallCodeChanged
        {
            add => Settings.WallCodeChanged += value;
            remove => Settings.WallCodeChanged -= value;
        }

        public static double DefaultPanelWidth
        {
            get => Settings.DefaultPanelWidth;
            set => Settings.DefaultPanelWidth = value;
        }

        public static string DefaultSpec
        {
            get => Settings.DefaultSpec;
            set => Settings.DefaultSpec = value;
        }

        public static double DefaultJointGap
        {
            get => Settings.DefaultJointGap;
            set => Settings.DefaultJointGap = value;
        }

        public static LayoutDirection DefaultDirection
        {
            get => Settings.DefaultDirection;
            set => Settings.DefaultDirection = value;
        }

        public static StartEdge DefaultStartEdge
        {
            get => Settings.DefaultStartEdge;
            set => Settings.DefaultStartEdge = value;
        }

        public void TogglePalette()
        {
            ShopDrawingModuleRegistry.Panel.TogglePalette();
        }

        public void ToggleSmartDimPalette()
        {
            ShopDrawingModuleRegistry.SmartDim.TogglePalette();
        }

        public void ToggleLayoutPalette()
        {
            ShopDrawingModuleRegistry.Layout.TogglePalette();
        }

        public void ToggleExportPdfPalette()
        {
            ShopDrawingModuleRegistry.Export.TogglePalette();
        }

        public void ExportPdfWrapper()
        {
            ShopDrawingModuleRegistry.Export.ExportPdfWrapper();
        }

        public void CreateLayoutFromPalette()
        {
            ShopDrawingModuleRegistry.Layout.CreateLayoutFromPalette();
        }

        public void CreateWallQuick()
        {
            ShopDrawingModuleRegistry.Panel.CreateWallQuick(
                Settings,
                ShopDrawingRuntimeServices.LayoutEngine,
                WasteRepo,
                BlockManager,
                BomManager);
        }

        public void CreateCeilingQuick()
        {
            ShopDrawingModuleRegistry.Panel.CreateCeilingQuick(
                Settings,
                ShopDrawingRuntimeServices.LayoutEngine,
                WasteRepo,
                BlockManager,
                BomManager);
        }

        public void ManageSpecs()
        {
            ShopDrawingModuleRegistry.Panel.ManageSpecs(SpecManager);
        }

        public void PlaceDetail()
        {
            ShopDrawingModuleRegistry.Panel.PlaceDetail(BlockManager, CurrentDetailType);
        }

        public void RefreshBom()
        {
            BomManager.Refresh();
        }

        public void ExportBom()
        {
            ShopDrawingModuleRegistry.Panel.ExportBom(BomManager, WasteRepo);
        }

        public void ShowWaste()
        {
            ShopDrawingModuleRegistry.Panel.ShowWaste();
        }

        public void UpdateAllPluginText()
        {
            ShopDrawingModuleRegistry.SmartDim.UpdateAllPluginText(DefaultTextHeightMm);
        }

        public void ToggleTenderPalette()
        {
            ShopDrawingModuleRegistry.Tender.TogglePalette();
        }
    }
}
