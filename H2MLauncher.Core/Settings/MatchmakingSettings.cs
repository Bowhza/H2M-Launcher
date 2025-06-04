using Flurl;

namespace H2MLauncher.Core.Settings
{
    public sealed record MatchmakingSettings
    {
        public string MatchmakingServerApiUrl { get; init; } = "";

        public string QueueingHubUrl { get; init; } = "";

        public string PartyHubUrl { get; init; } = "";

        public string SocialHubUrl { get; init; } = "";

        public string ServerDataUrl => Url.Combine(MatchmakingServerApiUrl, "servers/data");

        public bool UseRandomCliendId { get; init; } = false;

        /// <summary>
        /// USE ONLY FOR LOCAL DEVELOPMENT!
        /// </summary>
        public bool DisableCertificateValidation { get; init; } = false;
    }
}
