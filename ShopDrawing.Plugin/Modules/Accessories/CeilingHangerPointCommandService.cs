using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Runtime;
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

            // Show selection dialog first
            double initialDrop = ShopDrawingRuntimeServices.Settings.DefaultCeilingCableDropMm;
            var dialog = new ShopDrawing.Plugin.UI.CornerApplicationSelectionDialog(true, initialDrop);
            bool? dialogResult = Application.ShowModalWindow(dialog);
            if (dialogResult != true)
            {
                return;
            }
            string selectedApp = dialog.SelectedApplication;
            double selectedCableDrop = dialog.CableDropMm;
            ShopDrawingRuntimeSettings settings = ShopDrawingRuntimeServices.Settings;
            string selectedSpec = settings.DefaultSpec;

            // Remember user's choice for next time in the same session
            settings.DefaultCeilingCableDropMm = selectedCableDrop;

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
                        
                        AddAttributesToMarker(tr, blockId, blockRef, pointKind, selectedApp, selectedSpec, selectedCableDrop);
                        insertedCount++;
                    }

                    tr.Commit();
                }

                if (insertedCount > 0)
                {
                    ShopDrawingRuntimeServices.Settings.NotifyWasteUpdated();
                }

                ed.WriteMessage($"\nĐã đặt {insertedCount} điểm treo {GetPromptLabel(pointKind)}.");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nLỗi pick điểm treo: {ex.Message}");
            }
        }

        private static void AddAttributesToMarker(
            Transaction tr,
            ObjectId blockDefId,
            BlockReference blockReference,
            CeilingHangerPointKind pointKind,
            string application,
            string specKey,
            double cableDropMm)
        {
            var btr = (BlockTableRecord)tr.GetObject(blockDefId, OpenMode.ForRead);
            foreach (ObjectId entityId in btr)
            {
                if (tr.GetObject(entityId, OpenMode.ForRead) is not AttributeDefinition attributeDefinition
                    || attributeDefinition.Constant)
                {
                    continue;
                }

                var attributeReference = new AttributeReference();
                attributeReference.SetAttributeFromBlock(attributeDefinition, blockReference.BlockTransform);
                attributeReference.Position = attributeDefinition.Position.TransformBy(blockReference.BlockTransform);
                
                string text = attributeDefinition.Tag.ToUpperInvariant() switch
                {
                    "APP" => application ?? string.Empty,
                    "SPEC" => specKey ?? string.Empty,
                    "HANGER_KIND" => pointKind.ToString(),
                    "CABLE_DROP" => cableDropMm.ToString("F0"),
                    _ => string.Empty
                };

                attributeReference.TextString = text;
                attributeReference.Layer = blockReference.Layer;
                attributeReference.Invisible = attributeDefinition.Invisible;

                blockReference.AttributeCollection.AppendAttribute(attributeReference);
                tr.AddNewlyCreatedDBObject(attributeReference, true);
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
                ObjectId existingId = bt[blockName];
                EnsureAttributesExist(tr, existingId, pointKind);
                return existingId;
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

            AddAttribute(marker, tr, "APP", "APP", string.Empty, new Point3d(0, 0, 0), true);
            AddAttribute(marker, tr, "SPEC", "SPEC", string.Empty, new Point3d(0, 0, 0), true);
            AddAttribute(marker, tr, "HANGER_KIND", "HANGER_KIND", pointKind.ToString(), new Point3d(0, 0, 0), true);
            AddAttribute(marker, tr, "CABLE_DROP", "CABLE_DROP", "1500", new Point3d(0, 0, 0), true);

            return markerId;
        }

        private static void EnsureAttributesExist(Transaction tr, ObjectId blockDefId, CeilingHangerPointKind pointKind)
        {
            var btr = (BlockTableRecord)tr.GetObject(blockDefId, OpenMode.ForRead);
            bool hasApp = false;
            bool hasSpec = false;
            bool hasHangerKind = false;
            bool hasCableDrop = false;

            foreach (ObjectId entityId in btr)
            {
                if (tr.GetObject(entityId, OpenMode.ForRead) is AttributeDefinition attDef)
                {
                    if (attDef.Tag.Equals("APP", StringComparison.OrdinalIgnoreCase)) hasApp = true;
                    if (attDef.Tag.Equals("SPEC", StringComparison.OrdinalIgnoreCase)) hasSpec = true;
                    if (attDef.Tag.Equals("HANGER_KIND", StringComparison.OrdinalIgnoreCase)) hasHangerKind = true;
                    if (attDef.Tag.Equals("CABLE_DROP", StringComparison.OrdinalIgnoreCase)) hasCableDrop = true;
                }
            }

            if (!hasApp || !hasSpec || !hasHangerKind || !hasCableDrop)
            {
                btr.UpgradeOpen();
                if (!hasApp) AddAttribute(btr, tr, "APP", "APP", string.Empty, new Point3d(0, 0, 0), true);
                if (!hasSpec) AddAttribute(btr, tr, "SPEC", "SPEC", string.Empty, new Point3d(0, 0, 0), true);
                if (!hasHangerKind) AddAttribute(btr, tr, "HANGER_KIND", "HANGER_KIND", pointKind.ToString(), new Point3d(0, 0, 0), true);
                if (!hasCableDrop) AddAttribute(btr, tr, "CABLE_DROP", "CABLE_DROP", "1500", new Point3d(0, 0, 0), true);
            }
        }

        private static void AddAttribute(
            BlockTableRecord marker,
            Transaction tr,
            string tag,
            string prompt,
            string defaultValue,
            Point3d position,
            bool invisible)
        {
            var attr = new AttributeDefinition
            {
                Position = position,
                Tag = tag,
                Prompt = prompt,
                TextString = defaultValue,
                Height = 1.0,
                Justify = AttachmentPoint.MiddleCenter,
                AlignmentPoint = position,
                Invisible = invisible,
                Layer = "0"
            };

            marker.AppendEntity(attr);
            tr.AddNewlyCreatedDBObject(attr, true);
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
