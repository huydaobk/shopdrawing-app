using System;
using System.Collections.Generic;
using System.Globalization;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Models;
using ShopDrawing.Plugin.Modules;
using ShopDrawing.Plugin.Runtime;

namespace ShopDrawing.Plugin.Modules.Panel
{
    internal sealed class ShopdrawingPaletteFacade
    {
        private readonly ShopdrawingPaletteStatusService _statusService = new();
        private static ShopDrawingRuntimeSettings Settings => ShopDrawingRuntimeServices.Settings;

        public SpecConfigManager SpecManager => ShopDrawingRuntimeServices.SpecManager;

        public WasteRepository? WasteRepo => ShopDrawingRuntimeServices.WasteRepo;

        public IReadOnlyList<PanelSpec> GetSpecs()
        {
            return SpecManager.GetAll();
        }

        public int DefaultThickness
        {
            get => Settings.DefaultThickness;
            set => Settings.DefaultThickness = value;
        }

        public double DefaultPanelWidth
        {
            get => Settings.DefaultPanelWidth;
            set => Settings.DefaultPanelWidth = value;
        }

        public string DefaultSpec
        {
            get => Settings.DefaultSpec;
            set => Settings.DefaultSpec = value;
        }

        public double DefaultTextHeightMm
        {
            get => Settings.DefaultTextHeightMm;
            set => Settings.DefaultTextHeightMm = value;
        }

        public string DefaultApplication
        {
            get => Settings.DefaultApplication;
            set => Settings.DefaultApplication = value;
        }

        public string DefaultWallTopPanelTreatment
        {
            get => Settings.DefaultWallTopPanelTreatment;
            set => Settings.DefaultWallTopPanelTreatment = value;
        }

        public string DefaultWallStartPanelTreatment
        {
            get => Settings.DefaultWallStartPanelTreatment;
            set => Settings.DefaultWallStartPanelTreatment = value;
        }

        public string DefaultWallEndPanelTreatment
        {
            get => Settings.DefaultWallEndPanelTreatment;
            set => Settings.DefaultWallEndPanelTreatment = value;
        }

        public bool DefaultWallBottomEdgeEnabled
        {
            get => Settings.DefaultWallBottomEdgeEnabled;
            set => Settings.DefaultWallBottomEdgeEnabled = value;
        }

        public string DefaultOpeningType
        {
            get => Settings.DefaultOpeningType;
            set => Settings.DefaultOpeningType = value;
        }

        public string DefaultAnnotationScales
        {
            get => Settings.DefaultAnnotationScales;
            set => Settings.DefaultAnnotationScales = value;
        }

        public LayoutDirection DefaultDirection
        {
            get => Settings.DefaultDirection;
            set => Settings.DefaultDirection = value;
        }

        public StartEdge DefaultStartEdge
        {
            get => Settings.DefaultStartEdge;
            set => Settings.DefaultStartEdge = value;
        }

        public double DefaultJointGap
        {
            get => Settings.DefaultJointGap;
            set => Settings.DefaultJointGap = value;
        }

        public double DefaultCeilingCableDropMm
        {
            get => Settings.DefaultCeilingCableDropMm;
            set => Settings.DefaultCeilingCableDropMm = value;
        }

        public bool EnableOpeningCut
        {
            get => Settings.EnableOpeningCut;
            set => Settings.EnableOpeningCut = value;
        }

        public DetailType CurrentDetailType
        {
            get => Settings.CurrentDetailType;
            set => Settings.CurrentDetailType = value;
        }

        public event Action<string>? AnnotationScaleChanged
        {
            add => Settings.AnnotationScaleChanged += value;
            remove => Settings.AnnotationScaleChanged -= value;
        }

        public void ManageSpecs()
        {
            ShopDrawingModuleRegistry.Panel.ManageSpecs(SpecManager);
        }

        public void ShowWaste()
        {
            ShopDrawingModuleRegistry.Panel.ShowWaste();
        }

        public void ExportBom()
        {
            ShopDrawingModuleRegistry.Panel.ExportBom(ShopDrawingRuntimeServices.BomManager, WasteRepo);
        }

        public void UpdateAllPluginText()
        {
            ShopDrawingModuleRegistry.SmartDim.UpdateAllPluginText(Settings.DefaultTextHeightMm);
        }

