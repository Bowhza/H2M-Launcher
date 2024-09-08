namespace MatchmakingServer.Authentication
{
    public static class ApiKeyDefaults
    {
        /// <summary>
        /// Default value for AuthenticationScheme property in the <see cref="ApiKeyAuthenticationOptions"/>.
        /// </summary>
        public const string AuthenticationScheme = "ApiKey";

        public const string RequestHeaderKey = "X-API-Key";
    }
}
