using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ShopDrawing.Installer;

internal static class Program
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    private const string UpdateResultFileName = "shopdrawing_update_result.json";

    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        InstallerArguments? options = null;
        try
        {
            options = InstallerArguments.Parse(args);
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

            string destinationBundle = InstallBundleSafely(bundleSource, options.InstallDirectory, out string backupPath);
            WriteInstallLog(options, destinationBundle, backupPath);
            WriteUpdateResult(options.InstallDirectory, true, options.Version, "Installed successfully.");
            ShowDesktopNotification(
                options.Notify,
                success: true,
                version: options.Version,
                message: "Cap nhat ShopDrawing thanh cong.\nBan co the mo lai AutoCAD.");

            if (!options.Silent)
            {
                Console.WriteLine("Cai dat hoan tat.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            string installRoot = options?.InstallDirectory ?? InstallerArguments.GetDefaultInstallDirectory();
            WriteInstallFailureLog(
                installRoot,
                options?.Version ?? string.Empty,
                options?.BundleUrl ?? string.Empty,
                options?.BundleZipPath ?? string.Empty,
                ex.Message);
            WriteUpdateResult(installRoot, false, options?.Version ?? string.Empty, ex.Message);
            ShowDesktopNotification(
                options?.Notify == true,
                success: false,
                version: options?.Version ?? string.Empty,
                message: $"Cap nhat ShopDrawing that bai.\nChi tiet: {ex.Message}");
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
        CopyDirectory(sourcePath, destinationPath, ignoreLockedInstaller: false, ignoreMissingSource: false);
    }

    private static void CopyDirectory(
        string sourcePath,
        string destinationPath,
        bool ignoreLockedInstaller,
        bool ignoreMissingSource)
    {
        if (!Directory.Exists(sourcePath))
        {
            if (ignoreMissingSource)
            {
                return;
            }

            throw new DirectoryNotFoundException($"Khong tim thay thu muc: {sourcePath}");
        }

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
            try
            {
                File.Copy(file, targetFile, overwrite: true);
            }
            catch (UnauthorizedAccessException) when (ignoreLockedInstaller && IsInstallerFile(targetFile))
            {
            }
            catch (IOException) when (ignoreLockedInstaller && IsInstallerFile(targetFile))
            {
            }
        }
    }

    private static string InstallBundleSafely(string bundleSource, string installRoot, out string backupPath)
    {
        Directory.CreateDirectory(installRoot);

        string destinationBundle = Path.Combine(installRoot, "ShopDrawing.bundle");
        string stagingBundle = Path.Combine(
            installRoot,
            "_staging",
            $"ShopDrawing.bundle_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}");

        backupPath = string.Empty;

        try
        {
            CopyDirectory(bundleSource, stagingBundle);
            ValidateStagedBundle(stagingBundle);

            if (Directory.Exists(destinationBundle))
            {
                backupPath = Path.Combine(installRoot, "_backup", $"ShopDrawing.bundle_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                // Best-effort backup: installer file can be locked by current process.
                CopyDirectory(destinationBundle, backupPath, ignoreLockedInstaller: true, ignoreMissingSource: true);
            }

            // Merge staged files into destination to avoid deleting locked installer executable.
            CopyDirectory(stagingBundle, destinationBundle, ignoreLockedInstaller: true, ignoreMissingSource: false);
            return destinationBundle;
        }
        catch
        {
            TryDeleteDirectory(stagingBundle);

            if (!string.IsNullOrWhiteSpace(backupPath) &&
                Directory.Exists(backupPath) &&
                !Directory.Exists(destinationBundle))
            {
                try
                {
                    CopyDirectory(backupPath, destinationBundle);
                }
                catch
                {
                }
            }

            throw;
        }
        finally
        {
            TryDeleteDirectory(stagingBundle);
        }
    }

    private static bool IsInstallerFile(string path)
    {
        return string.Equals(
            Path.GetFileName(path),
            "ShopDrawing.Installer.exe",
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateStagedBundle(string stagedBundlePath)
    {
        string packageContentsPath = Path.Combine(stagedBundlePath, "PackageContents.xml");
        if (!File.Exists(packageContentsPath))
        {
            throw new InvalidOperationException("Bundle moi thieu PackageContents.xml.");
        }

        string pluginDllPath = Path.Combine(stagedBundlePath, "Contents", "Windows", "ShopDrawing.Plugin.dll");
        if (!File.Exists(pluginDllPath))
        {
            throw new InvalidOperationException("Bundle moi thieu ShopDrawing.Plugin.dll.");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static void WriteInstallLog(InstallerArguments options, string destinationBundle, string backupPath)
    {
        string logPath = Path.Combine(options.InstallDirectory, "shopdrawing_installer.log");
        StringBuilder builder = new();
        builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Installed {options.Version}");
        builder.AppendLine("Result: SUCCESS");
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

    private static void WriteInstallFailureLog(
        string installDirectory,
        string version,
        string bundleUrl,
        string bundleZipPath,
        string errorMessage)
    {
        string logPath = Path.Combine(installDirectory, "shopdrawing_installer.log");
        StringBuilder builder = new();
        builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Installed {version}");
        builder.AppendLine("Result: FAILED");
        if (!string.IsNullOrWhiteSpace(bundleUrl))
        {
            builder.AppendLine($"BundleUrl: {bundleUrl}");
        }
        if (!string.IsNullOrWhiteSpace(bundleZipPath))
        {
            builder.AppendLine($"BundleZip: {bundleZipPath}");
        }
        builder.AppendLine($"Error: {errorMessage}");
        builder.AppendLine();
        File.AppendAllText(logPath, builder.ToString());
    }

    private static void WriteUpdateResult(string installDirectory, bool success, string version, string message)
    {
        try
        {
            string markerPath = Path.Combine(installDirectory, UpdateResultFileName);
            var payload = new UpdateResultPayload
            {
                Success = success,
                Version = version,
                Message = message,
                Timestamp = DateTimeOffset.Now
            };

            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(markerPath, json, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static void ShowDesktopNotification(bool enabled, bool success, string version, string message)
    {
        if (!enabled)
        {
            return;
        }

        try
        {
            string normalizedVersion = NormalizeVersion(version);
            string title = success
                ? "ShopDrawing Update"
                : "ShopDrawing Update Error";
            string body = success
                ? $"{message}\nVersion: {normalizedVersion}"
                : message;
            uint type = success ? 0x00000040u : 0x00000010u; // MB_ICONINFORMATION / MB_ICONERROR
            _ = MessageBoxW(IntPtr.Zero, body, title, type);
        }
        catch
        {
        }
    }

    private static string NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "0.0.0";
        }

        string trimmed = version.Trim();
        int metadataIndex = trimmed.IndexOf('+');
        return metadataIndex > 0
            ? trimmed[..metadataIndex].Trim()
            : trimmed;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
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

    public bool Notify { get; private set; }

    public static string GetDefaultInstallDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk",
            "ApplicationPlugins");
    }

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
                && silent,
            Notify = values.TryGetValue("--notify", out string? notifyValue)
                && bool.TryParse(notifyValue, out bool notify)
                && notify
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
            Silent = settings.Silent,
            Notify = settings.Notify
        };
    }

    private static string NormalizeInstallDirectory(string? installDirectory)
    {
        string defaultRoot = GetDefaultInstallDirectory();

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

    public bool Notify { get; set; } = false;
}

internal sealed class UpdateResultPayload
{
    public bool Success { get; set; }

    public string Version { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }
}
