using System;
using System.Collections.Generic;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Runtime
{
    internal sealed class ShopDrawingRuntimeSettings
    {
        public int DefaultThickness { get; set; } = 50;

        public string DefaultAnnotationScales { get; set; } = "1:100";

        public double DefaultTextHeightMm { get; set; } = 1.8;

        public bool EnableOpeningCut { get; set; }

        public DetailType CurrentDetailType { get; set; } = DetailType.All;

        public int WallCounter { get; set; } = 1;

        public string DefaultWallCode { get; set; } = "W1";

        public double DefaultPanelWidth { get; set; } = 1100;

        public string DefaultSpec { get; set; } = string.Empty;

        public double DefaultJointGap { get; set; } = 2;

        public LayoutDirection DefaultDirection { get; set; } = LayoutDirection.Horizontal;

        public StartEdge DefaultStartEdge { get; set; } = StartEdge.Left;

        public LayoutDirection DefaultCeilingSuspensionDirection { get; set; } = LayoutDirection.Vertical;

        public bool DefaultCeilingDivideFromMaxSide { get; set; }

        public double DefaultCeilingTSpacingMm { get; set; } = 3000;

        public double DefaultCeilingTClearGapMm { get; set; } = 10;

        public int DefaultCeilingMushroomDivisionCount { get; set; }

        public List<double> DefaultCeilingBaySpansMm { get; set; } = new();

        public List<bool> DefaultCeilingBayHasMushroomFlags { get; set; } = new();

        public string DefaultApplication { get; set; } = "Phòng sạch";

        public double DefaultCeilingCableDropMm { get; set; } = 1500;

        public string DefaultWallTopPanelTreatment { get; set; } = TenderWall.TopPanelTreatmentNone;

        public string DefaultWallStartPanelTreatment { get; set; } = TenderWall.EndPanelTreatmentNone;

        public string DefaultWallEndPanelTreatment { get; set; } = TenderWall.EndPanelTreatmentNone;

        public bool DefaultWallBottomEdgeEnabled { get; set; } = true;

        public string DefaultOpeningType { get; set; } = "Cửa sổ/LKT";

        public event Action<string>? AnnotationScaleChanged;

        public event Action? WasteUpdated;

        public event Action<string>? WallCodeChanged;

        public void SetAnnotationScale(string scaleName)
        {
            DefaultAnnotationScales = scaleName;
            AnnotationScaleChanged?.Invoke(scaleName);
        }

        public void NotifyWasteUpdated()
        {
            WasteUpdated?.Invoke();
        }

        public void SetDefaultWallCode(string wallCode)
        {
            DefaultWallCode = wallCode;
            WallCodeChanged?.Invoke(wallCode);
        }
    }
}
