using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using ShopDrawing.Plugin.Core;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ShopDrawing.Plugin.Modules.SmartDim
{
    internal sealed class UpdatePluginTextService
    {
        public void Run(double defaultTextHeightMm)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            double scaleFactor = 100.0;
            try
            {
                var annotationScale = db.Cannoscale;
                if (annotationScale != null && annotationScale.DrawingUnits > 0)
                {
                    scaleFactor = annotationScale.DrawingUnits / annotationScale.PaperUnits;
                }
            }
            catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in UpdatePluginTextService.cs", ex);
            }

            double newHeight = defaultTextHeightMm * scaleFactor;
            if (newHeight < 20.0)
            {
                newHeight = 20.0;
            }

            var targetLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SD_TAG",
                "SD_OPENING"
            };

            int textCount = 0;
            int dimCount = 0;

            try
            {
                using var docLock = doc.LockDocument();
                using var tr = db.TransactionManager.StartTransaction();
                var modelSpace = (BlockTableRecord)tr.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(db),
                    OpenMode.ForRead);

                var arialStyleId = BlockManager.EnsureArialStyle(db, tr);
                var dimStyleId = SmartDimEngine.EnsureDimStyle(db, tr);

                foreach (ObjectId id in modelSpace)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead);
                    if (entity is DBText text && targetLayers.Contains(text.Layer))
                    {
                        text.UpgradeOpen();
                        text.Height = newHeight;
                        text.TextStyleId = arialStyleId;
                        textCount++;
                    }
                    else if (entity is DBText dimText &&
                             dimText.GetXDataForApplication(SmartDimEngine.XDATA_APP) != null)
                    {
                        dimText.UpgradeOpen();
                        dimText.Height = newHeight;
                        dimText.TextStyleId = arialStyleId;
                        textCount++;
                    }
                    else if (entity is Dimension dim &&
                             (string.Equals(dim.Layer, SmartDimEngine.DIM_LAYER_PANEL, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(dim.Layer, SmartDimEngine.DIM_LAYER_OPENING, StringComparison.OrdinalIgnoreCase)))
                    {
                        dim.UpgradeOpen();
                        dim.DimensionStyle = dimStyleId;
                        SmartDimEngine.ApplyAnnotativeContextsForUpdate(dim, db);
                        SmartDimEngine.NormalizeDimensionAppearanceForUpdate(dim, db);
                        dim.RecordGraphicsModified(true);
                        dimCount++;
                    }
                }

                tr.Commit();
                ed.WriteMessage($"\n✅ SD_UPDATE_TEXT: Đã cập nhật {textCount} text objects, {dimCount} dim objects → {defaultTextHeightMm}mm × {scaleFactor:F0} = {newHeight:F0}mm | Font: Arial");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ SD_UPDATE_TEXT lỗi: {ex.Message}");
            }
        }
    }
}
