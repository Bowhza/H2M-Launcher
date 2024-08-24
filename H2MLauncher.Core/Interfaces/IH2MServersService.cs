using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Interfaces
{
    public interface IH2MServersService
    {
        /// <summary>
        /// Gets the servers for the H2M game mode asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns the servers list.</returns>
        Task<IEnumerable<IW4MServer>> GetServersAsync(CancellationToken cancellationToken);
    }
}
