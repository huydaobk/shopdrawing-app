using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    /// <summary>
    /// Save/Load dự án Tender thành JSON file.
    /// Projects lưu tại: public/Resources/tender_projects/
    /// </summary>
    public class TenderProjectManager
    {
        private readonly string _projectsFolder;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public TenderProjectManager()
        {
            string assemblyFolder;
            try
            {
                string loc = Assembly.GetExecutingAssembly().Location;
                assemblyFolder = !string.IsNullOrEmpty(loc)
                    ? Path.GetDirectoryName(loc) ?? @"c:\my_project\shopdrawing-app\public"
                    : @"c:\my_project\shopdrawing-app\public";
            }
            catch
            {
                assemblyFolder = @"c:\my_project\shopdrawing-app\public";
            }

            _projectsFolder = Path.Combine(assemblyFolder, "Resources", "tender_projects");
            try { Directory.CreateDirectory(_projectsFolder); } catch { }
        }

        /// <summary>Tạo project mới với specs fork từ ShopDrawing + accessories mặc định</summary>
        public TenderProject CreateNew(string projectName, string customerName)
        {
            var specManager = new SpecConfigManager();
            var project = new TenderProject
            {
                ProjectName = projectName,
                CustomerName = customerName,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Specs = specManager.GetAll(),                    // Fork specs
                Accessories = AccessoryDataManager.GetDefaults() // Default accessories
            };
            project.Accessories = AccessoryDataManager.NormalizeConfiguredAccessories(project.Accessories);
            return project;
        }

        /// <summary>Lưu project thành JSON file</summary>
        public string Save(TenderProject project, string? filePath = null)
        {
            project.UpdatedAt = DateTime.Now;

            if (string.IsNullOrEmpty(filePath))
            {
                if (!string.IsNullOrEmpty(project.FilePath))
                {
                    filePath = project.FilePath;
                }
                else
                {
                    // Tạo tên file từ project name
                    string safeName = SanitizeFileName(project.ProjectName);
                    if (string.IsNullOrEmpty(safeName)) safeName = "tender_project";
                    string timestamp = DateTime.Now.ToString("yyMMdd_HHmm");
                    filePath = Path.Combine(_projectsFolder, $"{safeName}_{timestamp}.json");
                }
            }

            string json = JsonSerializer.Serialize(project, _jsonOptions);
            File.WriteAllText(filePath, json);
            project.FilePath = filePath;
            return filePath;
        }

        /// <summary>Load project từ JSON file</summary>
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
            catch
            {
                return null;
            }
        }

        /// <summary>Liệt kê tất cả project files đã lưu</summary>
        public List<string> ListProjects()
        {
            var files = new List<string>();
            try
            {
                foreach (var f in Directory.GetFiles(_projectsFolder, "*.json"))
                    files.Add(f);
            }
            catch { }
            return files;
        }

        /// <summary>Lấy thư mục lưu projects</summary>
        public string ProjectsFolder => _projectsFolder;

        /// <summary>Lấy auto-save path dựa trên tên file DWG hiện tại</summary>
        public string GetAutoSavePath(string dwgFileName)
        {
            string safeName = SanitizeFileName(Path.GetFileNameWithoutExtension(dwgFileName));
            if (string.IsNullOrEmpty(safeName)) safeName = "untitled";
            return Path.Combine(_projectsFolder, $"autosave_{safeName}.json");
        }

        /// <summary>Auto-save project theo tên DWG</summary>
        public string AutoSave(TenderProject project, string dwgFileName)
        {
            string path = GetAutoSavePath(dwgFileName);
            return Save(project, path);
        }

        /// <summary>Auto-load project nếu có file autosave cho DWG này</summary>
        public TenderProject? TryAutoLoad(string dwgFileName)
        {
            string path = GetAutoSavePath(dwgFileName);
            if (File.Exists(path))
                return Load(path);
            return null;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim().Replace(' ', '_');
        }
    }
}
