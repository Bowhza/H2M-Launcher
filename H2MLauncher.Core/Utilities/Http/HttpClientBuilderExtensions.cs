using Flurl;

using H2MLauncher.Core.Settings;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.Utilities.Http
{
    public static class HttpClientBuilderExtensions
    {
        public static IHttpClientBuilder ConfigureMatchmakingClient(this IHttpClientBuilder httpClientBuilder)
        {
            return httpClientBuilder.ConfigureHttpClient((sp, client) =>
            {
                MatchmakingSettings matchmakingSettings = sp.GetRequiredService<IOptions<MatchmakingSettings>>().Value;

                client.BaseAddress = Url.Parse(matchmakingSettings.MatchmakingServerUrl).ToUri();
                client.DefaultRequestHeaders.AddApplicationMetadata();
            });
        }
    }
}
