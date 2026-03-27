using System;
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
                    // Tổng m² tấm available (kho lẻ sẵn dùng)
                    var availableWastes = wasteRepo.GetAll("available");
                    foreach (var w in availableWastes)
                        stats.TotalWasteAreaMm2 += (w.WidthMm * w.LengthMm);

                    // Tổng m² tấm discarded (STEP/OPEN/TRIM/REM bỏ)
                    var discardedWastes = wasteRepo.GetAll("discarded");
                    foreach (var w in discardedWastes)
                        stats.TotalDiscardedAreaMm2 += (w.WidthMm * w.LengthMm);
                }
                catch { /* Ignore DB errors */ }
            }

            // Tổng m² panel trên bản vẽ (SD_PANEL layer)
            if (doc != null)
            {
                try
                {
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                        
                        foreach (ObjectId id in ms)
                        {
                            var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent != null && ent.Layer == "SD_PANEL" && ent is Polyline pl)
                            {
                                try
                                {
                                    var ext = pl.GeometricExtents;
                                    double w = ext.MaxPoint.X - ext.MinPoint.X;
                                    double h = ext.MaxPoint.Y - ext.MinPoint.Y;
                                    stats.TotalUsefulAreaMm2 += (w * h);
                                }
                                catch { }
                            }
                        }
                        tr.Commit();
                    }
                }
                catch (System.Exception)
                {
                    // Ignore transient transaction errors during auto-update
                }
            }

            return stats;
        }
    }
}
