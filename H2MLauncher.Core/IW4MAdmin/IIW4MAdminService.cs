using H2MLauncher.Core.IW4MAdmin.Models;

namespace H2MLauncher.Core.IW4MAdmin
{
    public interface IIW4MAdminService
    {
        /// <summary>
        /// Gets the server status for the specified instance and server id asynchronously.
        /// </summary>
        /// <param name="id">The server id.</param>
        /// <param name="serverInstanceAddress">The server instance address.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns the server status <see cref="IW4MServerStatus"/> asynchronously.</returns>
        Task<IW4MServerStatus?> GetServerStatusAsync(string serverInstanceAddress, string id, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the server status for all servers of the specified instance asynchronously.
        /// </summary>
        /// <param name="serverInstanceAddress">The server instance address.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns the server status list <see cref="IEnumerable{IW4MServerStatus}"/> asynchronously.</returns>
        Task<IReadOnlyList<IW4MServerStatus>> GetServerStatusListAsync(string serverInstanceAddress, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the server list from the server instance asynchronously.
        /// </summary>
        /// /// <param name="serverInstanceAddress">The server instance address.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns the server list <see cref="IEnumerable{IW4MServerDetails}"/> asynchronously.</returns>
        Task<IReadOnlyList<IW4MServerDetails>> GetServerListAsync(string serverInstanceAddress, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the server details from server instance asynchronously.
        /// </summary>
        /// <param name="id">The server id.</param>
        /// <param name="serverInstanceAddress">The server instance address.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns the server details <see cref="IW4MServerDetails"/> asynchronously.</returns>
        Task<IW4MServerDetails?> GetServerDetailsAsync(string serverInstanceAddress, string id, CancellationToken cancellationToken);
    }
}
