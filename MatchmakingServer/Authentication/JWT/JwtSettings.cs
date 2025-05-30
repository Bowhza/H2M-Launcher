using System.ComponentModel.DataAnnotations;

namespace MatchmakingServer.Authentication.JWT;

public class JwtSettings
{
    [Required(AllowEmptyStrings = false)]
    public required string Audience { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string Issuer { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string Secret { get; set; }

    public int AccessTokenExpirationMinutes { get; set; } = 60;
}
