using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
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
            if (!TryGetExistingProfilePath(out string path, out string dataDirectory))
            {
                return new ProjectProfile();
            }

            if (!File.Exists(path))
            {
                return BuildFallbackProfile(dataDirectory);
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

        private static bool TryGetExistingProfilePath(out string path, out string dataDirectory)
        {
            path = string.Empty;
            dataDirectory = string.Empty;
            if (!ProjectDataPathResolver.TryResolveExistingProjectContext(out _, out dataDirectory))
            {
                return false;
            }

            path = Path.Combine(dataDirectory, ProfileFileName);
            return true;
        }

        private static ProjectProfile BuildFallbackProfile(string dataDirectory)
        {
            var profile = new ProjectProfile();

            try
            {
                Document? document = Application.DocumentManager.MdiActiveDocument;
                if (document != null)
                {
                    profile.ProjectName = DrawingListManager.GetDocumentProjectName(document)?.Trim() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
            }

            try
            {
                string tenderProjectsDirectory = Path.Combine(dataDirectory, "tender_project");
                if (Directory.Exists(tenderProjectsDirectory))
                {
                    string? latestTenderFile = Directory
                        .GetFiles(tenderProjectsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(latestTenderFile))
                    {
                        string json = File.ReadAllText(latestTenderFile);
                        TenderProject? tenderProject = JsonSerializer.Deserialize<TenderProject>(json, JsonOptions);
                        if (tenderProject != null)
                        {
                            if (string.IsNullOrWhiteSpace(profile.ProjectName))
                            {
                                profile.ProjectName = tenderProject.ProjectName?.Trim() ?? string.Empty;
                            }

                            profile.CustomerName = tenderProject.CustomerName?.Trim() ?? string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
            }

            return profile;
        }

        public void Save(ProjectProfile profile)
        {
            profile.UpdatedAt = DateTime.Now;
            if (!ProjectDataPathResolver.TryResolveActiveDrawingContext(out _, out string dataDirectory))
            {
                throw new InvalidOperationException("Ban ve chua duoc luu. Hay SaveAs vao thu muc du an truoc khi luu INPUT.");
            }

            ProjectDataPathResolver.EnsureProjectMarkerForActiveDrawing();
            string path = Path.Combine(dataDirectory, ProfileFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string json = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(path, json);
            ProfileUpdated?.Invoke(profile);
        }
    }
}
