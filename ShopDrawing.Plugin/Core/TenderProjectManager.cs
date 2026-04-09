using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    /// <summary>
    /// Save/Load Tender projects as JSON files.
    /// Projects are stored under AppData to keep runtime data out of the repo and install folder.
    /// </summary>
    public class TenderProjectManager
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public TenderProjectManager()
        {
            EnsureProjectsFolder();
        }

        public TenderProject CreateNew(string projectName, string customerName)
        {
            var profile = new ProjectProfileManager().LoadOrDefault();
            var specManager = new SpecConfigManager();
            var project = new TenderProject
            {
                ProjectName = string.IsNullOrWhiteSpace(projectName) ? profile.ProjectName : projectName,
                CustomerName = string.IsNullOrWhiteSpace(customerName) ? profile.CustomerName : customerName,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Specs = specManager.GetAll(),
                Accessories = AccessoryDataManager.GetDefaults()
            };
            project.Accessories = AccessoryDataManager.NormalizeConfiguredAccessories(project.Accessories);
            return project;
        }

        public string Save(TenderProject project, string? filePath = null)
        {
            project.UpdatedAt = DateTime.Now;
            project.Accessories = AccessoryDataManager.NormalizeConfiguredAccessories(project.Accessories);

            if (string.IsNullOrEmpty(filePath))
            {
                if (!string.IsNullOrEmpty(project.FilePath))
                {
                    filePath = project.FilePath;
                }
                else
                {
                    string safeName = SanitizeFileName(project.ProjectName);
                    if (string.IsNullOrEmpty(safeName))
                    {
                        safeName = "tender_project";
                    }

                    string timestamp = DateTime.Now.ToString("yyMMdd_HHmm");
                    filePath = Path.Combine(GetProjectsFolder(), $"{safeName}_{timestamp}.json");
                }
            }

            string json = JsonSerializer.Serialize(project, _jsonOptions);
            File.WriteAllText(filePath, json);
            project.FilePath = filePath;
            return filePath;
        }

        public TenderProject? Load(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var project = JsonSerializer.Deserialize<TenderProject>(json, _jsonOptions);
                if (project != null)
                {
                    project.Accessories = AccessoryDataManager.NormalizeConfiguredAccessories(project.Accessories);
                    project.FilePath = filePath;
                }

                return project;
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
                return null;
            }
        }

        public List<string> ListProjects()
        {
            var files = new List<string>();
            try
            {
                foreach (var file in Directory.GetFiles(GetProjectsFolder(), "*.json"))
                {
                    files.Add(file);
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Error("Suppressed exception in TenderProjectManager.cs", ex);
            }

            return files;
        }

        public string ProjectsFolder => GetProjectsFolder();

        public string GetAutoSavePath(string dwgFileName)
        {
            string safeName = SanitizeFileName(Path.GetFileNameWithoutExtension(dwgFileName));
            if (string.IsNullOrEmpty(safeName))
            {
                safeName = "untitled";
            }

            return Path.Combine(GetProjectsFolder(), $"autosave_{safeName}.json");
        }

        public string AutoSave(TenderProject project, string dwgFileName)
        {
            string path = GetAutoSavePath(dwgFileName);
            return Save(project, path);
        }

        public TenderProject? TryAutoLoad(string dwgFileName)
        {
            string path = GetAutoSavePath(dwgFileName);
            return File.Exists(path) ? Load(path) : null;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name.Trim().Replace(' ', '_');
        }

        private static string GetProjectsFolder()
        {
            return Path.Combine(PluginLogger.GetDataDirectory(), "tender_projects");
        }

        private static void EnsureProjectsFolder()
        {
            try
            {
                Directory.CreateDirectory(GetProjectsFolder());
            }
            catch (Exception ex)
            {
                PluginLogger.Error("Suppressed exception in TenderProjectManager.cs", ex);
            }
        }
    }
}
