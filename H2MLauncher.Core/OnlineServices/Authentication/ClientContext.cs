using System.Diagnostics.CodeAnalysis;

using H2MLauncher.Core.Game;

namespace H2MLauncher.Core.OnlineServices.Authentication;

/// <summary>
/// Provides context for the online service clients, such as the unique client id or access token.
/// </summary>
public sealed class ClientContext : IDisposable
{
    private readonly IPlayerNameProvider _playerNameProvider;
    private BearerToken? _token;

    public ClientContext(IPlayerNameProvider playerNameProvider)
    {
        _playerNameProvider = playerNameProvider;
        _playerNameProvider.PlayerNameChanged += OnPlayerNameChanged;
    }

    public string ClientId { get; } = Guid.NewGuid().ToString();
    public string PlayerName => _playerNameProvider.PlayerName;
    

    public string? AccessToken => _token?.AccessToken;

    [MemberNotNullWhen(true, nameof(AccessToken))]
    public bool IsAuthenticated => _token is not null && _token.ExpirationDate > DateTimeOffset.Now;

    public void InvalidateToken()
    {
        _token = null;
    }

    public void UpdateToken(BearerToken? token)
    {
        _token = token;
    }

    private void OnPlayerNameChanged(string oldName, string newName)
    {
        // since the player name is stored in the token, we need to invalidate it
        // to require reauthentication for the next use.
        InvalidateToken();
    }

    public void Dispose()
    {
        _playerNameProvider.PlayerNameChanged -= OnPlayerNameChanged;
    }
}
