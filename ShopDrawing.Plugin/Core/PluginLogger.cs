using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace ShopDrawing.Plugin.Core
{
    internal static class PluginLogger
    {
        private static readonly string LogPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            "shopdrawing_plugin.log");

        private static readonly string DataFolder = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            "Data");

        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(DataFolder))
                {
                    Directory.CreateDirectory(DataFolder);
                }
            }
            catch (System.Exception ex)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in PluginLogger.cs", ex);
            }
        }

        public static string GetDataDirectory()
        {
            return DataFolder;
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
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var threadId = Environment.CurrentManagedThreadId;
                File.AppendAllText(LogPath, $"[{timestamp}] [Thread:{threadId:D2}] [{level}] {msg}\n");
            }
            catch
            {
                // Must act as a sink, never throw from catching exceptions in the plugin
            }
        }
    }
}
