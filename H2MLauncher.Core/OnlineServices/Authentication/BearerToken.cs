using System.Text.Json.Serialization;

namespace H2MLauncher.Core.OnlineServices.Authentication;

public sealed record BearerToken
{
    private int _expiresInSeconds;

    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refreshToken")]
    public required string RefreshToken { get; init; }

    [JsonPropertyName("expiresIn")]
    public required int ExpiresInSeconds
    {
        get => _expiresInSeconds;
        init
        {
            _expiresInSeconds = value;
            ExpirationDate = DateTimeOffset.Now.AddSeconds(value);
        }
    }

    public DateTimeOffset ExpirationDate { get; init; }
}
