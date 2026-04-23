using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ShopDrawing.Plugin.Models;

namespace ShopDrawing.Plugin.Core
{
    public class ShopdrawingAccessoryConfigManager
    {
        public static event Action? ConfigChanged;

        private static List<TenderAccessory>? _inMemoryConfig;

        static ShopdrawingAccessoryConfigManager()
        {
            EnsureConfigFileExists();
        }

        public static List<TenderAccessory> GetAll()
        {
            if (_inMemoryConfig != null)
            {
                return new List<TenderAccessory>(_inMemoryConfig);
            }

            try
            {
                string json = File.ReadAllText(GetConfigPath());
                _inMemoryConfig = JsonSerializer.Deserialize<List<TenderAccessory>>(json) ?? new List<TenderAccessory>();
                return new List<TenderAccessory>(_inMemoryConfig);
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Suppressed exception loading shopdrawing accessory config: " + ex.Message);
                return new List<TenderAccessory>();
            }
        }

        public static void Save(List<TenderAccessory> accessories)
        {
            _inMemoryConfig = new List<TenderAccessory>(accessories);

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(accessories, options);
                File.WriteAllText(GetConfigPath(), json);
                ConfigChanged?.Invoke();
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Failed to save shopdrawing accessory config: " + ex.Message);
            }
        }

        private static string GetConfigPath()
        {
            return Path.Combine(PluginLogger.GetDataDirectory(), "Shared", "shopdrawing_accessories.json");
        }

        public static List<TenderAccessory> GenerateDefaultConfig()
        {
            var defaultAccessories = AccessoryDataManager.GetDefaults();
                
            // Remove legacy height-based corner accessories for Shopdrawing
            defaultAccessories.RemoveAll(a => a.CalcRule == AccessoryCalcRule.PER_OUTSIDE_CORNER_HEIGHT || a.CalcRule == AccessoryCalcRule.PER_INSIDE_CORNER_HEIGHT);

            // Add the new "Góc ngã ba" accessory
            defaultAccessories.Add(new TenderAccessory
            {
                CategoryScope = "Vách",
                Application = "Tất cả",
                SpecKey = "Tất cả",
                Name = "Góc ngã ba",
                Material = "Nhôm",
                Position = "Góc ngoài",
                Unit = "cái",
                CalcRule = AccessoryCalcRule.PER_OUTSIDE_CORNER_QTY,
                Factor = 2.0,
                Note = "2 cái/góc"
            });

            return defaultAccessories;
        }

        private static void EnsureConfigFileExists()
        {
            string configPath = GetConfigPath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            }
            catch (Exception ex)
            {
                PluginLogger.Error("Suppressed exception in ShopdrawingAccessoryConfigManager.cs", ex);
            }

            if (File.Exists(configPath))
            {
                return;
            }

            try
            {
                var defaultAccessories = GenerateDefaultConfig();
                
                string json = JsonSerializer.Serialize(defaultAccessories, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
                _inMemoryConfig = defaultAccessories;
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Suppressed exception writing default shopdrawing config: " + ex.Message);
            }
        }
    }
}
