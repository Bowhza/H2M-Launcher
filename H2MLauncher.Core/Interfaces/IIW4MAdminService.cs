﻿using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Interfaces
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
        Task<IW4MServerStatus> GetServerStatusAsync(string serverInstanceAddress, string id, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the server list from the server instance asynchronously.
        /// </summary>
        /// /// <param name="serverInstanceAddress">The server instance address.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns the server list <see cref="IEnumerable{IW4MServerDetails}"/> asynchronously.</returns>
        Task<IEnumerable<IW4MServerDetails>> GetServerListAsync(string serverInstanceAddress, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the server details from server instance asynchronously.
        /// </summary>
        /// <param name="id">The server id.</param>
        /// <param name="serverInstanceAddress">The server instance address.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns the server details <see cref="IW4MServerDetails"/> asynchronously.</returns>
        Task<IW4MServerDetails> GetServerDetailsAsync(string serverInstanceAddress, string id, CancellationToken cancellationToken);
    }
}
