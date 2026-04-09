using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;

namespace ShopDrawing.Plugin.Core
{
    internal static class PlotStyleInstaller
    {
        public const string DefaultPlotStyleName = "SD_Black.ctb";
        public const string FallbackPlotStyleName = "monochrome.ctb";

        public static bool EnsureInstalled(Document? doc = null)
        {
            try
            {
                string? plotStylesDir = TryGetPlotStylesDirectory();
                if (string.IsNullOrWhiteSpace(plotStylesDir))
                    return false;

                Directory.CreateDirectory(plotStylesDir);

                string destinationPath = Path.Combine(plotStylesDir, DefaultPlotStyleName);
                string? sourcePath = GetPreferredSourcePath(plotStylesDir);
                if (string.IsNullOrWhiteSpace(sourcePath))
                    return File.Exists(destinationPath);

                if (NeedsCopy(sourcePath, destinationPath))
                    File.Copy(sourcePath, destinationPath, true);

                return File.Exists(destinationPath);
            }
            catch (Exception ex)
            {
                try
                {
                    doc?.Editor?.WriteMessage($"\n[SD] PlotStyle install: {ex.Message}");
                }
                catch (System.Exception innerEx)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in PlotStyleInstaller.cs", innerEx);
            }

                return false;
            }
        }

        public static string ResolvePreferredStyleName(IEnumerable<string>? availableStyles, string? requestedStyleName = null)
        {
            var styles = new HashSet<string>(
                availableStyles ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(requestedStyleName) && styles.Contains(requestedStyleName))
                return requestedStyleName;

            if (styles.Contains(DefaultPlotStyleName))
                return DefaultPlotStyleName;

            if (styles.Contains(FallbackPlotStyleName))
                return FallbackPlotStyleName;

            return !string.IsNullOrWhiteSpace(requestedStyleName)
                ? requestedStyleName
                : DefaultPlotStyleName;
        }

        internal static string? TryGetPlotStylesDirectory()
        {
            try
            {
                string? roamingRoot = Application.GetSystemVariable("ROAMABLEROOTPREFIX")?.ToString();
                if (!string.IsNullOrWhiteSpace(roamingRoot))
                {
                    string candidate = Path.Combine(roamingRoot, "Plotters", "Plot Styles");
                    if (Directory.Exists(candidate) || Directory.Exists(Path.Combine(roamingRoot, "Plotters")))
                        return candidate;
                }
            }
            catch (System.Exception innerEx)
            {
                ShopDrawing.Plugin.Core.PluginLogger.Error("Suppressed exception in PlotStyleInstaller.cs", innerEx);
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return FindPlotStylesDirectoryFromAppData(appData);
        }

        internal static string? FindPlotStylesDirectoryFromAppData(string appDataRoot)
        {
            if (string.IsNullOrWhiteSpace(appDataRoot) || !Directory.Exists(appDataRoot))
                return null;

            string autodeskRoot = Path.Combine(appDataRoot, "Autodesk");
            if (!Directory.Exists(autodeskRoot))
                return null;

            return Directory.EnumerateDirectories(autodeskRoot, "AutoCAD *", SearchOption.TopDirectoryOnly)
                .SelectMany(acadRoot => Directory.EnumerateDirectories(acadRoot, "R*", SearchOption.TopDirectoryOnly))
                .SelectMany(versionRoot => Directory.EnumerateDirectories(versionRoot, "*", SearchOption.TopDirectoryOnly))
                .Select(profileRoot => Path.Combine(profileRoot, "Plotters", "Plot Styles"))
                .Where(Directory.Exists)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        internal static string GetBundledPlotStylePath()
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? AppContext.BaseDirectory;
            return Path.Combine(assemblyDir, "Resources", "PlotStyles", DefaultPlotStyleName);
        }

        internal static string? GetPreferredSourcePath(string plotStylesDir)
        {
            string bundledPath = GetBundledPlotStylePath();
            if (File.Exists(bundledPath))
                return bundledPath;

            string fallbackPath = Path.Combine(plotStylesDir, FallbackPlotStyleName);
            return File.Exists(fallbackPath) ? fallbackPath : null;
        }

        internal static bool NeedsCopy(string sourcePath, string destinationPath)
        {
            if (!File.Exists(destinationPath))
                return true;

            var source = new FileInfo(sourcePath);
            var destination = new FileInfo(destinationPath);
            return source.Length != destination.Length
                || source.LastWriteTimeUtc != destination.LastWriteTimeUtc;
        }
    }
}
