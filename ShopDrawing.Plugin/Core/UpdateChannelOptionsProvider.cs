using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace ShopDrawing.Plugin.Core
{
    internal static class UpdateChannelOptionsProvider
    {
        private const string SettingsFileName = "update-settings.json";
        private const string DefaultManifestUrl = "https://api.github.com/repos/huydaobk/shopdrawing-app/releases/latest";
        private static readonly Regex LegacyReleaseManifestRegex = new(
            @"^https://github\.com/huydaobk/shopdrawing-app/releases/download/v[^/]+/latest\.json$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static UpdateChannelOptions Load()
        {
            try
            {
                var defaults = new UpdateChannelOptions
                {
                    ManifestUrl = DefaultManifestUrl
                };

                string settingsPath = Path.Combine(PluginVersionProvider.GetInstallDirectory(), SettingsFileName);
                if (!File.Exists(settingsPath))
                {
                    return defaults;
                }

                string json = File.ReadAllText(settingsPath);
                var parsed = JsonSerializer.Deserialize<UpdateChannelOptions>(json, JsonOptions) ?? defaults;
                parsed.ManifestUrl = NormalizeManifestUrl(parsed.ManifestUrl);

                return parsed;
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
                return new UpdateChannelOptions
                {
                    ManifestUrl = DefaultManifestUrl
                };
            }
        }

        private static string NormalizeManifestUrl(string? manifestUrl)
        {
            if (string.IsNullOrWhiteSpace(manifestUrl))
            {
                return DefaultManifestUrl;
            }

            string normalized = manifestUrl.Trim();

            // Tu dong nang cap cac URL cu dang pin vao 1 version release cu.
            if (LegacyReleaseManifestRegex.IsMatch(normalized))
            {
                return "https://github.com/huydaobk/shopdrawing-app/releases/latest/download/latest.json";
            }

            return normalized;
        }
    }
}
