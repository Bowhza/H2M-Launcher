using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Services
{
    public interface IMasterServerService
    {
        IAsyncEnumerable<ServerConnectionDetails> FetchServersAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the cached servers or fetches them if the cache entry does not exists.
        /// </summary>
        IAsyncEnumerable<ServerConnectionDetails> GetServersAsync(CancellationToken cancellationToken = default);
    }
}
