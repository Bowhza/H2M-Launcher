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

                client.BaseAddress = Url.Parse(matchmakingSettings.MatchmakingServerApiUrl).ToUri();
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
