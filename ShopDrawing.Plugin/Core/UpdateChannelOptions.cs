namespace ShopDrawing.Plugin.Core
{
    internal sealed class UpdateChannelOptions
    {
        public string ManifestUrl { get; set; } = string.Empty;

        public bool CheckOnStartup { get; set; } = true;

        public int CheckDelaySeconds { get; set; } = 8;

        public string ChannelName { get; set; } = "stable";
    }
}
