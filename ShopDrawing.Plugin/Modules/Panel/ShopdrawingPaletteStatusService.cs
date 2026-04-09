using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Modules.Panel
{
    internal sealed class ShopdrawingPaletteStatusService
    {
        private const string UnknownPanelStatus           = "● Đang thiết kế: -";
        private const string UnknownWastePercentStatus    = "● Hao hụt: - %";
        private const string MissingWasteDbPercentStatus  = "● Waste DB chưa sẵn sàng";
        private const string MissingWasteDbStatus         = "● Kho lẻ: DB chưa sẵn sàng";
        private const string WasteDbErrorStatus           = "● Kho lẻ: Lỗi DB";

        public ShopdrawingPaletteStatusSnapshot GetSnapshot(WasteRepository? wasteRepo)
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;

                WasteStats? stats;
                string wastePercentText = BuildWastePercentText(doc, wasteRepo, out stats);
                string panelStatusText  = BuildPanelStatusText(doc);
                string wasteStatusText  = BuildWasteStatusText(wasteRepo, stats);

                return new ShopdrawingPaletteStatusSnapshot(
                    panelStatusText,
                    wasteStatusText,
                    wastePercentText);
            }
            catch
            {
                return new ShopdrawingPaletteStatusSnapshot(
                    UnknownPanelStatus,
                    MissingWasteDbStatus,
                    UnknownWastePercentStatus);
            }
        }

        private static string BuildWastePercentText(Document? doc, WasteRepository? wasteRepo, out WasteStats? stats)
        {
            stats = null;
            if (wasteRepo == null)
            {
                return MissingWasteDbPercentStatus;
            }

            try
            {
                var calc  = new WasteCalculator();
                stats = calc.CalculateLiveStats(doc, wasteRepo);
                return $"● Hao hụt: {stats.WastePercentage:F1} %";
            }
            catch
            {
                return UnknownWastePercentStatus;
            }
        }

        private static string BuildPanelStatusText(Document? doc)
        {
            if (doc == null)
            {
                return UnknownPanelStatus;
            }

            try
            {
                int panelCount = 0;
                using var tr = doc.Database.TransactionManager.StartTransaction();
                var blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in modelSpace)
                {
                    if (id.IsErased) continue;
                    if (tr.GetObject(id, OpenMode.ForRead) is Entity entity &&
                        entity.Layer == "SD_PANEL" &&
                        entity is Polyline)
                    {
                        panelCount++;
                    }
                }

                tr.Commit();
                return $"\u25cf \u0110ang thi\u1ebft k\u1ebf: {panelCount} t\u1ea5m";
            }
            catch
            {
                return UnknownPanelStatus;
            }
        }

        private static string BuildWasteStatusText(WasteRepository? wasteRepo, WasteStats? stats)
        {
            if (wasteRepo == null)
            {
                return MissingWasteDbStatus;
            }

            try
            {
                if (stats != null)
                {
                    return $"\u25cf Kho l\u1ebb: {stats.AvailableCount} t\u1ea5m ({stats.TotalWasteAreaM2:F2} m\u00b2)";
                }

                // Fallback: query trực tiếp nếu stats null
                var wastes = wasteRepo.GetAll("available");
                double areaMm2 = 0;
                foreach (var w in wastes) areaMm2 += (w.WidthMm * w.LengthMm);
                return $"\u25cf Kho l\u1ebb: {wastes.Count} t\u1ea5m ({areaMm2 / 1_000_000.0:F2} m\u00b2)";
            }
            catch
            {
                return WasteDbErrorStatus;
            }
        }
    }
}
