using System;
using System.Net.Http;
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
            using HttpResponseMessage response = await HttpClient.GetAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<UpdateManifest>(responseStream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
    }
}
