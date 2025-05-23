using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace MatchmakingServer.Authentication.Passwordless;

public class PasswordlessAuthenticationService
{
    private static readonly TimeSpan ChallengeExpiration = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;
    private readonly ILogger<PasswordlessAuthenticationService> _logger;

    public PasswordlessAuthenticationService(IMemoryCache cache, ILogger<PasswordlessAuthenticationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public ChallengeResponse GenerateChallenge()
    {
        string challengeId = Guid.NewGuid().ToString("N");
        string challengeNonce = Guid.NewGuid().ToString("N");

        // Store challenge with expiration
        MemoryCacheEntryOptions cacheEntryOptions = new()
        {
            AbsoluteExpirationRelativeToNow = ChallengeExpiration
        };

        _cache.Set(challengeId, challengeNonce, cacheEntryOptions);

        _logger.LogInformation("Generated challenge {challengeId}.", challengeId);

        return new() { ChallengeId = challengeId, Nonce = challengeNonce };
    }

    public AuthenticationResult Authenticate(AuthenticationRequest request)
    {
        // 1. Retrieve the challenge from the cache
        if (!_cache.TryGetValue(request.ChallengeId, out string? challenge) || string.IsNullOrEmpty(challenge))
        {
            _logger.LogWarning("Authentication failed: Challenge ID {challengeId} not found or expired.", request.ChallengeId);
            return (null, AuthenticationError.ExpiredChallenge);
        }

        _cache.Remove(request.ChallengeId);

        // 2. Decode Base64 public key and signature
        byte[] publicKeyBytes;
        byte[] signatureBytes;
        try
        {
            publicKeyBytes = Convert.FromBase64String(request.PublicKey);
            signatureBytes = Convert.FromBase64String(request.Signature);
        }
        catch (FormatException)
        {
            return (null, AuthenticationError.InvalidPublicKeyOrSignatureFormat);
        }

        // 3. Verify the signature
        bool isValidSignature = false;
        try
        {
            using RSA rsa = RSA.Create();

            rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
            byte[] challengeBytes = Encoding.UTF8.GetBytes(challenge);

            isValidSignature = rsa.VerifyData(
                challengeBytes,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Cryptography error during signature verification for challenge ID {challengeId}.", request.ChallengeId);
            return (null, AuthenticationError.VerificationFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred during signature verification for challenge ID {challengeId}.", request.ChallengeId);
            return (null, AuthenticationError.VerificationFailed);
        }

        if (!isValidSignature)
        {
            return (null, AuthenticationError.VerificationFailed);
        }

        // 4. Authentication successful
        byte[] publicKeyHashBytes = SHA256.HashData(publicKeyBytes);
        string? publicKeyHash = WebEncoders.Base64UrlEncode(publicKeyHashBytes);

        Claim[] claims =
        [
            new Claim(ClaimTypes.NameIdentifier, publicKeyHash), // Public key hash is the unique identifier
            new Claim(ClaimTypes.Name, request.PlayerName),
        ];

        ClaimsIdentity identity = new(claims, "RsaPasswordless");

        return (identity, AuthenticationError.Success);
    }
}
