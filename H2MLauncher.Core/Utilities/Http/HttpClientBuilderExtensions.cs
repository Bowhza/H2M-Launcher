using Flurl;

using H2MLauncher.Core.Settings;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.Utilities.Http
{
    public static class HttpClientBuilderExtensions
    {
        public static IHttpClientBuilder ConfigureMatchmakingClient(this IHttpClientBuilder httpClientBuilder, bool trimTrailingSlashes = false)
        {
            return httpClientBuilder.ConfigureHttpClient((sp, client) =>
            {
                MatchmakingSettings matchmakingSettings = sp.GetRequiredService<IOptions<MatchmakingSettings>>().Value;

                string apiUrl = trimTrailingSlashes 
                    ? matchmakingSettings.MatchmakingServerApiUrl.TrimEnd('/') 
                    : matchmakingSettings.MatchmakingServerApiUrl;

                client.BaseAddress = Url.Parse(apiUrl).ToUri();
                client.DefaultRequestHeaders.AddApplicationMetadata();
            });
        }

        public static IHttpClientBuilder ConfigureCertificateValidationIgnore(this IHttpClientBuilder httpClientBuilder)
        {
           return httpClientBuilder.ConfigurePrimaryHttpMessageHandler(CustomMessageHandlerFactory);
        }

        public static readonly Func<IServiceProvider, HttpMessageHandler> CustomMessageHandlerFactory = (sp) =>
        {
            MatchmakingSettings matchmakingSettings = sp.GetRequiredService<IOptions<MatchmakingSettings>>().Value;

            return matchmakingSettings.DisableCertificateValidation
                ? new SocketsHttpHandler()
                {
                    SslOptions = new()
                    {
                        RemoteCertificateValidationCallback = delegate { return true; }
                    }
                }
                : new SocketsHttpHandler();
        };
    }
}
