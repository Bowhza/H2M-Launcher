using System.Net.Http.Headers;

using H2MLauncher.Core.Services;

namespace H2MLauncher.Core.Utilities.Http
{
    public static class HttpRequestHeaderExtensions
    {
        /// <summary>
        /// Add headers to identify app version and name.
        /// </summary>
        public static void AddApplicationMetadata(this HttpRequestHeaders headers)
        {
            headers.Add("X-App-Name", "H2MLauncher");
            headers.Add("X-App-Version", LauncherService.CurrentVersion);
        }
    }
}
