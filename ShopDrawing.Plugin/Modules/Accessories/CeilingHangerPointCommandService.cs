using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using ShopDrawing.Plugin.Core;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ShopDrawing.Plugin.Modules.Accessories
{
    internal sealed class CeilingHangerPointCommandService
    {
        private const string TBarPointBlockName = "SD_T_HANGER_POINT_MARK";
        private const string MushroomPointBlockName = "SD_MUSHROOM_HANGER_POINT_MARK";

        public void Run(BlockManager blockManager, CeilingHangerPointKind pointKind)
        {
            Document? doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            Editor ed = doc.Editor;
            int insertedCount = 0;

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    blockManager.EnsureLayers(tr);
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    ObjectId blockId = EnsureMarkerBlock(doc.Database, tr, pointKind);
                    string layerName = GetLayerName(pointKind);
                    double scale = Math.Max(40.0, GetDrawingScale(doc.Database));

                    while (true)
                    {
                        PromptPointOptions options = new PromptPointOptions(
                            insertedCount == 0
                                ? $"\nChọn điểm treo {GetPromptLabel(pointKind)} đầu tiên (Enter để kết thúc): "
                                : $"\nChọn tiếp điểm treo {GetPromptLabel(pointKind)} (Enter để kết thúc): ")
                        {
                            AllowNone = true
                        };

                        PromptPointResult result = ed.GetPoint(options);
                        if (result.Status == PromptStatus.None)
                        {
                            break;
                        }

                        if (result.Status != PromptStatus.OK)
                        {
                            return;
                        }

                        var blockRef = new BlockReference(result.Value, blockId)
                        {
                            Layer = layerName,
                            ScaleFactors = new Scale3d(scale, scale, 1.0)
                        };

                        ms.AppendEntity(blockRef);
                        tr.AddNewlyCreatedDBObject(blockRef, true);
                        insertedCount++;
                    }

                    tr.Commit();
                }

                ed.WriteMessage($"\nĐã đặt {insertedCount} điểm treo {GetPromptLabel(pointKind)}.");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nLỗi pick điểm treo: {ex.Message}");
            }
        }

        private static string GetLayerName(CeilingHangerPointKind pointKind)
            => pointKind == CeilingHangerPointKind.TBar ? "SD_CEILING_T_HANGER" : "SD_CEILING_MUSHROOM_HANGER";

        private static string GetPromptLabel(CeilingHangerPointKind pointKind)
            => pointKind == CeilingHangerPointKind.TBar ? "thanh T" : "bulong nấm";

        private static string GetBlockName(CeilingHangerPointKind pointKind)
            => pointKind == CeilingHangerPointKind.TBar ? TBarPointBlockName : MushroomPointBlockName;

        private static ObjectId EnsureMarkerBlock(Database db, Transaction tr, CeilingHangerPointKind pointKind)
        {
            string blockName = GetBlockName(pointKind);
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (bt.Has(blockName))
            {
                return bt[blockName];
            }

            bt.UpgradeOpen();
            var marker = new BlockTableRecord
            {
                Name = blockName,
                Origin = Point3d.Origin
            };

            ObjectId markerId = bt.Add(marker);
            tr.AddNewlyCreatedDBObject(marker, true);

            if (pointKind == CeilingHangerPointKind.TBar)
            {
                AddLine(marker, tr, new Point3d(-1.4, 1.0, 0), new Point3d(1.4, 1.0, 0));
                AddLine(marker, tr, new Point3d(0, 1.0, 0), new Point3d(0, -1.4, 0));
                AddLine(marker, tr, new Point3d(-1.2, -1.4, 0), new Point3d(1.2, -1.4, 0));
            }
            else
            {
                AddLine(marker, tr, new Point3d(0, 1.6, 0), new Point3d(1.6, 0, 0));
                AddLine(marker, tr, new Point3d(1.6, 0, 0), new Point3d(0, -1.6, 0));
                AddLine(marker, tr, new Point3d(0, -1.6, 0), new Point3d(-1.6, 0, 0));
                AddLine(marker, tr, new Point3d(-1.6, 0, 0), new Point3d(0, 1.6, 0));
                AddLine(marker, tr, new Point3d(-1.0, 0, 0), new Point3d(1.0, 0, 0));
            }

            return markerId;
        }

        private static void AddLine(BlockTableRecord marker, Transaction tr, Point3d start, Point3d end)
        {
            var line = new Line(start, end)
            {
                Layer = "0",
                Color = Color.FromColorIndex(ColorMethod.ByBlock, 0)
            };

            marker.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
        }

        private static double GetDrawingScale(Database db)
        {
            try
            {
                var acScale = db.Cannoscale;
                if (acScale != null && acScale.DrawingUnits > 0)
                {
                    return acScale.DrawingUnits / acScale.PaperUnits;
                }
            }
            catch
            {
            }

            return 100.0;
        }
    }
}
