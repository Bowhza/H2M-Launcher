using System.Net.Http.Json;

namespace H2MLauncher.Core.Services
{
    public static class HttpContentExtensions
    {
        public static async Task<T?> TryReadFromJsonAsync<T>(this HttpContent content, CancellationToken cancellationToken = default)
        {
            try
            {
                return await content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                return default;
            }
        }
    }
}
