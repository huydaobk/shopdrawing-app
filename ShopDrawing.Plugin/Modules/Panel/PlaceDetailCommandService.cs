using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ShopDrawing.Plugin.Modules.Panel
{
    internal sealed class PlaceDetailCommandService
    {
        public void Run(BlockManager blockManager, DetailType detailType)
        {
            Document? doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;

            try
            {
                var opt = new PromptEntityOptions("\nChọn Polyline biên tường để chèn Detail:");
                opt.SetRejectMessage("\nPhải là Polyline!");
                opt.AddAllowedClass(typeof(Polyline), true);
                var entRes = ed.GetEntity(opt);
                if (entRes.Status != PromptStatus.OK) return;

                using var tr = doc.Database.TransactionManager.StartTransaction();
                if (tr.GetObject(entRes.ObjectId, OpenMode.ForRead) is not Polyline polyline)
                {
                    return;
                }

                blockManager.EnsureLayers(tr);

                var placer = new DetailPlacer();
                int count = placer.PlaceDetails(polyline, detailType, tr);

                tr.Commit();
                ed.WriteMessage($"\nâœ… Đã chèn {count} details (loại {detailType}).");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nâŒ Lỗi chèn detail: {ex.Message}");
            }
        }
    }
}
