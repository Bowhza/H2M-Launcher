namespace MatchmakingServer.Authentication.Passwordless;

public enum AuthenticationError
{
    Success,
    ExpiredChallenge,
    InvalidPublicKeyOrSignatureFormat,
    VerificationFailed
}
