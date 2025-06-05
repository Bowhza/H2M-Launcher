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
    private readonly RsaKeyManager _rsaKeyManager;

    public AuthenticationService(
        HttpClient httpClient,
        ClientContext clientContext,
        RsaKeyManager rsaKeyManager,
        IOptions<MatchmakingSettings> options,
        ILogger<AuthenticationService> logger)
    {
        _httpClient = httpClient;
        _clientContext = clientContext;
        _rsaKeyManager = rsaKeyManager;
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
            _logger.LogDebug("Authenticating with passwordless auth...");

            string challengeUrl = _options.Value.MatchmakingServerApiUrl
                .AppendPathSegment("auth/challenge");

            ChallengeResponse? challengeResponse = await _httpClient.GetFromJsonAsync<ChallengeResponse>(challengeUrl);

            if (challengeResponse is null)
            {
                _logger.LogWarning("Could not request challenge from {challengeUrl}", challengeUrl);
                return null;
            }

            if (!_rsaKeyManager.IsKeyLoaded)
            {
                _rsaKeyManager.LoadOrCreateKey();
            }

            string signature = _rsaKeyManager.SignChallenge(challengeResponse.Nonce);
            string publicKey = _rsaKeyManager.GetPublicKeySpkiBase64();

            var loginRespone = await _httpClient.PostAsync("auth/login", JsonContent.Create(new
            {
                ChallengeId = challengeResponse.ChallengeId,
                PublicKey = publicKey,
                Signature = signature,
            }));

            if (!loginRespone.IsSuccessStatusCode)
            {
                _logger.LogError("Could not complete login: Server responded with {statusCode}", loginRespone.StatusCode);
                return null;
            }

            _clientContext.UpdateToken(await loginRespone.Content.ReadFromJsonAsync<BearerToken>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
        }

        return _clientContext.AccessToken;
    }
}
