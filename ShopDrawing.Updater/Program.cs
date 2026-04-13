using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace ShopDrawing.Updater;

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
            UpdaterArguments options = UpdaterArguments.Parse(args);
            string tempRoot = Path.Combine(Path.GetTempPath(), "ShopDrawingUpdater", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            Console.WriteLine("ShopDrawing Updater");
            Console.WriteLine($"Target version: {options.Version}");
            Console.WriteLine($"Install dir: {options.InstallDirectory}");
            Console.WriteLine("Hay dong AutoCAD de bat dau cap nhat.");

            await WaitForTargetProcessExitAsync(options.TargetProcessId).ConfigureAwait(false);

            string packagePath = Path.Combine(tempRoot, "package.zip");
            string extractPath = Path.Combine(tempRoot, "extract");
            Directory.CreateDirectory(extractPath);

            Console.WriteLine("Dang tai goi cap nhat...");
            await DownloadFileAsync(options.PackageUrl, packagePath).ConfigureAwait(false);

            Console.WriteLine("Dang giai nen...");
            ZipFile.ExtractToDirectory(packagePath, extractPath, overwriteFiles: true);

            string backupRoot = Path.Combine(options.InstallDirectory, "_backup");
            Directory.CreateDirectory(backupRoot);
            string backupPath = Path.Combine(backupRoot, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(backupPath);

            Console.WriteLine("Dang backup ban hien tai...");
            CopyDirectory(options.InstallDirectory, backupPath, file => !file.EndsWith("ShopDrawing.Updater.exe", StringComparison.OrdinalIgnoreCase));

            Console.WriteLine("Dang cap nhat file plugin...");
            CopyDirectory(extractPath, options.InstallDirectory, file => !file.EndsWith("ShopDrawing.Updater.exe", StringComparison.OrdinalIgnoreCase));

            WriteUpdateLog(options, backupPath);

            Console.WriteLine("Cap nhat thanh cong.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Cap nhat that bai: " + ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task DownloadFileAsync(string url, string destinationPath)
    {
        using HttpResponseMessage response = await HttpClient.GetAsync(url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using FileStream outputStream = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await contentStream.CopyToAsync(outputStream).ConfigureAwait(false);
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
            // Process da thoat truoc khi updater bat dau doi.
        }
    }

    private static void CopyDirectory(string sourcePath, string destinationPath, Func<string, bool> fileFilter)
    {
        foreach (string directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourcePath, directory);
            Directory.CreateDirectory(Path.Combine(destinationPath, relative));
        }

        foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            if (!fileFilter(file))
            {
                continue;
            }

            string relative = Path.GetRelativePath(sourcePath, file);
            string targetFile = Path.Combine(destinationPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            CopyFileWithRetry(file, targetFile);
        }
    }

    private static void CopyFileWithRetry(string sourceFile, string targetFile)
    {
        if (FilesEquivalent(sourceFile, targetFile))
        {
            return;
        }

        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                File.Copy(sourceFile, targetFile, overwrite: true);
                return;
            }
            catch (IOException ex) when (IsSharingViolation(ex) && attempt < maxAttempts)
            {
                Thread.Sleep(250 * attempt);
            }
        }

        try
        {
            File.Copy(sourceFile, targetFile, overwrite: true);
        }
        catch (IOException ex) when (IsSharingViolation(ex))
        {
            throw new IOException(
                $"File đang bị khóa: {targetFile}. Hãy đóng toàn bộ AutoCAD rồi cập nhật lại.",
                ex);
        }
    }

    private static bool FilesEquivalent(string sourceFile, string targetFile)
    {
        if (!File.Exists(targetFile))
        {
            return false;
        }

        try
        {
            var sourceInfo = new FileInfo(sourceFile);
            var targetInfo = new FileInfo(targetFile);
            if (sourceInfo.Length != targetInfo.Length)
            {
                return false;
            }

            if (sourceInfo.Length == 0)
            {
                return true;
            }

            byte[] sourceHash = ComputeSha256(sourceFile);
            byte[] targetHash = ComputeSha256(targetFile);
            return sourceHash.AsSpan().SequenceEqual(targetHash);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return SHA256.HashData(stream);
    }

    private static bool IsSharingViolation(IOException exception)
    {
        const int sharingViolation = 32;
        const int lockViolation = 33;
        int code = exception.HResult & 0xFFFF;
        return code == sharingViolation || code == lockViolation;
    }

    private static void WriteUpdateLog(UpdaterArguments options, string backupPath)
    {
        string logPath = Path.Combine(options.InstallDirectory, "shopdrawing_updater.log");
        StringBuilder builder = new();
        builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Updated to {options.Version}");
        builder.AppendLine($"Package: {options.PackageUrl}");
        builder.AppendLine($"Backup: {backupPath}");
        builder.AppendLine();
        File.AppendAllText(logPath, builder.ToString());
    }
}

internal sealed class UpdaterArguments
{
    public string PackageUrl { get; private set; } = string.Empty;

    public string InstallDirectory { get; private set; } = string.Empty;

    public int TargetProcessId { get; private set; }

    public string Version { get; private set; } = string.Empty;

    public static UpdaterArguments Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Count; i += 2)
        {
            string key = args[i];
            string value = i + 1 < args.Count ? args[i + 1] : string.Empty;
            values[key] = value;
        }

        string packageUrl = GetRequired(values, "--package-url");
        string installDirectory = GetRequired(values, "--install-dir");
        string targetPid = GetRequired(values, "--target-pid");

        if (!int.TryParse(targetPid, out int processId) || processId <= 0)
        {
            throw new InvalidOperationException("Gia tri --target-pid khong hop le.");
        }

        return new UpdaterArguments
        {
            PackageUrl = packageUrl,
            InstallDirectory = installDirectory,
            TargetProcessId = processId,
            Version = values.TryGetValue("--version", out string? version) ? version : string.Empty
        };
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Thieu tham so {key}.");
        }

        return value;
    }
}
