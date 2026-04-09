using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    public class SpecConfigManager
    {
        public static event System.Action? SpecsChanged;

        private List<PanelSpec>? _inMemorySpecs;

        public SpecConfigManager()
        {
            EnsureConfigFileExists();
        }

        public SpecConfigManager(List<PanelSpec> specs)
        {
            _inMemorySpecs = specs.Select(s => new PanelSpec
            {
                Key = s.Key,
                WallCodePrefix = s.WallCodePrefix,
                Description = s.Description,
                PanelType = s.PanelType,
                Thickness = s.Thickness,
                PanelWidth = s.PanelWidth,
                Density = s.Density,
                FireRating = s.FireRating,
                FmApproved = s.FmApproved,
                FacingColor = s.FacingColor,
                TopFacing = s.TopFacing,
                TopCoating = s.TopCoating,
                TopSteelThickness = s.TopSteelThickness,
                TopProfile = s.TopProfile,
                BottomFacing = s.BottomFacing,
                BottomCoating = s.BottomCoating,
                BottomSteelThickness = s.BottomSteelThickness,
                BottomProfile = s.BottomProfile
            }).ToList();
        }

        public List<PanelSpec> GetAll()
        {
            if (_inMemorySpecs != null)
            {
                return new List<PanelSpec>(_inMemorySpecs);
            }

            try
            {
                string json = File.ReadAllText(GetConfigPath());
                return JsonSerializer.Deserialize<List<PanelSpec>>(json) ?? new List<PanelSpec>();
            }
            catch (System.Exception ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
                return new List<PanelSpec>();
            }
        }

        public void Save(List<PanelSpec> specs)
        {
            if (_inMemorySpecs != null)
            {
                _inMemorySpecs = new List<PanelSpec>(specs);
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(specs, options);
            File.WriteAllText(GetConfigPath(), json);
            SpecsChanged?.Invoke();
        }

        public PanelSpec? GetByKey(string key)
        {
            return GetAll().Find(s => s.Key == key);
        }

        private static string GetConfigPath()
        {
            return Path.Combine(PluginLogger.GetDataDirectory(), "panel_specs.json");
        }

        private static void EnsureConfigFileExists()
        {
            string configPath = GetConfigPath();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            }
            catch (System.Exception ex)
            {
                PluginLogger.Error("Suppressed exception in SpecConfigManager.cs", ex);
            }

            if (File.Exists(configPath))
            {
                return;
            }

            try
            {
                string bundledConfigPath = Path.Combine(PluginLogger.GetBundledResourcesDirectory(), "panel_specs.json");
                if (File.Exists(bundledConfigPath))
                {
                    File.Copy(bundledConfigPath, configPath, overwrite: false);
                }
                else
                {
                    string json = JsonSerializer.Serialize(BuildDefaultSpecs(), new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(configPath, json);
                }
            }
            catch (System.Exception ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
            }
        }

        private static List<PanelSpec> BuildDefaultSpecs()
        {
            return new List<PanelSpec>
            {
                new PanelSpec
                {
                    Key = "ISOFRIGO-TT", WallCodePrefix = "W", Description = "Ton/Ton",
                    PanelType = "ISOFRIGO", Density = "44+-2", FireRating = "-", FmApproved = true,
                    FacingColor = "Trang",
                    TopFacing = "Tole", TopCoating = "AZ150", TopSteelThickness = 0.6, TopProfile = "Vuong",
                    BottomFacing = "Tole", BottomCoating = "AZ150", BottomSteelThickness = 0.6, BottomProfile = "Vuong"
                },
                new PanelSpec
                {
                    Key = "ISOFRIGO-TI", WallCodePrefix = "W", Description = "Ton/Inox",
                    PanelType = "ISOFRIGO", Density = "44+-2", FireRating = "-", FmApproved = true,
                    FacingColor = "Trang",
                    TopFacing = "Tole", TopCoating = "AZ150", TopSteelThickness = 0.6, TopProfile = "Vuong",
                    BottomFacing = "Inox", BottomCoating = "AZ150", BottomSteelThickness = 0.5, BottomProfile = "Vuong"
                },
                new PanelSpec
                {
                    Key = "ISOFRIGO-II", WallCodePrefix = "W", Description = "Inox/Inox",
                    PanelType = "ISOFRIGO", Density = "44+-2", FireRating = "-", FmApproved = true,
                    FacingColor = "Trang",
                    TopFacing = "Inox", TopCoating = "AZ150", TopSteelThickness = 0.5, TopProfile = "Vuong",
                    BottomFacing = "Inox", BottomCoating = "AZ150", BottomSteelThickness = 0.5, BottomProfile = "Vuong"
                }
            };
        }
    }
}
