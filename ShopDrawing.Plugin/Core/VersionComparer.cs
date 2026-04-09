using System;

namespace ShopDrawing.Plugin.Core
{
    internal static class VersionComparer
    {
        public static bool IsNewer(string candidateVersion, string currentVersion)
        {
            return TryParse(candidateVersion, out Version? candidate)
                && TryParse(currentVersion, out Version? current)
                && candidate > current;
        }

        public static bool TryParse(string? rawVersion, out Version? version)
        {
            version = null;

            if (string.IsNullOrWhiteSpace(rawVersion))
            {
                return false;
            }

            string normalized = rawVersion.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(1);
            }

            int suffixIndex = normalized.IndexOfAny(new[] { '-', '+' });
            if (suffixIndex >= 0)
            {
                normalized = normalized.Substring(0, suffixIndex);
            }

            return Version.TryParse(normalized, out version);
        }
    }
}
