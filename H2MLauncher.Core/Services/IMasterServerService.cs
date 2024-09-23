using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Services
{
    public interface IMasterServerService
    {
        Task<IReadOnlySet<ServerConnectionDetails>> FetchServersAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the cached servers or fetches them if the cache entry does not exists.
        /// </summary>
        Task<IReadOnlySet<ServerConnectionDetails>> GetServersAsync(CancellationToken cancellationToken);
    }
}
