using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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

        private static bool _startupScheduled;
        private static bool _idleNotifierRegistered;
        private static UpdateCheckResult? _pendingNotification;

        public static void TryScheduleStartupCheck()
        {
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
                string installDirectory = PluginVersionProvider.GetInstallDirectory();
                string installerPath = Path.Combine(installDirectory, "ShopDrawing.Installer.exe");
                if (!File.Exists(installerPath))
                {
                    UiFeedback.ShowWarning("Khong tim thay ShopDrawing.Installer.exe trong thu muc cai dat.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(result.PackageUrl))
                {
                    UiFeedback.ShowWarning("Manifest khong co packageUrl hop le.");
                    return false;
                }

                ProcessStartInfo startInfo = new()
                {
                    FileName = installerPath,
                    WorkingDirectory = installDirectory,
                    UseShellExecute = false,
                    Arguments = BuildUpdaterArguments(result, installDirectory)
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

        private static string BuildUpdaterArguments(UpdateCheckResult result, string installDirectory)
        {
            StringBuilder builder = new();
            AppendArgument(builder, "--bundle-url", result.PackageUrl);
            AppendArgument(builder, "--install-dir", installDirectory);
            AppendArgument(builder, "--target-pid", Process.GetCurrentProcess().Id.ToString());
            AppendArgument(builder, "--version", result.LatestVersion);
            AppendArgument(builder, "--silent", "true");
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
    }
}
