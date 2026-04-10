using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using ShopDrawing.Plugin.UI;

namespace ShopDrawing.Plugin.Core
{
    internal static class PluginUpdateService
    {
        private static readonly object SyncRoot = new();
        private static readonly UpdateManifestClient ManifestClient = new();
        private static readonly Version MinimumSafeInstallerVersion = new(0, 1, 28);

        private static bool _startupScheduled;
        private static bool _idleNotifierRegistered;
        private static bool _startupResultChecked;
        private static UpdateCheckResult? _pendingNotification;

        public static void TryScheduleStartupCheck()
        {
            if (!_startupResultChecked)
            {
                _startupResultChecked = true;
                TryShowInstallerResult();
            }

            lock (SyncRoot)
            {
                if (_startupScheduled)
                {
                    return;
                }

                _startupScheduled = true;
            }

            UpdateChannelOptions options = UpdateChannelOptionsProvider.Load();
            if (!options.CheckOnStartup || string.IsNullOrWhiteSpace(options.ManifestUrl))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    if (options.CheckDelaySeconds > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(options.CheckDelaySeconds)).ConfigureAwait(false);
                    }

                    UpdateCheckResult result = await CheckForUpdatesAsync(CancellationToken.None).ConfigureAwait(false);
                    if (!result.IsUpdateAvailable)
                    {
                        return;
                    }

