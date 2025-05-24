using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MatchmakingServer.Authentication.JWT;

public class TokenService
{
    private readonly SymmetricSecurityKey _jwtSigningKey;
    private readonly JwtSettings _jwtSettings;

    public TokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
        _jwtSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.Secret));
    }

    public TokenResponse CreateToken(IEnumerable<Claim> claims)
    {
        JsonWebTokenHandler tokenHandler = new();
        SecurityTokenDescriptor tokenDescriptor = new()
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            SigningCredentials = new SigningCredentials(_jwtSigningKey, SecurityAlgorithms.HmacSha256Signature),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
        };

        string? jwtToken = tokenHandler.CreateToken(tokenDescriptor);
        TimeSpan expiresIn = TimeSpan.FromMinutes(_jwtSettings.AccessTokenExpirationMinutes);

        return new TokenResponse
        {
            AccessToken = jwtToken,
            ExpiresIn = (int)expiresIn.TotalSeconds,
            TokenType = "Bearer"
        };
    }
}
