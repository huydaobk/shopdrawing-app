using Autodesk.AutoCAD.DatabaseServices;
using ShopDrawing.Plugin.Runtime;

namespace ShopDrawing.Plugin.Modules.Accessories
{
    internal sealed class ShopdrawingAccessoryScanner
    {
        private class CeilingSnapshotBuilder
        {
            public double TLineLengthM { get; set; }
            public double MushroomLineLengthM { get; set; }
            public int MushroomBoltCount { get; set; }
            public int THangerPointCount { get; set; }
            public int MushroomHangerPointCount { get; set; }
            public double TotalTCableDropM { get; set; }
            public double TotalMushroomCableDropM { get; set; }
        }

        public System.Collections.Generic.IReadOnlyList<ShopdrawingAccessorySnapshot> ScanCeiling(Transaction tr, Database db, ShopDrawingRuntimeSettings settings)
        {
            if (tr.GetObject(db.BlockTableId, OpenMode.ForRead) is not BlockTable bt
                || tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) is not BlockTableRecord ms)
            {
                return new System.Collections.Generic.List<ShopdrawingAccessorySnapshot>();
            }

            var builders = new System.Collections.Generic.Dictionary<(string App, string Spec), CeilingSnapshotBuilder>();

            CeilingSnapshotBuilder GetBuilder((string App, string Spec) key)
            {
                if (!builders.TryGetValue(key, out var builder))
                {
                    builder = new CeilingSnapshotBuilder();
                    builders[key] = builder;
                }
                return builder;
            }

            foreach (ObjectId id in ms)
            {
                if (id.IsErased) continue;

                if (tr.GetObject(id, OpenMode.ForRead) is not Entity entity)
                {
                    continue;
                }

                var key = GetEntityScope(tr, entity, settings);
                var builder = GetBuilder(key);

                switch (entity.Layer)
                {
                    case "SD_CEILING_T":
                        builder.TLineLengthM += GetEntityLengthM(entity);
                        break;
                    case "SD_CEILING_MUSHROOM":
                        builder.MushroomLineLengthM += GetEntityLengthM(entity);
                        break;
                    case "SD_CEILING_BOLT":
                        builder.MushroomBoltCount += CountPointEntity(entity);
                        break;
                    case "SD_CEILING_T_HANGER":
                        if (CountPointEntity(entity) > 0)
                        {
                            builder.THangerPointCount++;
                            builder.TotalTCableDropM += GetCableDropM(tr, entity, settings);
                        }
                        break;
                    case "SD_CEILING_MUSHROOM_HANGER":
                        if (CountPointEntity(entity) > 0)
                        {
                            builder.MushroomHangerPointCount++;
                            builder.TotalMushroomCableDropM += GetCableDropM(tr, entity, settings);
                        }
                        break;
                }
            }

            var results = new System.Collections.Generic.List<ShopdrawingAccessorySnapshot>();
            foreach (var kvp in builders)
            {
                // Only include if there is actual data
                if (kvp.Value.TLineLengthM > 0 || kvp.Value.MushroomLineLengthM > 0 || kvp.Value.MushroomBoltCount > 0 || kvp.Value.THangerPointCount > 0 || kvp.Value.MushroomHangerPointCount > 0)
                {
                    results.Add(new ShopdrawingAccessorySnapshot(
                        kvp.Key.Spec,
                        kvp.Key.App,
                        kvp.Value.TLineLengthM,
                        kvp.Value.MushroomLineLengthM,
                        kvp.Value.MushroomBoltCount,
                        kvp.Value.THangerPointCount,
                        kvp.Value.MushroomHangerPointCount,
                        kvp.Value.TotalTCableDropM,
                        kvp.Value.TotalMushroomCableDropM));
                }
            }

            return results;
        }

        private static (string App, string Spec) GetEntityScope(Transaction tr, Entity entity, ShopDrawingRuntimeSettings settings)
        {
            string app = settings.DefaultApplication;
            string spec = settings.DefaultSpec;

            if (entity is BlockReference br)
            {
                foreach (ObjectId attId in br.AttributeCollection)
                {
                    if (attId.IsErased) continue;
                    if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference att)
                    {
                        if (att.Tag.Equals("APP", System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(att.TextString)) app = att.TextString;
                        }
                        else if (att.Tag.Equals("SPEC", System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(att.TextString)) spec = att.TextString;
                        }
                    }
                }
            }

            return (app, spec);
        }

        private static double GetCableDropM(Transaction tr, Entity entity, ShopDrawingRuntimeSettings settings)
        {
            if (entity is BlockReference br)
            {
                foreach (ObjectId attId in br.AttributeCollection)
                {
                    if (attId.IsErased) continue;
                    if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference att)
                    {
                        if (att.Tag.Equals("CABLE_DROP", System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (double.TryParse(att.TextString, out double dropMm))
                            {
                                return dropMm / 1000.0;
                            }
                        }
                    }
                }
            }
            return settings.DefaultCeilingCableDropMm / 1000.0;
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
