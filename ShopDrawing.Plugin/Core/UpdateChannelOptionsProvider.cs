using System;
using System.IO;
using System.Text.Json;

namespace ShopDrawing.Plugin.Core
{
    internal static class UpdateChannelOptionsProvider
    {
        private const string SettingsFileName = "update-settings.json";

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
                string settingsPath = Path.Combine(PluginVersionProvider.GetInstallDirectory(), SettingsFileName);
                if (!File.Exists(settingsPath))
                {
                    return new UpdateChannelOptions();
                }

                string json = File.ReadAllText(settingsPath);
                return JsonSerializer.Deserialize<UpdateChannelOptions>(json, JsonOptions) ?? new UpdateChannelOptions();
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
                return new UpdateChannelOptions();
            }
        }
    }
}
