using Autodesk.AutoCAD.DatabaseServices;
using ShopDrawing.Plugin.Runtime;

namespace ShopDrawing.Plugin.Modules.Accessories
{
    internal sealed class ShopdrawingAccessoryScanner
    {
        public ShopdrawingAccessorySnapshot ScanCeiling(Transaction tr, Database db, ShopDrawingRuntimeSettings settings)
        {
            if (tr.GetObject(db.BlockTableId, OpenMode.ForRead) is not BlockTable bt
                || tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) is not BlockTableRecord ms)
            {
                return new ShopdrawingAccessorySnapshot(
                    settings.DefaultSpec,
                    settings.DefaultApplication,
                    0,
                    0,
                    0,
                    0,
                    0,
                    settings.DefaultCeilingCableDropMm / 1000.0);
            }

            double tLineLengthM = 0;
            double mushroomLineLengthM = 0;
            int mushroomBoltCount = 0;
            int tHangerPointCount = 0;
            int mushroomHangerPointCount = 0;

            foreach (ObjectId id in ms)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity entity)
                {
                    continue;
                }

                switch (entity.Layer)
                {
                    case "SD_CEILING_T":
                        tLineLengthM += GetEntityLengthM(entity);
                        break;
                    case "SD_CEILING_MUSHROOM":
                        mushroomLineLengthM += GetEntityLengthM(entity);
                        break;
                    case "SD_CEILING_BOLT":
                        mushroomBoltCount += CountPointEntity(entity);
                        break;
                    case "SD_CEILING_T_HANGER":
                        tHangerPointCount += CountPointEntity(entity);
                        break;
                    case "SD_CEILING_MUSHROOM_HANGER":
                        mushroomHangerPointCount += CountPointEntity(entity);
                        break;
                }
            }

            return new ShopdrawingAccessorySnapshot(
                settings.DefaultSpec,
                settings.DefaultApplication,
                tLineLengthM,
                mushroomLineLengthM,
                mushroomBoltCount,
                tHangerPointCount,
                mushroomHangerPointCount,
                settings.DefaultCeilingCableDropMm / 1000.0);
        }

        private static double GetEntityLengthM(Entity entity)
        {
            if (entity is Curve curve)
            {
                return curve.GetDistanceAtParameter(curve.EndParam) / 1000.0;
            }

            return 0;
        }

        private static int CountPointEntity(Entity entity)
        {
            return entity is BlockReference or DBPoint ? 1 : 0;
        }
    }
}
