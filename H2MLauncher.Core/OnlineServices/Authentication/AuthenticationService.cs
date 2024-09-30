using System.Net.Http.Json;

using Flurl;

using H2MLauncher.Core.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.OnlineServices.Authentication;

public sealed class AuthenticationService
{
    private readonly IOptions<MatchmakingSettings> _options;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ClientContext _clientContext;

    public AuthenticationService(
        HttpClient httpClient,
        ClientContext clientContext,
        IOptions<MatchmakingSettings> options,
        ILogger<AuthenticationService> logger)
    {
        _httpClient = httpClient;
        _clientContext = clientContext;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Performs authentication against the matchmaking server, using the client id and player name.
    /// </summary>
    /// <returns>The access token, if the authentication was successful.</returns>
    public async Task<string?> LoginAsync()
    {
        try
        {
            string loginUrl = _options.Value.MatchmakingServerUrl
                .AppendPathSegment("login")
                .SetQueryParams(new
                {
                    uid = _clientContext.ClientId,
                    playerName = _clientContext.PlayerName,
                });

            HttpResponseMessage response = await _httpClient.GetAsync(loginUrl);

            _clientContext.UpdateToken(await response.Content.ReadFromJsonAsync<BearerToken>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
        }

        return _clientContext.AccessToken;
    }
}
