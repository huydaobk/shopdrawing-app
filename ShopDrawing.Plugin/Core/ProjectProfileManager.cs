using System;
using System.IO;
using System.Text.Json;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    internal sealed class ProjectProfileManager
    {
        private const string ProfileFileName = "project_profile.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static event Action<ProjectProfile>? ProfileUpdated;

        public string GetProfilePath()
        {
            return Path.Combine(ProjectDataPathResolver.GetDataDirectory(), ProfileFileName);
        }

        public ProjectProfile LoadOrDefault()
        {
            if (!TryGetExistingProfilePath(out string path))
            {
                return new ProjectProfile();
            }

            if (!File.Exists(path))
            {
                return new ProjectProfile();
            }

            try
            {
                string json = File.ReadAllText(path);
                var profile = JsonSerializer.Deserialize<ProjectProfile>(json, JsonOptions);
                return profile ?? new ProjectProfile();
            }
            catch (Exception ex)
            {
                PluginLogger.Error("Suppressed exception in ProjectProfileManager.cs", ex);
                return new ProjectProfile();
            }
        }

        private static bool TryGetExistingProfilePath(out string path)
        {
            path = string.Empty;
            if (!ProjectDataPathResolver.TryResolveExistingProjectContext(out _, out string dataDirectory))
            {
                return false;
            }

            path = Path.Combine(dataDirectory, ProfileFileName);
            return true;
        }

        public void Save(ProjectProfile profile)
        {
            profile.UpdatedAt = DateTime.Now;
            string path = GetProfilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string json = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(path, json);
            ProfileUpdated?.Invoke(profile);
        }
    }
}
