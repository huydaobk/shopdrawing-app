using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace ShopDrawing.Plugin.Modules.Accessories
{
    internal sealed class ShopdrawingPlanAccessoryScanner
    {
        public IReadOnlyList<ShopdrawingPlanAccessorySnapshot> ScanWallCorners(
            Transaction tr,
            Database db,
            Runtime.ShopDrawingRuntimeSettings settings)
        {
            if (tr.GetObject(db.BlockTableId, OpenMode.ForRead) is not BlockTable bt
                || tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) is not BlockTableRecord ms)
            {
                return Array.Empty<ShopdrawingPlanAccessorySnapshot>();
            }

            var markers = new List<CornerMarkerInfo>();
            foreach (ObjectId id in ms)
            {
                if (id.IsErased) continue;

                if (tr.GetObject(id, OpenMode.ForRead) is not BlockReference blockReference)
                {
                    continue;
                }

                string layerName = blockReference.Layer ?? string.Empty;
                if (!string.Equals(layerName, WallCornerMarkerCommandService.OutsideCornerLayerName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(layerName, WallCornerMarkerCommandService.InsideCornerLayerName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string application = ReadAttribute(blockReference, tr, "APP");
                string specKey = ReadAttribute(blockReference, tr, "SPEC");
                string kind = ReadAttribute(blockReference, tr, "CORNER_KIND");
                string heightText = ReadAttribute(blockReference, tr, "HEIGHT_MM");

                if (!double.TryParse(heightText, out double heightMm) || heightMm <= 0)
                {
                    string heightRaw = ReadAttribute(blockReference, tr, "HEIGHT");
                    if (string.IsNullOrWhiteSpace(heightRaw))
                    {
                        heightMm = settings.DefaultWallHeight;
                    }
                    else
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(heightRaw, @"\d+");
                        if (match.Success)
                        {
                            heightMm = double.Parse(match.Value);
                        }
                        else
                        {
                            heightMm = settings.DefaultWallHeight;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(application))
                {
                    application = settings.DefaultApplication;
                }

                if (string.IsNullOrWhiteSpace(specKey))
                {
                    specKey = settings.DefaultSpec;
                }

                if (string.IsNullOrWhiteSpace(kind))
                {
                    kind = string.Equals(layerName, WallCornerMarkerCommandService.OutsideCornerLayerName, StringComparison.OrdinalIgnoreCase)
                        ? nameof(WallCornerMarkerKind.Outside)
                        : nameof(WallCornerMarkerKind.Inside);
                }

                markers.Add(new CornerMarkerInfo(application, specKey, kind, heightMm));
            }

            return markers
                .GroupBy(x => $"{x.Application}||{x.SpecKey}", StringComparer.OrdinalIgnoreCase)
                .Select(group => new ShopdrawingPlanAccessorySnapshot(
                    group.First().Application,
                    group.First().SpecKey,
                    group.Where(x => x.IsOutside).Sum(x => x.HeightMm) / 1000.0,
                    group.Where(x => !x.IsOutside).Sum(x => x.HeightMm) / 1000.0,
                    group.Count(x => x.IsOutside),
                    group.Count(x => !x.IsOutside)))
                .OrderBy(x => x.Application, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.SpecKey, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ReadAttribute(BlockReference blockReference, Transaction tr, string tag)
        {
            foreach (ObjectId attributeId in blockReference.AttributeCollection)
            {
                if (tr.GetObject(attributeId, OpenMode.ForRead) is not AttributeReference attributeReference)
                {
                    continue;
                }

                if (string.Equals(attributeReference.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    return attributeReference.TextString?.Trim() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private sealed record CornerMarkerInfo(
            string Application,
            string SpecKey,
            string Kind,
            double HeightMm)
        {
            public bool IsOutside => string.Equals(Kind, nameof(WallCornerMarkerKind.Outside), StringComparison.OrdinalIgnoreCase);
        }
    }
}
