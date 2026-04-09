using System;
using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;

namespace ShopDrawing.Plugin.Core
{
    internal static class ProjectDataPathResolver
    {
        private const string MarkerFileName = ".shopdrawing-project.json";
        private const string DataFolderName = "ShopDrawingData";
        private const string LogsFolderName = "logs";
        private const string LogFileName = "shopdrawing_plugin.log";

        private static readonly string AppDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShopDrawing");

        private static readonly string AppDataDataFolder = Path.Combine(AppDataRoot, "Data");
        private static readonly string AppDataLogPath = Path.Combine(AppDataRoot, LogFileName);

        public static string GetDataDirectory()
        {
            return ResolveContext(ensureExists: true).DataDirectory;
        }

        public static string GetLogPath()
        {
            return ResolveContext(ensureExists: true).LogPath;
        }

        public static string GetRuntimeRoot()
        {
            return ResolveContext(ensureExists: true).RuntimeRoot;
        }

        public static string GetProjectMarkerFileName()
        {
            return MarkerFileName;
        }

        private static PathContext ResolveContext(bool ensureExists)
        {
            string? drawingPath = TryGetActiveDrawingPath();
            if (string.IsNullOrWhiteSpace(drawingPath))
            {
                return BuildAppDataContext(ensureExists);
            }

            try
            {
                PathContext context = ResolveFromDrawingPath(drawingPath, ensureExists);

                return context;
            }
            catch (Exception)
            {
                return BuildAppDataContext(ensureExists);
            }
        }

        internal static PathContext ResolveFromDrawingPath(string drawingPath, bool ensureExists = false)
        {
            string? drawingDirectory = Path.GetDirectoryName(drawingPath);
            if (string.IsNullOrWhiteSpace(drawingDirectory) || !Directory.Exists(drawingDirectory))
            {
                throw new DirectoryNotFoundException($"Drawing directory not found for path: {drawingPath}");
            }

            string projectRoot = FindProjectRoot(drawingDirectory) ?? InferProjectRoot(drawingDirectory);
            string dataDirectory = Path.Combine(projectRoot, DataFolderName);
            string logsDirectory = Path.Combine(dataDirectory, LogsFolderName);
            string logPath = Path.Combine(logsDirectory, LogFileName);

            if (ensureExists)
            {
                Directory.CreateDirectory(projectRoot);
                Directory.CreateDirectory(dataDirectory);
                Directory.CreateDirectory(logsDirectory);
                EnsureMarkerFile(projectRoot);
            }

            return new PathContext(projectRoot, dataDirectory, logPath);
        }

        private static PathContext BuildAppDataContext(bool ensureExists)
        {
            if (ensureExists)
            {
                Directory.CreateDirectory(AppDataRoot);
                Directory.CreateDirectory(AppDataDataFolder);
            }

            return new PathContext(AppDataRoot, AppDataDataFolder, AppDataLogPath);
        }

        private static string? TryGetActiveDrawingPath()
        {
            try
            {
                Document? document = Application.DocumentManager.MdiActiveDocument;
                if (document == null)
                {
                    return null;
                }

                string? drawingPath = document.Database?.Filename;
                if (string.IsNullOrWhiteSpace(drawingPath))
                {
                    drawingPath = document.Name;
                }

                if (string.IsNullOrWhiteSpace(drawingPath) || !Path.IsPathRooted(drawingPath))
                {
                    return null;
                }

                return drawingPath;
            }
            catch
            {
                return null;
            }
        }

        private static string? FindProjectRoot(string startDirectory)
        {
            DirectoryInfo? current = new DirectoryInfo(startDirectory);
            while (current != null)
            {
                string markerPath = Path.Combine(current.FullName, MarkerFileName);
                if (File.Exists(markerPath))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return null;
        }

        private static string InferProjectRoot(string drawingDirectory)
        {
            var current = new DirectoryInfo(drawingDirectory);
            if (current.Name.Equals("Drawings", StringComparison.OrdinalIgnoreCase) && current.Parent != null)
            {
                return current.Parent.FullName;
            }

            return current.FullName;
        }

        private static void EnsureMarkerFile(string projectRoot)
        {
            string markerPath = Path.Combine(projectRoot, MarkerFileName);
            if (File.Exists(markerPath))
            {
                return;
            }

            var marker = new
            {
                projectFormat = "shopdrawing-project",
                version = 1,
                dataFolder = DataFolderName,
                createdAt = DateTimeOffset.Now
            };

            string json = JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(markerPath, json);
        }

        internal readonly record struct PathContext(string RuntimeRoot, string DataDirectory, string LogPath);
    }
}


