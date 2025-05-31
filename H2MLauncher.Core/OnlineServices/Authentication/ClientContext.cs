using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using H2MLauncher.Core.Game;

namespace H2MLauncher.Core.OnlineServices.Authentication;

/// <summary>
/// Provides context for the online service clients, such as the unique client id or access token.
/// </summary>
public sealed class ClientContext : IDisposable
{
    private readonly IPlayerNameProvider _playerNameProvider;
    private BearerToken? _token;
    private string? _userId;
    private string? _userName;

    public string PlayerName => _playerNameProvider.PlayerName;

    public string? UserId => _userId;

    public string? UserName => _userName;

    public string? AccessToken => _token?.AccessToken;

    [MemberNotNullWhen(true, nameof(AccessToken))]
    [MemberNotNullWhen(true, nameof(UserId))]
    public bool IsAuthenticated => _token is not null && _token.ExpirationDate > DateTimeOffset.Now;

    public event Action? UserChanged;

    public ClientContext(IPlayerNameProvider playerNameProvider)
    {
        _playerNameProvider = playerNameProvider;
        _playerNameProvider.PlayerNameChanged += OnPlayerNameChanged;
    }

    public void InvalidateToken()
    {
        _token = null;
    }

    public void UpdateToken(BearerToken? token)
    {
        _token = token;
        _userId = null;
        _userName = null;

        if (token is null)
        {
            UserChanged?.Invoke();
            return;
        }

        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt = handler.ReadJwtToken(token.AccessToken);

        Claim? userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdClaim is not null)
        {
            _userId = userIdClaim.Value;
        }

        Claim? userNameClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        if (userNameClaim is not null)
        {
            _userName = userNameClaim.Value;
        }

        UserChanged?.Invoke();
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
