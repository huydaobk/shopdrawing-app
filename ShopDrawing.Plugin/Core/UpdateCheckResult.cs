namespace ShopDrawing.Plugin.Core
{
    internal sealed class UpdateCheckResult
    {
        public bool IsConfigured { get; init; }

        public bool IsUpdateAvailable { get; init; }

        public bool IsMandatory { get; init; }

        public string CurrentVersion { get; init; } = string.Empty;

        public string LatestVersion { get; init; } = string.Empty;

        public string InstallerUrl { get; init; } = string.Empty;

        public string PackageUrl { get; init; } = string.Empty;

        public string Notes { get; init; } = string.Empty;

        public string ChannelName { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;
    }
}
