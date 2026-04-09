using System;
using System.IO;
using System.Text.Json;

namespace ShopDrawing.Plugin.Core
{
    internal static class UpdateChannelOptionsProvider
    {
        private const string SettingsFileName = "update-settings.json";
        private const string DefaultManifestUrl = "https://github.com/huydaobk/shopdrawing-app/releases/latest/download/latest.json";

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
                if (string.IsNullOrWhiteSpace(parsed.ManifestUrl))
                {
                    parsed.ManifestUrl = DefaultManifestUrl;
                }

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
    }
}
