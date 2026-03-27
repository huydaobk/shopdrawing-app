using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    public class SpecConfigManager
    {
        /// <summary>Fire sau mỗi lần Save() — Palette subscribe để tự refresh Spec dropdown.</summary>
        public static event System.Action? SpecsChanged;

        private readonly string _configPath;
        private List<PanelSpec>? _inMemorySpecs; // For Tender fork pattern

        public SpecConfigManager()
        {
            // AutoCAD NETLOAD: Assembly.Location co the tra ve "" (empty)
            // Dung thu tu uu tien: Location → CodeBase → fallback hardcoded path
            string assemblyFolder;
            try
            {
                string loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    assemblyFolder = Path.GetDirectoryName(loc) ?? string.Empty;
                }
                else
                {
                    // Fallback: dung duong dan workspace cong khai
                    assemblyFolder = @"c:\my_project\shopdrawing-app\public";
                }
            }
            catch
            {
                assemblyFolder = @"c:\my_project\shopdrawing-app\public";
            }

            _configPath = Path.Combine(assemblyFolder, "Resources", "panel_specs.json");
            
            // Đảm bảo folder tồn tại
            try { Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!); } catch { }
            
            // Nếu chưa có file thì tạo file mặc định
            if (!File.Exists(_configPath))
            {
                try
                {
                    var defaultSpecs = new List<PanelSpec>
                    {
                        new PanelSpec
                        {
                            Key = "ISOFRIGO-TT", WallCodePrefix = "W", Description = "Tôn/Tôn",
                            PanelType = "ISOFRIGO", Density = "44±2", FireRating = "-", FmApproved = true,
                            FacingColor = "Trắng",
                            TopFacing = "Tole", TopCoating = "AZ150", TopSteelThickness = 0.6, TopProfile = "Vuông",
                            BottomFacing = "Tole", BottomCoating = "AZ150", BottomSteelThickness = 0.6, BottomProfile = "Vuông"
                        },
                        new PanelSpec
                        {
                            Key = "ISOFRIGO-TI", WallCodePrefix = "W", Description = "Tôn/Inox",
                            PanelType = "ISOFRIGO", Density = "44±2", FireRating = "-", FmApproved = true,
                            FacingColor = "Trắng",
                            TopFacing = "Tole", TopCoating = "AZ150", TopSteelThickness = 0.6, TopProfile = "Vuông",
                            BottomFacing = "Inox", BottomCoating = "AZ150", BottomSteelThickness = 0.5, BottomProfile = "Vuông"
                        },
                        new PanelSpec
                        {
                            Key = "ISOFRIGO-II", WallCodePrefix = "W", Description = "Inox/Inox",
                            PanelType = "ISOFRIGO", Density = "44±2", FireRating = "-", FmApproved = true,
                            FacingColor = "Trắng",
                            TopFacing = "Inox", TopCoating = "AZ150", TopSteelThickness = 0.5, TopProfile = "Vuông",
                            BottomFacing = "Inox", BottomCoating = "AZ150", BottomSteelThickness = 0.5, BottomProfile = "Vuông"
                        }
                    };
                    Save(defaultSpecs);
                }
                catch { /* Khong tao duoc default thi bo qua */ }
            }
        }

        /// <summary>Constructor cho Tender fork pattern — in-memory, không dùng file</summary>
        public SpecConfigManager(List<PanelSpec> specs)
        {
            _configPath = string.Empty;
            _inMemorySpecs = specs.Select(s => new PanelSpec
            {
                Key = s.Key, WallCodePrefix = s.WallCodePrefix, Description = s.Description,
                PanelType = s.PanelType, Thickness = s.Thickness, PanelWidth = s.PanelWidth,
                Density = s.Density, FireRating = s.FireRating, FmApproved = s.FmApproved,
                FacingColor = s.FacingColor,
                TopFacing = s.TopFacing, TopCoating = s.TopCoating,
                TopSteelThickness = s.TopSteelThickness, TopProfile = s.TopProfile,
                BottomFacing = s.BottomFacing, BottomCoating = s.BottomCoating,
                BottomSteelThickness = s.BottomSteelThickness, BottomProfile = s.BottomProfile
            }).ToList();
        }

        public List<PanelSpec> GetAll()
        {
            if (_inMemorySpecs != null)
                return new List<PanelSpec>(_inMemorySpecs);
            try
            {
                string json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<List<PanelSpec>>(json) ?? new List<PanelSpec>();
            }
            catch
            {
                return new List<PanelSpec>();
            }
        }

        public void Save(List<PanelSpec> specs)
        {
            if (_inMemorySpecs != null)
            {
                _inMemorySpecs = new List<PanelSpec>(specs);
                return; // In-memory mode: không write file, không fire global event
            }
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(specs, options);
            File.WriteAllText(_configPath, json);
            // Notify palette và bất kỳ subscriber nào rằng Spec list đã thay đổi
            SpecsChanged?.Invoke();
        }

        public PanelSpec? GetByKey(string key)
        {
            return GetAll().Find(s => s.Key == key);
        }
    }
}
