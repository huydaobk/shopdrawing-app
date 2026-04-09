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
                var opt = new PromptEntityOptions("\nChá»n Polyline biÃªn tÆ°á»ng Ä‘á»ƒ chÃ¨n Detail:");
                opt.SetRejectMessage("\nPháº£i lÃ  Polyline!");
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
                ed.WriteMessage($"\nâœ… ÄÃ£ chÃ¨n {count} details (loáº¡i {detailType}).");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nâŒ Lá»—i chÃ¨n detail: {ex.Message}");
            }
        }
    }
}
