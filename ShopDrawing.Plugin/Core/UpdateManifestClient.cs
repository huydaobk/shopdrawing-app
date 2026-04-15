using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShopDrawing.Plugin.Core
{
    internal sealed class UpdateManifestClient
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<UpdateManifest?> GetManifestAsync(string manifestUrl, CancellationToken cancellationToken)
        {
            if (IsGitHubLatestReleaseApi(manifestUrl))
            {
                return await GetManifestFromGitHubReleaseApiAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
            }

            using HttpRequestMessage request = CreateNoCacheRequest(manifestUrl);
            using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<UpdateManifest>(responseStream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        private static bool IsGitHubLatestReleaseApi(string manifestUrl)
        {
            return manifestUrl.StartsWith("https://api.github.com/", StringComparison.OrdinalIgnoreCase)
                && manifestUrl.Contains("/releases/latest", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<UpdateManifest?> GetManifestFromGitHubReleaseApiAsync(string manifestUrl, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = CreateNoCacheRequest(manifestUrl);
            request.Headers.UserAgent.ParseAdd("ShopDrawing-Updater/1.0");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);

            JsonElement root = document.RootElement;
            string tagName = GetString(root, "tag_name");
            string version = tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? tagName[1..]
                : tagName;

            JsonElement[] assets = root.TryGetProperty("assets", out JsonElement assetsElement) &&
                                   assetsElement.ValueKind == JsonValueKind.Array
                ? assetsElement.EnumerateArray().ToArray()
                : Array.Empty<JsonElement>();

            string installerUrl = FindAssetUrl(assets, "ShopDrawing.Installer.exe");
            string packageUrl = FindAssetUrl(assets, "ShopDrawing.bundle.zip");

            return new UpdateManifest
            {
                Version = version,
                InstallerUrl = installerUrl,
                PackageUrl = packageUrl,
                Notes = GetString(root, "name"),
                Mandatory = false,
                ReleaseDate = GetString(root, "published_at"),
                ChannelName = "stable"
            };
        }

        private static string FindAssetUrl(JsonElement[] assets, string assetName)
        {
            foreach (JsonElement asset in assets)
            {
                string name = GetString(asset, "name");
                if (!string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string url = GetString(asset, "browser_download_url");
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }

            return string.Empty;
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value) ||
                value.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return value.GetString()?.Trim() ?? string.Empty;
        }

        private static HttpRequestMessage CreateNoCacheRequest(string manifestUrl)
        {
            string requestedUrl = AppendCacheBuster(manifestUrl);
            HttpRequestMessage request = new(HttpMethod.Get, requestedUrl);
            request.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true,
                MustRevalidate = true
            };
            request.Headers.Pragma.ParseAdd("no-cache");
            return request;
        }

        private static string AppendCacheBuster(string manifestUrl)
        {
            if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out Uri? uri))
            {
                return manifestUrl;
            }

            string queryPrefix = string.IsNullOrWhiteSpace(uri.Query)
                ? string.Empty
                : uri.Query.TrimStart('?') + "&";
            string stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

            UriBuilder builder = new(uri)
            {
                Query = $"{queryPrefix}_ts={stamp}"
            };

            return builder.Uri.ToString();
        }
    }
}
