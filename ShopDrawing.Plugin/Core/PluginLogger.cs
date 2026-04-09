using System;
using System.IO;
using System.Reflection;

namespace ShopDrawing.Plugin.Core
{
    internal static class PluginLogger
    {
        private static readonly string BundledResourcesFolder = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            "Resources");

        public static void Initialize()
        {
            try
            {
                EnsureDirectory(ProjectDataPathResolver.GetDataDirectory());
                EnsureDirectory(Path.GetDirectoryName(ProjectDataPathResolver.GetLogPath())!);
            }
            catch (Exception ex)
            {
                Error("Suppressed exception in PluginLogger.cs", ex);
            }
        }

        public static string GetDataDirectory()
        {
            string dataDirectory = ProjectDataPathResolver.GetDataDirectory();
            EnsureDirectory(dataDirectory);
            return dataDirectory;
        }

        public static string GetAppDataRoot()
        {
            string runtimeRoot = ProjectDataPathResolver.GetRuntimeRoot();
            EnsureDirectory(runtimeRoot);
            return runtimeRoot;
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
                string logPath = ProjectDataPathResolver.GetLogPath();
                string? logDirectory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(logDirectory))
                {
                    EnsureDirectory(logDirectory);
                }

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var threadId = Environment.CurrentManagedThreadId;
                File.AppendAllText(logPath, $"[{timestamp}] [Thread:{threadId:D2}] [{level}] {msg}\n");
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
