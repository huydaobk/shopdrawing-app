using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ShopDrawing.Installer;

internal static class Program
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        try
        {
            InstallerArguments options = InstallerArguments.Parse(args);
            if (options.TargetProcessId > 0)
            {
                await WaitForTargetProcessExitAsync(options.TargetProcessId).ConfigureAwait(false);
            }

            string tempRoot = Path.Combine(Path.GetTempPath(), "ShopDrawingInstaller", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            string bundleZip = await ResolveBundleZipAsync(options, tempRoot).ConfigureAwait(false);
            string extractPath = Path.Combine(tempRoot, "extract");
            ZipFile.ExtractToDirectory(bundleZip, extractPath, overwriteFiles: true);

            string bundleSource = Path.Combine(extractPath, "ShopDrawing.bundle");
            if (!Directory.Exists(bundleSource))
            {
                bool looksLikeBundleRoot =
                    File.Exists(Path.Combine(extractPath, "PackageContents.xml")) &&
                    Directory.Exists(Path.Combine(extractPath, "Contents"));

                if (looksLikeBundleRoot)
                {
                    bundleSource = extractPath;
                }
                else
                {
                    throw new InvalidOperationException("Goi cai dat khong chua thu muc ShopDrawing.bundle.");
                }
            }

            string installRoot = options.InstallDirectory;
            Directory.CreateDirectory(installRoot);
            string destinationBundle = Path.Combine(installRoot, "ShopDrawing.bundle");
            string backupPath = Path.Combine(installRoot, "_backup", $"ShopDrawing.bundle_{DateTime.Now:yyyyMMdd_HHmmss}");

            if (Directory.Exists(destinationBundle))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                CopyDirectory(destinationBundle, backupPath);
                Directory.Delete(destinationBundle, recursive: true);
            }

            CopyDirectory(bundleSource, destinationBundle);
            WriteInstallLog(options, destinationBundle, backupPath);

            if (!options.Silent)
            {
                Console.WriteLine("Cai dat hoan tat.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Cai dat that bai: " + ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task<string> ResolveBundleZipAsync(InstallerArguments options, string tempRoot)
    {
        if (!string.IsNullOrWhiteSpace(options.BundleZipPath))
        {
            string candidatePath = options.BundleZipPath;
            if (!Path.IsPathRooted(candidatePath))
            {
                candidatePath = Path.Combine(AppContext.BaseDirectory, candidatePath);
            }

            if (!File.Exists(candidatePath))
            {
                throw new FileNotFoundException("Khong tim thay bundle zip duoc chi dinh.", candidatePath);
            }

            return candidatePath;
        }

        if (!string.IsNullOrWhiteSpace(options.BundleUrl))
        {
            string zipPath = Path.Combine(tempRoot, "ShopDrawing.bundle.zip");
            using HttpResponseMessage response = await HttpClient.GetAsync(options.BundleUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using FileStream outputStream = new(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await contentStream.CopyToAsync(outputStream).ConfigureAwait(false);
            return zipPath;
        }

        string adjacentZip = Path.Combine(AppContext.BaseDirectory, "ShopDrawing.bundle.zip");
        if (File.Exists(adjacentZip))
        {
            return adjacentZip;
        }

        throw new InvalidOperationException("Khong tim thay bundle zip de cai dat.");
    }

    private static async Task WaitForTargetProcessExitAsync(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            while (!process.HasExited)
            {
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                process.Refresh();
            }
        }
        catch (ArgumentException)
        {
        }
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (string directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourcePath, directory);
            Directory.CreateDirectory(Path.Combine(destinationPath, relative));
        }

        foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourcePath, file);
            string targetFile = Path.Combine(destinationPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile, overwrite: true);
        }
    }

    private static void WriteInstallLog(InstallerArguments options, string destinationBundle, string backupPath)
    {
        string logPath = Path.Combine(options.InstallDirectory, "shopdrawing_installer.log");
        StringBuilder builder = new();
        builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Installed {options.Version}");
        builder.AppendLine($"Destination: {destinationBundle}");
        if (!string.IsNullOrWhiteSpace(options.BundleUrl))
        {
            builder.AppendLine($"BundleUrl: {options.BundleUrl}");
        }
        if (!string.IsNullOrWhiteSpace(options.BundleZipPath))
        {
            builder.AppendLine($"BundleZip: {options.BundleZipPath}");
        }
        if (!string.IsNullOrWhiteSpace(backupPath))
        {
            builder.AppendLine($"Backup: {backupPath}");
        }
        builder.AppendLine();
        File.AppendAllText(logPath, builder.ToString());
    }
}

internal sealed class InstallerArguments
{
    public string BundleUrl { get; private set; } = string.Empty;

    public string BundleZipPath { get; private set; } = string.Empty;

    public string InstallDirectory { get; private set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Autodesk",
        "ApplicationPlugins");

    public int TargetProcessId { get; private set; }

    public string Version { get; private set; } = string.Empty;

    public bool Silent { get; private set; }

    public static InstallerArguments Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Count; i++)
        {
            string key = args[i];
            if (!key.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            string value = i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";
            values[key] = value;
        }

        if (values.Count == 0)
        {
            return LoadFromLocalSettings();
        }

        int targetPid = 0;
        if (values.TryGetValue("--target-pid", out string? pidValue))
        {
            _ = int.TryParse(pidValue, out targetPid);
        }

        return new InstallerArguments
        {
            BundleUrl = values.TryGetValue("--bundle-url", out string? bundleUrl) ? bundleUrl : string.Empty,
            BundleZipPath = values.TryGetValue("--bundle-zip", out string? bundleZip) ? bundleZip : string.Empty,
            InstallDirectory = NormalizeInstallDirectory(values.TryGetValue("--install-dir", out string? installDir) ? installDir : null),
            TargetProcessId = targetPid,
            Version = values.TryGetValue("--version", out string? version) ? version : string.Empty,
            Silent = values.TryGetValue("--silent", out string? silentValue)
                && bool.TryParse(silentValue, out bool silent)
                && silent
        };
    }

    private static InstallerArguments LoadFromLocalSettings()
    {
        string settingsPath = Path.Combine(AppContext.BaseDirectory, "install-settings.json");
        if (!File.Exists(settingsPath))
        {
            return new InstallerArguments();
        }

        string json = File.ReadAllText(settingsPath);
        InstallSettings? settings = JsonSerializer.Deserialize<InstallSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (settings == null)
        {
            return new InstallerArguments();
        }

        return new InstallerArguments
        {
            BundleUrl = settings.BundleUrl ?? string.Empty,
            BundleZipPath = settings.BundleZipPath ?? string.Empty,
            InstallDirectory = NormalizeInstallDirectory(settings.InstallDirectory),
            Version = settings.Version ?? string.Empty,
            Silent = settings.Silent
        };
    }

    private static string NormalizeInstallDirectory(string? installDirectory)
    {
        string defaultRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk",
            "ApplicationPlugins");

        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            return defaultRoot;
        }

        string fullPath = Path.GetFullPath(installDirectory);
        DirectoryInfo? current = new(fullPath);
        while (current != null)
        {
            if (current.Name.Equals("ApplicationPlugins", StringComparison.OrdinalIgnoreCase))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return fullPath;
    }
}

internal sealed class InstallSettings
{
    public string? BundleUrl { get; set; }

    public string? BundleZipPath { get; set; }

    public string? InstallDirectory { get; set; }

    public string? Version { get; set; }

    public bool Silent { get; set; } = true;
}
