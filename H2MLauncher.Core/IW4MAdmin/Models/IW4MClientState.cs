namespace H2MLauncher.Core.IW4MAdmin.Models
{
    public enum IW4MClientState
    {
        /// <summary>
        ///     default client state
        /// </summary>
        Unknown,

        /// <summary>
        ///     represents when the client has been detected as joining
        ///     by the log file, but has not be authenticated by RCon
        /// </summary>
        Connecting,

        /// <summary>
        ///     represents when the client has been authenticated by RCon
        ///     and validated by the database
        /// </summary>
        Connected,

        /// <summary>
        ///     represents when the client is leaving (either through RCon or log file)
        /// </summary>
        Disconnecting
    }
}
