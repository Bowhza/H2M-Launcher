using System.Net.Http.Json;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.DependencyInjection;

namespace H2MLauncher.Core.Services
{
    public class HMWMasterService(IServiceScopeFactory serviceScopeFactory, IErrorHandlingService errorHandlingService, IHttpClientFactory httpClientFactory)
    {
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService;
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public async IAsyncEnumerable<string> FetchServersAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            HttpClient httpClient = _httpClientFactory.CreateClient(nameof(HMWMasterService));
            try
            {
                response = await httpClient.GetAsync("game-servers", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    yield break;
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to fetch the HMW servers details at this time. Please try again later.");
                yield break;
            }

            IAsyncEnumerable<string?> addresses = response.Content.ReadFromJsonAsAsyncEnumerable<string>(cancellationToken);
            await foreach (string? address in addresses.ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(address))
                {
                    yield return address!;
                }
            }
        }
    }
}
