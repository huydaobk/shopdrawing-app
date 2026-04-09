using System;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Data;

namespace ShopDrawing.Plugin.Runtime
{
    internal static class ShopDrawingRuntimeServices
    {
        public static SpecConfigManager SpecManager { get; } = new();

        public static ShopDrawingRuntimeSettings Settings { get; } = new();

        public static string WasteDbPath => System.IO.Path.Combine(
            PluginLogger.GetDataDirectory(),
            "shopdrawing_waste.db");

        private static WasteRepository? _wasteRepo;
        private static string? _wasteRepoPath;

        public static WasteRepository? WasteRepo => GetWasteRepository();

        public static BomManager BomManager { get; } = new();

        public static LayoutEngine LayoutEngine { get; } = new();

        public static BlockManager BlockManager { get; } = new();

        static ShopDrawingRuntimeServices()
        {
            PluginLogger.Initialize();

            _ = GetWasteRepository();

            try
            {
                BomManager.RegisterReactor();
            }
            catch (Exception ex)
            {
                PluginLogger.Error("BomManager.RegisterReactor failed", ex);
            }
        }

        public static void RefreshProjectScopedServices()
        {
            _wasteRepo = null;
            _wasteRepoPath = null;
            _ = GetWasteRepository();
        }

        private static WasteRepository? GetWasteRepository()
        {
            string dbPath = WasteDbPath;
            if (_wasteRepo != null && string.Equals(_wasteRepoPath, dbPath, StringComparison.OrdinalIgnoreCase))
            {
                return _wasteRepo;
            }

            try
            {
                _wasteRepo = new WasteRepository(dbPath);
                _wasteRepoPath = dbPath;
            }
            catch (Exception ex)
            {
                _wasteRepo = null;
                _wasteRepoPath = null;
                PluginLogger.Error("WasteRepo init failed", ex);
            }

            return _wasteRepo;
        }
    }
}