                    lock (SyncRoot)
                    {
                        _pendingNotification = result;
                        if (_idleNotifierRegistered)
                        {
                            return;
                        }

                        Application.Idle += OnIdleNotifyUpdate;
                        _idleNotifierRegistered = true;
                    }
                }
                catch (Exception ex)
                {
                    PluginLogger.Warn("Suppressed exception: " + ex.Message);
                }
            });
        }

        public static void BeginInteractiveCheck()
        {
            _ = Task.Run(async () =>
            {
                UpdateCheckResult result = await CheckForUpdatesAsync(CancellationToken.None).ConfigureAwait(false);

                lock (SyncRoot)
                {
                    _pendingNotification = result;
                    if (_idleNotifierRegistered)
                    {
                        return;
                    }

                    Application.Idle += OnIdleNotifyUpdate;
                    _idleNotifierRegistered = true;
                }
            });
        }

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            string currentVersion = PluginVersionProvider.GetCurrentVersion();
            UpdateChannelOptions options = UpdateChannelOptionsProvider.Load();
            if (string.IsNullOrWhiteSpace(options.ManifestUrl))
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = currentVersion,
                    ChannelName = options.ChannelName,
                    ErrorMessage = "Chua cau hinh manifest update.",
                    IsConfigured = false
                };
            }

            try
            {
                UpdateManifest? manifest = await ManifestClient.GetManifestAsync(options.ManifestUrl, cancellationToken).ConfigureAwait(false);
                if (manifest == null)
                {
                    return new UpdateCheckResult
                    {
                        CurrentVersion = currentVersion,
                        ChannelName = options.ChannelName,
                        ErrorMessage = "Khong doc duoc manifest update.",
                        IsConfigured = true
                    };
                }

                string latestVersion = manifest.Version?.Trim() ?? string.Empty;

                return new UpdateCheckResult
                {
                    CurrentVersion = currentVersion,
                    LatestVersion = latestVersion,
                    InstallerUrl = manifest.InstallerUrl?.Trim() ?? string.Empty,
                    PackageUrl = manifest.PackageUrl?.Trim() ?? string.Empty,
                    Notes = manifest.Notes?.Trim() ?? string.Empty,
                    IsMandatory = manifest.Mandatory,
                    IsConfigured = true,
                    IsUpdateAvailable = VersionComparer.IsNewer(latestVersion, currentVersion),
                    ChannelName = string.IsNullOrWhiteSpace(manifest.ChannelName) ? options.ChannelName : manifest.ChannelName
                };
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
                return new UpdateCheckResult
                {
                    CurrentVersion = currentVersion,
                    ChannelName = options.ChannelName,
                    ErrorMessage = ex.Message,
                    IsConfigured = true
                };
            }
        }

        public static bool TryLaunchUpdater(UpdateCheckResult result)
        {
            try
            {
                string pluginDirectory = PluginVersionProvider.GetInstallDirectory();
                string installerPath = Path.Combine(pluginDirectory, "ShopDrawing.Installer.exe");
                bool hasInstaller = File.Exists(installerPath);
                bool mustDownloadBeforeLaunch = !hasInstaller || IsLegacyInstaller(installerPath);
                if (mustDownloadBeforeLaunch)
                {
                    if (!TryDownloadInstaller(result.InstallerUrl, installerPath))
                    {
                        UiFeedback.ShowWarning("Khong tim thay ShopDrawing.Installer.exe trong thu muc cai dat.");
                        return false;
                    }
                }
                else if (ShouldRefreshInstallerInBackground(installerPath, result))
                {
                    _ = Task.Run(() => TryDownloadInstaller(result.InstallerUrl, installerPath));
                }

                if (string.IsNullOrWhiteSpace(result.PackageUrl))
                {
                    UiFeedback.ShowWarning("Manifest khong co packageUrl hop le.");
                    return false;
                }

                ProcessStartInfo startInfo = new()
                {
                    FileName = installerPath,
                    WorkingDirectory = pluginDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = BuildUpdaterArguments(result)
                };

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                UiFeedback.ShowError("Khong the mo updater: " + ex.Message);
                return false;
            }
        }

        private static bool TryDownloadInstaller(string? installerUrl, string installerPath)
        {
            if (string.IsNullOrWhiteSpace(installerUrl))
            {
                return false;
            }

            try
            {
                using var client = new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(2)
                };

                byte[] payload = client.GetByteArrayAsync(installerUrl).GetAwaiter().GetResult();
                Directory.CreateDirectory(Path.GetDirectoryName(installerPath) ?? AppContext.BaseDirectory);
                File.WriteAllBytes(installerPath, payload);
                return true;
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
                return false;
            }
        }

        private static string BuildUpdaterArguments(UpdateCheckResult result)
        {
            StringBuilder builder = new();
            AppendArgument(builder, "--bundle-url", result.PackageUrl);
            AppendArgument(builder, "--install-dir", PluginVersionProvider.GetApplicationPluginsDirectory());
            AppendArgument(builder, "--target-pid", Process.GetCurrentProcess().Id.ToString());
            AppendArgument(builder, "--version", result.LatestVersion);
            AppendArgument(builder, "--silent", "true");
            AppendArgument(builder, "--notify", "true");
            return builder.ToString();
        }

        private static void AppendArgument(StringBuilder builder, string name, string value)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(name);
            builder.Append(' ');
            builder.Append('"');
            builder.Append(value.Replace("\"", "\\\""));
            builder.Append('"');
        }

        private static bool IsLegacyInstaller(string installerPath)
        {
            if (!TryGetInstallerVersion(installerPath, out Version? installedVersion) || installedVersion == null)
            {
                return true;
            }

            return installedVersion < MinimumSafeInstallerVersion;
        }

        private static bool ShouldRefreshInstallerInBackground(string installerPath, UpdateCheckResult result)
        {
            if (string.IsNullOrWhiteSpace(result.InstallerUrl))
            {
                return false;
            }

            if (!TryGetInstallerVersion(installerPath, out Version? installedVersion) || installedVersion == null)
            {
                return false;
            }

            if (!VersionComparer.TryParse(result.LatestVersion, out Version? latestVersion) || latestVersion == null)
            {
                return false;
            }

            return latestVersion > installedVersion;
        }

        private static bool TryGetInstallerVersion(string installerPath, out Version? version)
        {
            version = null;

            try
            {
                string fileVersion = FileVersionInfo.GetVersionInfo(installerPath).FileVersion ?? string.Empty;
                return VersionComparer.TryParse(fileVersion, out version);
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
                return false;
            }
        }

        private static void OnIdleNotifyUpdate(object? sender, EventArgs e)
        {
            UpdateCheckResult? result;
            lock (SyncRoot)
            {
                result = _pendingNotification;
                _pendingNotification = null;
                Application.Idle -= OnIdleNotifyUpdate;
                _idleNotifierRegistered = false;
            }

            if (result == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                UiFeedback.ShowWarning(
                    $"Khong kiem tra duoc cap nhat.\nPhien ban hien tai: {FormatVersionForDisplay(result.CurrentVersion)}\nChi tiet: {result.ErrorMessage}",
                    "ShopDrawing Update");
                return;
            }

            if (!result.IsConfigured)
            {
                UiFeedback.ShowInfo("Chua cau hinh kenh cap nhat cho plugin.", "ShopDrawing Update");
                return;
            }

            if (!result.IsUpdateAvailable)
            {
                UiFeedback.ShowInfo(
                    $"Dang o ban moi nhat.\nPhien ban hien tai: {FormatVersionForDisplay(result.CurrentVersion)}",
                    "ShopDrawing Update");
                return;
            }

            string message = BuildUpdateMessage(result);
            if (UiFeedback.AskYesNo(message, "ShopDrawing Update") != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            if (TryLaunchUpdater(result))
            {
                UiFeedback.ShowInfo("Installer da mo. Hay dong AutoCAD de cap nhat.");
            }
        }

        private static string BuildUpdateMessage(UpdateCheckResult result)
        {
            StringBuilder builder = new();
            builder.AppendLine("Co ban cap nhat moi cho ShopDrawing.");
            builder.AppendLine($"Hien tai: {FormatVersionForDisplay(result.CurrentVersion)}");
            builder.AppendLine($"Moi nhat: {FormatVersionForDisplay(result.LatestVersion)}");

            if (!string.IsNullOrWhiteSpace(result.Notes))
            {
                builder.AppendLine();
                builder.AppendLine("Noi dung:");
                builder.AppendLine(result.Notes);
            }

            builder.AppendLine();
            builder.AppendLine("Mo updater ngay bay gio?");
            return builder.ToString();
        }

        private static string FormatVersionForDisplay(string? version)
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

        private static void TryShowInstallerResult()
        {
            try
            {
                string markerPath = Path.Combine(
                    PluginVersionProvider.GetApplicationPluginsDirectory(),
                    "shopdrawing_update_result.json");

                if (!File.Exists(markerPath))
                {
                    return;
                }

                string json = File.ReadAllText(markerPath);
                UpdateResultMarker? marker = JsonSerializer.Deserialize<UpdateResultMarker>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                File.Delete(markerPath);

                if (marker == null)
                {
                    return;
                }

                if (marker.Success)
                {
                    UiFeedback.ShowInfo(
                        $"Cap nhat ShopDrawing thanh cong.\nVersion: {FormatVersionForDisplay(marker.Version)}",
                        "ShopDrawing Update");
                }
                else
                {
                    string detail = string.IsNullOrWhiteSpace(marker.Message)
                        ? "Khong ro nguyen nhan."
                        : marker.Message.Trim();

                    UiFeedback.ShowWarning(
                        $"Cap nhat ShopDrawing that bai.\nChi tiet: {detail}",
                        "ShopDrawing Update");
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Warn("Suppressed exception: " + ex.Message);
            }
        }

        private sealed class UpdateResultMarker
        {
            public bool Success { get; set; }

            public string Version { get; set; } = string.Empty;

            public string Message { get; set; } = string.Empty;
        }
    }
}
