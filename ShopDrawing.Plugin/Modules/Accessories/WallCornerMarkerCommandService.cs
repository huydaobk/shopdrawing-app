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
    internal sealed class WallCornerMarkerCommandService
    {
        internal const string OutsideCornerLayerName = "SD_WALL_OUTSIDE_CORNER";
        internal const string InsideCornerLayerName = "SD_WALL_INSIDE_CORNER";

        private const string OutsideCornerBlockName = "SD_WALL_OUTSIDE_CORNER_MARK";
        private const string InsideCornerBlockName = "SD_WALL_INSIDE_CORNER_MARK";

        public void Run(BlockManager blockManager, ShopDrawingRuntimeSettings settings, WallCornerMarkerKind kind)
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
                    ObjectId blockId = EnsureMarkerBlock(doc.Database, tr, kind);
                    double scale = Math.Max(40.0, GetDrawingScale(doc.Database));

                    var dialog = new ShopDrawing.Plugin.UI.CornerApplicationSelectionDialog();
                    bool? dialogResult = Application.ShowModalWindow(dialog);
                    if (dialogResult != true)
                    {
                        return;
                    }

                    string selectedApp = dialog.SelectedApplication;

                    while (true)
                    {
                        PromptPointResult pointResult = ed.GetPoint(new PromptPointOptions(
                            insertedCount == 0
                                ? $"\nChọn vị trí marker {GetDisplayLabel(kind)} trên mặt bằng (Enter để kết thúc): "
                                : $"\nChọn tiếp marker {GetDisplayLabel(kind)} (Enter để kết thúc): ")
                        {
                            AllowNone = true
                        });

                        if (pointResult.Status == PromptStatus.None)
                        {
                            break;
                        }

                        if (pointResult.Status != PromptStatus.OK)
                        {
                            return;
                        }

                        double heightMm = 3000.0; // Default height internally, or whatever

                        InsertMarker(
                            doc.Database,
                            tr,
                            ms,
                            blockId,
                            pointResult.Value,
                            scale,
                            kind,
                            heightMm,
                            selectedApp,
                            settings.DefaultSpec);

                        insertedCount++;
                    }

                    tr.Commit();
                }

                ed.WriteMessage($"\nĐã đặt {insertedCount} marker {GetDisplayLabel(kind)}.");

                if (insertedCount > 0)
                {
                    settings.NotifyWasteUpdated();
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nLỗi pick marker góc: {ex.Message}");
            }
        }

        private static void InsertMarker(
            Database db,
            Transaction tr,
            BlockTableRecord ms,
            ObjectId blockId,
            Point3d position,
            double scale,
            WallCornerMarkerKind kind,
            double heightMm,
            string application,
            string specKey)
        {
            var blockReference = new BlockReference(position, blockId)
            {
                Layer = GetLayerName(kind),
                ScaleFactors = new Scale3d(scale, scale, 1.0)
            };

            if (application == AccessoryDataManager.AppCleanroom)
            {
                blockReference.Color = Color.FromColorIndex(ColorMethod.ByAci, 3); // Green
            }
            else if (application == AccessoryDataManager.AppColdStorage)
            {
                blockReference.Color = Color.FromColorIndex(ColorMethod.ByAci, 4); // Cyan
            }

            ms.AppendEntity(blockReference);
            tr.AddNewlyCreatedDBObject(blockReference, true);

            BlockTableRecord blockDefinition = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);
            foreach (ObjectId entityId in blockDefinition)
            {
                if (tr.GetObject(entityId, OpenMode.ForRead) is not AttributeDefinition attributeDefinition
                    || attributeDefinition.Constant)
                {
                    continue;
                }

                var attributeReference = new AttributeReference();
                attributeReference.SetAttributeFromBlock(attributeDefinition, blockReference.BlockTransform);
                attributeReference.Position = attributeDefinition.Position.TransformBy(blockReference.BlockTransform);
                attributeReference.TextString = ResolveAttributeValue(attributeDefinition.Tag, kind, heightMm, application, specKey);
                attributeReference.Layer = blockReference.Layer;
                attributeReference.Invisible = attributeDefinition.Invisible;

                blockReference.AttributeCollection.AppendAttribute(attributeReference);
                tr.AddNewlyCreatedDBObject(attributeReference, true);
            }
        }

        private static string ResolveAttributeValue(string tag, WallCornerMarkerKind kind, double heightMm, string application, string specKey)
        {
            return tag.ToUpperInvariant() switch
            {
                "ITEM" => GetDisplayLabel(kind).ToUpperInvariant(),
                "HEIGHT" => "",
                "HEIGHT_MM" => $"{heightMm:F0}",
                "APP" => application ?? string.Empty,
                "SPEC" => specKey ?? string.Empty,
                "CORNER_KIND" => kind.ToString(),
                _ => string.Empty
            };
        }

        private static string GetLayerName(WallCornerMarkerKind kind)
            => kind == WallCornerMarkerKind.Outside ? OutsideCornerLayerName : InsideCornerLayerName;

        private static string GetBlockName(WallCornerMarkerKind kind)
            => kind == WallCornerMarkerKind.Outside ? OutsideCornerBlockName : InsideCornerBlockName;

        private static string GetDisplayLabel(WallCornerMarkerKind kind)
            => "góc";

        private static ObjectId EnsureMarkerBlock(Database db, Transaction tr, WallCornerMarkerKind kind)
        {
            string blockName = GetBlockName(kind);
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (bt.Has(blockName))
            {
                ObjectId existingId = bt[blockName];
                EnsureAttributesExist(tr, existingId, kind);
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

            AddLine(marker, tr, new Point3d(-1.8, 0, 0), new Point3d(1.8, 0, 0));
            AddLine(marker, tr, new Point3d(0, -1.8, 0), new Point3d(0, 1.8, 0));

            if (kind == WallCornerMarkerKind.Outside)
            {
                AddLine(marker, tr, new Point3d(-1.4, -1.4, 0), new Point3d(1.4, 1.4, 0));
            }
            else
            {
                AddLine(marker, tr, new Point3d(-1.4, 1.4, 0), new Point3d(1.4, -1.4, 0));
            }

            AddAttribute(marker, tr, "ITEM", "ITEM", GetDisplayLabel(kind).ToUpperInvariant(), new Point3d(2.4, 0.9, 0), false);
            AddAttribute(marker, tr, "HEIGHT", "HEIGHT", "H=3000", new Point3d(2.4, -0.9, 0), false);
            AddAttribute(marker, tr, "HEIGHT_MM", "HEIGHT_MM", "3000", new Point3d(0, 0, 0), true);
            AddAttribute(marker, tr, "APP", "APP", string.Empty, new Point3d(0, 0, 0), true);
            AddAttribute(marker, tr, "SPEC", "SPEC", string.Empty, new Point3d(0, 0, 0), true);
            AddAttribute(marker, tr, "CORNER_KIND", "CORNER_KIND", kind.ToString(), new Point3d(0, 0, 0), true);

            return markerId;
        }

        private static void EnsureAttributesExist(Transaction tr, ObjectId blockDefId, WallCornerMarkerKind kind)
        {
            var btr = (BlockTableRecord)tr.GetObject(blockDefId, OpenMode.ForRead);
            bool hasApp = false;
            bool hasSpec = false;
            bool hasHeightMm = false;
            bool hasCornerKind = false;

            foreach (ObjectId entityId in btr)
            {
                if (tr.GetObject(entityId, OpenMode.ForRead) is AttributeDefinition attDef)
                {
                    if (attDef.Tag.Equals("APP", StringComparison.OrdinalIgnoreCase)) hasApp = true;
                    if (attDef.Tag.Equals("SPEC", StringComparison.OrdinalIgnoreCase)) hasSpec = true;
                    if (attDef.Tag.Equals("HEIGHT_MM", StringComparison.OrdinalIgnoreCase)) hasHeightMm = true;
                    if (attDef.Tag.Equals("CORNER_KIND", StringComparison.OrdinalIgnoreCase)) hasCornerKind = true;
                }
            }

            if (!hasApp || !hasSpec || !hasHeightMm || !hasCornerKind)
            {
                btr.UpgradeOpen();
                if (!hasHeightMm) AddAttribute(btr, tr, "HEIGHT_MM", "HEIGHT_MM", "3000", new Point3d(0, 0, 0), true);
                if (!hasApp) AddAttribute(btr, tr, "APP", "APP", string.Empty, new Point3d(0, 0, 0), true);
                if (!hasSpec) AddAttribute(btr, tr, "SPEC", "SPEC", string.Empty, new Point3d(0, 0, 0), true);
                if (!hasCornerKind) AddAttribute(btr, tr, "CORNER_KIND", "CORNER_KIND", kind.ToString(), new Point3d(0, 0, 0), true);
            }
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

        private static void AddAttribute(
            BlockTableRecord marker,
            Transaction tr,
            string tag,
            string prompt,
            string defaultValue,
            Point3d position,
            bool invisible)
        {
            var attributeDefinition = new AttributeDefinition
            {
                Tag = tag,
                Prompt = prompt,
                TextString = defaultValue,
                Position = position,
                Height = invisible ? 0.8 : 1.3,
                Invisible = invisible,
                Layer = "0",
                Color = Color.FromColorIndex(ColorMethod.ByBlock, 0)
            };

            marker.AppendEntity(attributeDefinition);
            tr.AddNewlyCreatedDBObject(attributeDefinition, true);
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
