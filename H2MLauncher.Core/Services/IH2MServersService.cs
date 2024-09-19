using H2MLauncher.Core.IW4MAdmin.Models;

namespace H2MLauncher.Core.Services
{
    public interface IH2MServersService
    {
        /// <summary>
        /// Gets the current collection of servers.
        /// </summary>
        IReadOnlyCollection<IW4MServer> Servers { get; }

        /// <summary>
        /// Gets the servers for the H2M game mode asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns the servers list.</returns>
        Task<IReadOnlyList<IW4MServer>> FetchServersAsync(CancellationToken cancellationToken);
    }
}
