using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using ShopDrawing.Plugin.Data;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    public class WasteCalculator
    {
        public WasteStats CalculateLiveStats(Document? doc, WasteRepository? wasteRepo)
        {
            var stats = new WasteStats();

            if (wasteRepo != null)
            {
                try
                {
                    // Status co the bi lech hoa/thuong hoac du khoang trang o du lieu cu,
                    // nen luon normalize khi tong hop.
                    var allWastes = wasteRepo.GetAll();
                    foreach (var w in allWastes)
                    {
                        double areaMm2 = w.WidthMm * w.LengthMm;
                        if (HasStatus(w.Status, "available"))
                        {
                            stats.AvailableCount++;
                            stats.TotalWasteAreaMm2 += areaMm2;
                            continue;
                        }

                        if (HasStatus(w.Status, "discarded"))
                        {
                            stats.TotalDiscardedAreaMm2 += areaMm2;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    PluginLogger.Warn("WasteCalculator DB error: " + ex.Message);
                }
            }

            // 4. Tong m2 panel huu ich tren ban ve (layer SD_PANEL)
            //    Scan qua toan bo entity trong ModelSpace
            //    Luu y: Don vi phu thuoc cai dat trong CAD (thong thuong = mm)
            if (doc != null)
            {
                try
                {
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        var rows = ShopDrawing.Plugin.Commands.ShopDrawingCommands.BomManager.ScanDocumentForPanels(tr, doc.Database);
                        
                        // rows: grouped, so we need AreaM2 * Qty
                        foreach (var r in rows)
                        {
                            stats.TotalUsefulAreaMm2 += (r.AreaM2 * r.Qty) * 1_000_000.0;
                        }

                        tr.Commit();
                    }
                }
                catch (System.Exception ex)
                {
                    PluginLogger.Warn("WasteCalculator CAD scan error: " + ex.Message);
                }
            }

            return stats;
        }

        private static bool HasStatus(string? status, string expected)
            => string.Equals((status ?? string.Empty).Trim(), expected, System.StringComparison.OrdinalIgnoreCase);
    }
}
