using System;
using System.Collections.Generic;
using System.Linq;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    internal static class WallHeightResolver
    {
        public static IReadOnlyList<TenderHeightSegment> Normalize(
            double wallLengthMm,
            double fallbackHeightMm,
            IEnumerable<TenderHeightSegment>? segments)
        {
            if (wallLengthMm <= 0 || fallbackHeightMm <= 0)
                return Array.Empty<TenderHeightSegment>();

            var valid = (segments ?? Enumerable.Empty<TenderHeightSegment>())
                .Where(s => s != null && s.LengthMm > 0 && s.HeightMm > 0)
                .Select(s => new TenderHeightSegment
                {
                    LengthMm = s.LengthMm,
                    HeightMm = s.HeightMm,
                    CadHandle = s.CadHandle
                })
                .ToList();

            if (valid.Count == 0)
            {
                return new List<TenderHeightSegment>
                {
                    new() { LengthMm = wallLengthMm, HeightMm = fallbackHeightMm }
                };
            }

            double totalLength = valid.Sum(s => s.LengthMm);
            if (totalLength <= 0)
            {
                return new List<TenderHeightSegment>
                {
                    new() { LengthMm = wallLengthMm, HeightMm = fallbackHeightMm }
                };
            }

            double scale = wallLengthMm / totalLength;
            foreach (var segment in valid)
                segment.LengthMm *= scale;

            return valid;
        }
    }
}