        public bool TrySetTextHeight(string rawText)
        {
            if (!double.TryParse(
                    rawText.Replace(",", "."),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double textHeightMm) ||
                textHeightMm <= 0)
            {
                return false;
            }

            Settings.DefaultTextHeightMm = textHeightMm;
            return true;
        }

        public bool TrySetJointGap(string rawText)
        {
            if (!double.TryParse(
                    rawText.Replace(",", "."),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double jointGapMm) ||
                jointGapMm < 0)
            {
                return false;
            }

            Settings.DefaultJointGap = jointGapMm;
            return true;
        }

        public bool TrySetCeilingCableDrop(string rawText)
        {
            if (!double.TryParse(
                    rawText.Replace(",", "."),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double cableDropMm) ||
                cableDropMm < 0)
            {
                return false;
            }

            Settings.DefaultCeilingCableDropMm = cableDropMm;
            return true;
        }

        public void SetDirectionByIndex(int selectedIndex)
        {
            Settings.DefaultDirection = selectedIndex == 0
                ? LayoutDirection.Horizontal
                : LayoutDirection.Vertical;
        }

        public void SetStartEdgeByIndex(int selectedIndex)
        {
            Settings.DefaultStartEdge = selectedIndex == 0
                ? StartEdge.Left
                : StartEdge.Right;
        }

        public void SetOpeningCut(bool enabled)
        {
            Settings.EnableOpeningCut = enabled;
        }

        public void SetApplication(string application)
        {
            if (!string.IsNullOrWhiteSpace(application))
            {
                Settings.DefaultApplication = application.Trim();
            }
        }

        public void SetWallTopTreatment(string treatment)
        {
            Settings.DefaultWallTopPanelTreatment = TenderWall.NormalizeTopPanelTreatment(treatment);
        }

        public void SetWallStartTreatment(string treatment)
        {
            Settings.DefaultWallStartPanelTreatment = TenderWall.NormalizeEndPanelTreatment(treatment);
        }

        public void SetWallEndTreatment(string treatment)
        {
            Settings.DefaultWallEndPanelTreatment = TenderWall.NormalizeEndPanelTreatment(treatment);
        }

        public void SetWallBottomEdgeEnabled(bool enabled)
        {
            Settings.DefaultWallBottomEdgeEnabled = enabled;
        }

        public void SetOpeningType(string openingType)
        {
            if (!string.IsNullOrWhiteSpace(openingType))
            {
                Settings.DefaultOpeningType = openingType.Trim();
            }
        }

        public void SetCurrentDetailType(DetailType detailType)
        {
            Settings.CurrentDetailType = detailType;
        }

        public ShopdrawingPaletteStatusSnapshot GetStatusSnapshot()
        {
            return _statusService.GetSnapshot(WasteRepo);
        }

        public ShopdrawingPaletteSettingsSnapshot GetSettingsSnapshot()
        {
            string scaleLabel = Settings.DefaultAnnotationScales.Split(',')[0].Trim();
            return new ShopdrawingPaletteSettingsSnapshot(
                Settings.DefaultSpec,
                Settings.DefaultApplication,
                Settings.DefaultWallTopPanelTreatment,
                Settings.DefaultWallStartPanelTreatment,
                Settings.DefaultWallEndPanelTreatment,
                Settings.DefaultWallBottomEdgeEnabled,
                Settings.DefaultOpeningType,
                Settings.DefaultThickness,
                Settings.DefaultPanelWidth,
                Settings.DefaultTextHeightMm,
                scaleLabel,
                Settings.DefaultJointGap,
                Settings.EnableOpeningCut,
                Settings.CurrentDetailType,
                Settings.DefaultDirection,
                Settings.DefaultStartEdge,
                Settings.DefaultCeilingCableDropMm);
        }

        public ShopdrawingSpecSelectionResult ApplySpec(PanelSpec spec)
        {
            Settings.DefaultSpec = spec.Key;
            if (spec.Thickness > 0)
            {
                Settings.DefaultThickness = spec.Thickness;
            }

            if (spec.PanelWidth > 0)
            {
                Settings.DefaultPanelWidth = spec.PanelWidth;
            }

            return new ShopdrawingSpecSelectionResult(
                Settings.DefaultSpec,
                Settings.DefaultThickness,
                Settings.DefaultPanelWidth);
        }
    }
}
