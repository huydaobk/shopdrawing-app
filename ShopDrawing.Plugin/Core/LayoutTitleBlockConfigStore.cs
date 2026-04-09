using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace ShopDrawing.Plugin.Core
{
    public static class LayoutTitleBlockConfigStore
    {
        private const string NOD_KEY = "SD_LAYOUT_TITLEBLOCK_CFG";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public static LayoutTitleBlockConfig Load(Document doc)
        {
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();
            var config = Load(db, tr);
            tr.Commit();
            return config;
        }

        public static void Save(Document doc, LayoutTitleBlockConfig config)
        {
            var db = doc.Database;
            using var docLock = doc.LockDocument();
            using var tr = db.TransactionManager.StartTransaction();
            Save(db, tr, config);
            tr.Commit();
        }

        public static IReadOnlyList<LayoutTitleBlockSource> DiscoverSources(string dwgPath)
        {
            if (!File.Exists(dwgPath))
                return [];

            using var db = new Database(false, true);
            db.ReadDwgFile(dwgPath, FileOpenMode.OpenForReadAndAllShare, true, null);
            db.CloseInput(true);

            using var tr = db.TransactionManager.StartTransaction();
            var sources = new List<LayoutTitleBlockSource>();

            var modelSpace = (BlockTableRecord)tr.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            var modelTags = GetAttributeTags(modelSpace, tr);
            if (modelTags.Count > 0)
            {
                var (mw, mh) = MeasureBlockBounds(modelSpace, tr);
                var (mPos, mSize) = DetectStripLayout(modelSpace, tr, mw, mh);
                sources.Add(new LayoutTitleBlockSource
                {
                    Name = LayoutTitleBlockConfig.ModelSpaceSourceName,
                    AttributeTags = modelTags,
                    Width = mw,
                    Height = mh,
                    StripPosition = mPos,
                    StripSize = mSize
                });
            }

            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId blockId in blockTable)
            {
                var block = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);
                if (block.IsAnonymous || block.IsLayout || block.IsFromExternalReference)
                    continue;

                var tags = GetAttributeTags(block, tr);
                if (tags.Count == 0)
                    continue;

                var (bw, bh) = MeasureBlockBounds(block, tr);
                var (bPos, bSize) = DetectStripLayout(block, tr, bw, bh);
                sources.Add(new LayoutTitleBlockSource
                {
                    Name = block.Name,
                    AttributeTags = tags,
                    Width = bw,
                    Height = bh,
                    StripPosition = bPos,
                    StripSize = bSize
                });
            }

            tr.Commit();
            return sources
                .OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool TryInsertTitleBlock(
            BlockTableRecord paperSpace,
            Transaction tr,
            LayoutTitleBlockConfig config,
            Point3d position,
            LayoutTitleBlockValueSet values,
            out string warning)
        {
            warning = string.Empty;

            if (config.Mode != LayoutTitleBlockMode.External || !config.IsConfiguredForExternal)
            {
                warning = "External title block chua duoc cau hinh.";
                return false;
            }

            if (!File.Exists(config.DwgPath))
            {
                warning = "Khong tim thay file DWG external.";
                return false;
            }

            try
            {
                var db = paperSpace.Database;
                ObjectId blockDefinitionId = ImportBlockDefinition(db, config);
                if (blockDefinitionId.IsNull)
                {
                    warning = "Khong import duoc block dinh nghia.";
                    return false;
                }

                var blockDefinition = (BlockTableRecord)tr.GetObject(blockDefinitionId, OpenMode.ForRead);

                // Remove any pre-existing title block references in this paper space
                // that use the same block definition — they carry stale attribute text values
                // from previous insertions (e.g., "SD-W4" before project name was configured).
                var staleRefs = paperSpace
                    .Cast<ObjectId>()
                    .Where(id =>
                    {
                        try
                        {
                            var e = tr.GetObject(id, OpenMode.ForRead);
                            return e is BlockReference br && br.BlockTableRecord == blockDefinitionId;
                        }
                        catch (System.Exception ex) { ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message);  return false; }
                    })
                    .ToList();

                foreach (var staleId in staleRefs)
                {
                    try
                    {
                        var staleRef = (BlockReference)tr.GetObject(staleId, OpenMode.ForWrite);
                        foreach (ObjectId attrId in staleRef.AttributeCollection)
                        {
                            var attrRef = tr.GetObject(attrId, OpenMode.ForWrite);
                            if (!attrRef.IsErased) attrRef.Erase();
                        }
                        staleRef.Erase();
                    }
                    catch (System.Exception ex) { ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message);  /* ignore individual erase failures */ }
                }

                var blockReference = new BlockReference(position, blockDefinitionId)
                {
                    Layer = LayoutManagerEngine.TITLE_LAYER
                };

                paperSpace.AppendEntity(blockReference);
                tr.AddNewlyCreatedDBObject(blockReference, true);

                bool hasAttributes = false;
                foreach (ObjectId entityId in blockDefinition)
                {
                    if (tr.GetObject(entityId, OpenMode.ForRead) is not AttributeDefinition attributeDefinition
                        || attributeDefinition.Constant)
                    {
                        continue;
                    }

                    hasAttributes = true;

                    var attributeReference = new AttributeReference();
                    attributeReference.SetAttributeFromBlock(attributeDefinition, blockReference.BlockTransform);
                    attributeReference.Position =
                        attributeDefinition.Position.TransformBy(blockReference.BlockTransform);
                    attributeReference.TextString = ResolveAttributeText(attributeDefinition, config, values);
                    attributeReference.Layer = LayoutManagerEngine.TITLE_LAYER;

                    blockReference.AttributeCollection.AppendAttribute(attributeReference);
                    tr.AddNewlyCreatedDBObject(attributeReference, true);
                }

                if (!hasAttributes)
                {
                    blockReference.Erase();
                    warning = "Block external khong co attribute kha dung.";
                    return false;
                }

                return true;
            }
            catch (Exception ex) { ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message); 
                warning = ex.Message;
                return false;
            }
        }

        private static LayoutTitleBlockConfig Load(Database db, Transaction tr)
        {
            try
            {
                var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (!nod.Contains(NOD_KEY))
                    return new LayoutTitleBlockConfig();

                var xrecord = (Xrecord)tr.GetObject(nod.GetAt(NOD_KEY), OpenMode.ForRead);
                string json = xrecord.Data?.AsArray()
                    .Select(value => value.Value?.ToString())
                    .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
                    ?? string.Empty;

                return JsonSerializer.Deserialize<LayoutTitleBlockConfig>(json, JsonOptions)
                    ?? new LayoutTitleBlockConfig();
            }
            catch (System.Exception ex) { ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message); 
                return new LayoutTitleBlockConfig();
            }
        }

        private static void Save(Database db, Transaction tr, LayoutTitleBlockConfig config)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            var xrecord = new Xrecord
            {
                Data = new ResultBuffer(new TypedValue(1, json))
            };

            if (nod.Contains(NOD_KEY))
            {
                var existing = (DBObject)tr.GetObject(nod.GetAt(NOD_KEY), OpenMode.ForWrite);
                existing.Erase();
                nod.Remove(NOD_KEY);
            }

            nod.SetAt(NOD_KEY, xrecord);
            tr.AddNewlyCreatedDBObject(xrecord, true);
        }

        private static List<string> GetAttributeTags(BlockTableRecord source, Transaction tr)
        {
            return source
                .Cast<ObjectId>()
                .Select(id => tr.GetObject(id, OpenMode.ForRead))
                .OfType<AttributeDefinition>()
                .Where(attribute => !attribute.Constant && !string.IsNullOrWhiteSpace(attribute.Tag))
                .Select(attribute => attribute.Tag.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static (double Width, double Height) MeasureBlockBounds(BlockTableRecord block, Transaction tr)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            bool hasExtents = false;

            foreach (ObjectId entityId in block)
            {
                try
                {
                    var entity = (Entity)tr.GetObject(entityId, OpenMode.ForRead);
                    var ext = entity.GeometricExtents;
                    minX = Math.Min(minX, ext.MinPoint.X);
                    minY = Math.Min(minY, ext.MinPoint.Y);
                    maxX = Math.Max(maxX, ext.MaxPoint.X);
                    maxY = Math.Max(maxY, ext.MaxPoint.Y);
                    hasExtents = true;
                }
                catch (System.Exception ex) { ShopDrawing.Plugin.Core.PluginLogger.Warn("Suppressed exception: " + ex.Message);  /* Some entities don't support GeometricExtents */ }
            }

            if (!hasExtents) return (0, 0);
            return (maxX - minX, maxY - minY);
        }

        /// <summary>
        /// Analyzes a block to detect if the info strip is at Bottom or Right.
        ///
        /// Phase 1 (most reliable): Find a long dividing LINE running vertically or horizontally
        ///   through the block interior. A right strip has a tall vertical line near the right edge.
        ///   A bottom strip has a wide horizontal line near the bottom edge.
        /// Phase 2 (fallback): Use attribute-position centroids.
        /// </summary>
        private static (TitleBlockStripPosition Position, double StripSize) DetectStripLayout(
            BlockTableRecord block, Transaction tr, double blockWidth, double blockHeight)
        {
            if (blockWidth <= 0 || blockHeight <= 0)
                return (TitleBlockStripPosition.Bottom, 0);

            // ─────────────────────────────────────────────────────────────────
            // Collect all entities, tracking block bounds and line candidates
            // ─────────────────────────────────────────────────────────────────
            double minBX = double.MaxValue, minBY = double.MaxValue;
            double maxBX = double.MinValue, maxBY = double.MinValue;
            var attrPositions = new List<(double X, double Y)>();
            var verticalLines  = new List<(double X, double Length)>();  // vertical dividers
            var horizontalLines = new List<(double Y, double Length)>(); // horizontal dividers

            foreach (ObjectId entityId in block)
            {
                try
                {
                    var entity = tr.GetObject(entityId, OpenMode.ForRead);

                    // Track block bounds
                    if (entity is Entity ent)
                    {
                        var ext = ent.GeometricExtents;
                        minBX = Math.Min(minBX, ext.MinPoint.X);
                        minBY = Math.Min(minBY, ext.MinPoint.Y);
                        maxBX = Math.Max(maxBX, ext.MaxPoint.X);
                        maxBY = Math.Max(maxBY, ext.MaxPoint.Y);
                    }

                    // Collect attribute positions
                    if (entity is AttributeDefinition attrDef && !attrDef.Constant)
                        attrPositions.Add((attrDef.Position.X, attrDef.Position.Y));

                    // Collect LINE segments as potential dividers
                    if (entity is Line line)
                    {
                        double dx = Math.Abs(line.EndPoint.X - line.StartPoint.X);
                        double dy = Math.Abs(line.EndPoint.Y - line.StartPoint.Y);
                        double len = line.Length;

                        // Vertical line: dx tiny, dy large
                        if (dy > 0 && dx / dy < 0.05 && len > blockHeight * 0.30)
                            verticalLines.Add((Math.Min(line.StartPoint.X, line.EndPoint.X), len));

                        // Horizontal line: dy tiny, dx large
                        if (dx > 0 && dy / dx < 0.05 && len > blockWidth * 0.30)
                            horizontalLines.Add((Math.Min(line.StartPoint.Y, line.EndPoint.Y), len));
                    }
                    else if (entity is Polyline poly)
                    {
                        // Check each segment of a polyline
                        for (int i = 0; i < poly.NumberOfVertices - 1; i++)
                        {
                            var s = poly.GetPoint3dAt(i);
                            var e = poly.GetPoint3dAt(i + 1);
                            double dx = Math.Abs(e.X - s.X);
                            double dy = Math.Abs(e.Y - s.Y);
                            double len = s.DistanceTo(e);

                            if (dy > 0 && dx / dy < 0.05 && len > blockHeight * 0.30)
                                verticalLines.Add((Math.Min(s.X, e.X), len));
                            if (dx > 0 && dy / dx < 0.05 && len > blockWidth * 0.30)
                                horizontalLines.Add((Math.Min(s.Y, e.Y), len));
                        }
                    }
                }
                catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in LayoutTitleBlockConfigStore.cs", ex);
            }
            }

            // ─────────────────────────────────────────────────────────────────
            // PHASE 1: Geometry-based — find the INNER dividing line
            //   For a right strip: there should be a tall vertical line at x ≈ (blockRight - stripWidth)
            //   For a bottom strip: there should be a wide horizontal line at y ≈ stripHeight
            // ─────────────────────────────────────────────────────────────────
            if (maxBX > minBX && maxBY > minBY)
            {
                double bw = maxBX - minBX;
                double bh = maxBY - minBY;

                // Find tallest vertical line in the right 60% of the block (= right strip divider)
                var rightDividers = verticalLines
                    .Where(l => (l.X - minBX) / bw > 0.35)   // in right 65% of block
                    .OrderByDescending(l => l.Length)
                    .ToList();

                // Find lowest horizontal line in the bottom 50% of the block (= bottom strip divider)
                var bottomDividers = horizontalLines
                    .Where(l => (l.Y - minBY) / bh < 0.55)   // in bottom 55% of block
                    .OrderByDescending(l => l.Length)
                    .ToList();

                bool hasRightDivider  = rightDividers.Count > 0 && rightDividers[0].Length > bh * 0.55;
                bool hasBottomDivider = bottomDividers.Count > 0 && bottomDividers[0].Length > bw * 0.55;

                System.Diagnostics.Debug.WriteLine(
                    $"[SD][DetectStrip] RightDividers={rightDividers.Count} (best={rightDividers.FirstOrDefault().Length:F0}mm), " +
                    $"BottomDividers={bottomDividers.Count} (best={bottomDividers.FirstOrDefault().Length:F0}mm)");

                if (hasRightDivider && !hasBottomDivider)
                {
                    double dividerRelX = (rightDividers[0].X - minBX) / bw;
                    double stripWidth = bw * (1.0 - dividerRelX);
                    System.Diagnostics.Debug.WriteLine($"[SD][DetectStrip] → RIGHT (line), stripWidth={stripWidth:F1}");
                    return (TitleBlockStripPosition.Right, Math.Max(stripWidth, 20));
                }

                if (hasBottomDivider && !hasRightDivider)
                {
                    double dividerRelY = (bottomDividers[0].Y - minBY) / bh;
                    double stripHeight = bh * (dividerRelY + 0.05); // include the line itself
                    System.Diagnostics.Debug.WriteLine($"[SD][DetectStrip] → BOTTOM (line), stripHeight={stripHeight:F1}");
                    return (TitleBlockStripPosition.Bottom, Math.Max(stripHeight, 20));
                }

                if (hasRightDivider && hasBottomDivider)
                {
                    // Both found — prefer whichever divider is longer relative to its span
                    double rightScore  = rightDividers[0].Length / bh;
                    double bottomScore = bottomDividers[0].Length / bw;
                    if (rightScore >= bottomScore)
                    {
                        double dividerRelX = (rightDividers[0].X - minBX) / bw;
                        double stripWidth = bw * (1.0 - dividerRelX);
                        System.Diagnostics.Debug.WriteLine($"[SD][DetectStrip] → RIGHT (both, score r={rightScore:F2} b={bottomScore:F2})");
                        return (TitleBlockStripPosition.Right, Math.Max(stripWidth, 20));
                    }
                    else
                    {
                        double dividerRelY = (bottomDividers[0].Y - minBY) / bh;
                        double stripHeight = bh * (dividerRelY + 0.05);
                        System.Diagnostics.Debug.WriteLine($"[SD][DetectStrip] → BOTTOM (both, score r={rightScore:F2} b={bottomScore:F2})");
                        return (TitleBlockStripPosition.Bottom, Math.Max(stripHeight, 20));
                    }
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // PHASE 2: Attribute-position fallback
            // ─────────────────────────────────────────────────────────────────
            if (attrPositions.Count > 0)
            {
                var norm = attrPositions
                    .Select(p => ((p.X - minBX) / blockWidth, (p.Y - minBY) / blockHeight))
                    .ToList();

                double minRelX = norm.Min(p => p.Item1);
                double maxRelX = norm.Max(p => p.Item1);
                double maxRelY = norm.Max(p => p.Item2);

                System.Diagnostics.Debug.WriteLine(
                    $"[SD][DetectStrip] Phase2 attr minRelX={minRelX:F2}, maxRelY={maxRelY:F2}");

                // If all attrs are in the right 45% → right strip
                if (minRelX > 0.55)
                {
                    double stripWidth = blockWidth * (1.0 - minRelX);
                    return (TitleBlockStripPosition.Right, Math.Max(stripWidth, 20));
                }

                // Attrs span the lower portion and wide width → bottom strip
                if (maxRelY < 0.45 && (maxRelX - minRelX) > 0.30)
                {
                    double stripHeight = blockHeight * maxRelY;
                    return (TitleBlockStripPosition.Bottom, Math.Max(stripHeight, 20));
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // PHASE 3: Simple heuristic — aspect ratio can't tell us, return 0
            // ─────────────────────────────────────────────────────────────────
            return (TitleBlockStripPosition.Bottom, 0);
        }


        private static ObjectId ImportBlockDefinition(Database destinationDb, LayoutTitleBlockConfig config)
        {
            string importedName = BuildImportedBlockName(config);

            // Always erase the cached block to ensure a fresh import with correct attribute definitions.
            // Keeping a cached block would preserve stale attribute default texts from previous insertions.
            using (var destinationTransaction = destinationDb.TransactionManager.StartTransaction())
            {
                var destinationBlocks =
                    (BlockTable)destinationTransaction.GetObject(destinationDb.BlockTableId, OpenMode.ForWrite);
                if (destinationBlocks.Has(importedName))
                {
                    var staleBlock = (BlockTableRecord)destinationTransaction.GetObject(
                        destinationBlocks[importedName], OpenMode.ForWrite);
                    // Erase all entities in the stale block definition
                    foreach (ObjectId entityId in staleBlock)
                    {
                        var entity = destinationTransaction.GetObject(entityId, OpenMode.ForWrite);
                        if (!entity.IsErased) entity.Erase();
                    }
                    staleBlock.Erase();
                }

                destinationTransaction.Commit();
            }

            using var sourceDb = new Database(false, true);
            sourceDb.ReadDwgFile(config.DwgPath, FileOpenMode.OpenForReadAndAllShare, true, null);
            sourceDb.CloseInput(true);

            if (string.Equals(config.SourceName, LayoutTitleBlockConfig.ModelSpaceSourceName, StringComparison.Ordinal))
            {
                return destinationDb.Insert(importedName, sourceDb, true);
            }

            using (var sourceTransaction = sourceDb.TransactionManager.StartTransaction())
            {
                var sourceBlocks = (BlockTable)sourceTransaction.GetObject(sourceDb.BlockTableId, OpenMode.ForRead);
                if (!sourceBlocks.Has(config.SourceName))
                    throw new InvalidOperationException("Khong tim thay block source da chon.");

                var sourceBlock =
                    (BlockTableRecord)sourceTransaction.GetObject(sourceBlocks[config.SourceName], OpenMode.ForWrite);
                if (!string.Equals(sourceBlock.Name, importedName, StringComparison.OrdinalIgnoreCase))
                {
                    sourceBlock.Name = importedName;
                }

                var ids = new ObjectIdCollection { sourceBlock.ObjectId };
                var mapping = new IdMapping();
                sourceDb.WblockCloneObjects(
                    ids,
                    destinationDb.BlockTableId,
                    mapping,
                    DuplicateRecordCloning.Ignore,
                    false);

                sourceTransaction.Commit();
            }

            using var verificationTransaction = destinationDb.TransactionManager.StartTransaction();
            var verificationBlocks =
                (BlockTable)verificationTransaction.GetObject(destinationDb.BlockTableId, OpenMode.ForRead);
            if (!verificationBlocks.Has(importedName))
                throw new InvalidOperationException("Khong clone duoc block external vao ban ve hien tai.");

            ObjectId importedId = verificationBlocks[importedName];
            verificationTransaction.Commit();
            return importedId;
        }

        private static string ResolveAttributeText(
            AttributeDefinition attributeDefinition,
            LayoutTitleBlockConfig config,
            LayoutTitleBlockValueSet values)
        {
            string? pluginField = config.GetPluginField(attributeDefinition.Tag);

            // Tag has NO mapping at all → preserve block's original placeholder text
            if (pluginField == null)
                return attributeDefinition.TextString;

            // Tag IS mapped (even to None/empty field) → resolve to plugin value.
            // Do NOT fall back to block text — that can contain stale values from previous insertions.
            string mappedValue = LayoutTitleBlockFields.ResolveValue(pluginField, values);

            return mappedValue
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        }

        private static string BuildImportedBlockName(LayoutTitleBlockConfig config)
        {
            string fileStem = Sanitize(Path.GetFileNameWithoutExtension(config.DwgPath), 18);
            string sourceStem = Sanitize(
                config.SourceName == LayoutTitleBlockConfig.ModelSpaceSourceName ? "ModelSpace" : config.SourceName,
                18);
            string hash = BuildShortHash($"{config.DwgPath}|{config.SourceName}");
            return $"SD_TB_{fileStem}_{sourceStem}_{hash}";
        }

        private static string Sanitize(string value, int maxLength)
        {
            string sanitized = new string(value
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray());

            if (string.IsNullOrWhiteSpace(sanitized))
                sanitized = "TB";

            return sanitized.Length <= maxLength
                ? sanitized
                : sanitized[..maxLength];
        }

        private static string BuildShortHash(string value)
        {
            using var sha1 = SHA1.Create();
            byte[] bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes)[..8];
        }
    }
}
