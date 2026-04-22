using System;
using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;

namespace ShopDrawing.Plugin.Core
{
    internal static class ProjectDataPathResolver
    {
        private const string MarkerFileName = ".shopdrawing-project.json";
        private const string DataFolderName = "Project Data";
        private const string LogsFolderName = "Log";
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

        public static bool TryResolveActiveDrawingContext(out string projectRoot, out string dataDirectory)
        {
            projectRoot = string.Empty;
            dataDirectory = string.Empty;

            string? drawingPath = TryGetActiveDrawingPath();
            if (string.IsNullOrWhiteSpace(drawingPath))
            {
                return false;
            }

            try
            {
                PathContext context = ResolveFromDrawingPath(drawingPath, ensureExists: false);
                projectRoot = context.RuntimeRoot;
                dataDirectory = context.DataDirectory;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void EnsureProjectMarkerForActiveDrawing()
        {
            if (!TryResolveActiveDrawingContext(out string projectRoot, out string dataDirectory))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(projectRoot);
                Directory.CreateDirectory(dataDirectory);
                string logPath = Path.Combine(dataDirectory, LogsFolderName, LogFileName);
                string? logDirectory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                EnsureMarkerFile(projectRoot);
            }
            catch
            {
                // Best-effort only; never break caller flow.
            }
        }

        public static bool TryResolveExistingProjectContext(out string projectRoot, out string dataDirectory)
        {
            projectRoot = string.Empty;
            dataDirectory = string.Empty;

            string? drawingPath = TryGetActiveDrawingPath();
            if (string.IsNullOrWhiteSpace(drawingPath))
            {
                return false;
            }

            try
            {
                PathContext context = ResolveFromDrawingPath(drawingPath, ensureExists: false);
                string markerPath = Path.Combine(context.RuntimeRoot, MarkerFileName);
                if (!File.Exists(markerPath))
                {
                    return false;
                }

                projectRoot = context.RuntimeRoot;
                dataDirectory = context.DataDirectory;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string InitializeProjectStructure(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            }

            string normalizedRoot = Path.GetFullPath(projectRoot);
            string drawingsDirectory = Path.Combine(normalizedRoot, "Drawings");
            string dataDirectory = Path.Combine(normalizedRoot, DataFolderName);
            string logsDirectory = Path.Combine(dataDirectory, LogsFolderName);

            Directory.CreateDirectory(normalizedRoot);
            Directory.CreateDirectory(drawingsDirectory);
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(logsDirectory);
            
            Directory.CreateDirectory(Path.Combine(dataDirectory, "Shared"));
            Directory.CreateDirectory(Path.Combine(dataDirectory, "tender_project"));
            Directory.CreateDirectory(Path.Combine(dataDirectory, "shopdrawing_project"));
            Directory.CreateDirectory(Path.Combine(dataDirectory, "production_project"));
            
            string boqDirectory = Path.Combine(dataDirectory, "BOQ");
            Directory.CreateDirectory(boqDirectory);
            Directory.CreateDirectory(Path.Combine(boqDirectory, "Tender"));
            Directory.CreateDirectory(Path.Combine(boqDirectory, "Shopdrawing"));
            Directory.CreateDirectory(Path.Combine(boqDirectory, "Production"));

            EnsureMarkerFile(normalizedRoot);
            return normalizedRoot;
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

                // DWGTITLED = 0 means the drawing is still Drawing1/Drawing2... and has not been saved yet.
                if (!IsCurrentDrawingNamed())
                {
                    return null;
                }

                string? drawingPath = document.Database?.Filename;
                if (string.IsNullOrWhiteSpace(drawingPath) || !Path.IsPathRooted(drawingPath))
                {
                    return null;
                }

                return Path.GetFullPath(drawingPath);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsCurrentDrawingNamed()
        {
            try
            {
                object value = Application.GetSystemVariable("DWGTITLED");
                return value switch
                {
                    short shortValue => shortValue != 0,
                    int intValue => intValue != 0,
                    _ => true
                };
            }
            catch
            {
                return true;
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


