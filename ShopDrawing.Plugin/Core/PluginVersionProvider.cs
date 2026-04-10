using System;
using System.IO;
using System.Reflection;

namespace ShopDrawing.Plugin.Core
{
    internal static class PluginVersionProvider
    {
        public static string GetCurrentVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            string informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion?
                .Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                int metadataIndex = informationalVersion.IndexOf('+');
                return metadataIndex > 0
                    ? informationalVersion[..metadataIndex].Trim()
                    : informationalVersion;
            }

            string fileVersion = assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()?
                .Version?
                .Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(fileVersion))
            {
                return fileVersion;
            }

            return assembly.GetName().Version?.ToString() ?? "0.0.0";
        }

        public static string GetInstallDirectory()
        {
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(assemblyLocation) ?? AppContext.BaseDirectory;
        }

        public static string GetApplicationPluginsDirectory()
        {
            string installDirectory = GetInstallDirectory();
            DirectoryInfo? current = new DirectoryInfo(installDirectory);
            while (current != null)
            {
                if (current.Name.Equals("ApplicationPlugins", StringComparison.OrdinalIgnoreCase))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk",
                "ApplicationPlugins");
        }
    }
}
