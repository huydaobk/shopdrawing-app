namespace ShopDrawing.Plugin.Core
{
    internal sealed class UpdateManifest
    {
        public string Version { get; set; } = string.Empty;

        public string InstallerUrl { get; set; } = string.Empty;

        public string PackageUrl { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public bool Mandatory { get; set; }

        public string ReleaseDate { get; set; } = string.Empty;

        public string ChannelName { get; set; } = "stable";
    }
}
