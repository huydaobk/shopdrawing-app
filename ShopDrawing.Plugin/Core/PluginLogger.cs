using System;
using System.IO;
using System.Reflection;

namespace ShopDrawing.Plugin.Core
{
    internal static class PluginLogger
    {
        private static readonly string AppDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShopDrawing");

        private static readonly string LogPath = Path.Combine(AppDataRoot, "shopdrawing_plugin.log");

        private static readonly string DataFolder = Path.Combine(AppDataRoot, "Data");

        private static readonly string BundledResourcesFolder = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            "Resources");

        public static void Initialize()
        {
            try
            {
                EnsureDirectory(AppDataRoot);
                EnsureDirectory(DataFolder);
            }
            catch (Exception ex)
            {
                Error("Suppressed exception in PluginLogger.cs", ex);
            }
        }

        public static string GetDataDirectory()
        {
            EnsureDirectory(DataFolder);
            return DataFolder;
        }

        public static string GetAppDataRoot()
        {
            EnsureDirectory(AppDataRoot);
            return AppDataRoot;
        }

        public static string GetBundledResourcesDirectory()
        {
            return BundledResourcesFolder;
        }

        public static void Info(string msg) => Log("INFO", msg);

        public static void Warn(string msg) => Log("WARN", msg);

        public static void Error(string msg, Exception? ex = null)
        {
            string errorMessage = ex != null ? $"{msg}\nException: {ex.Message}\nStackTrace:\n{ex.StackTrace}" : msg;
            Log("ERROR", errorMessage);
        }

        private static void Log(string level, string msg)
        {
            try
            {
                EnsureDirectory(AppDataRoot);
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var threadId = Environment.CurrentManagedThreadId;
                File.AppendAllText(LogPath, $"[{timestamp}] [Thread:{threadId:D2}] [{level}] {msg}\n");
            }
            catch
            {
                // Must act as a sink, never throw from catching exceptions in the plugin
            }
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
