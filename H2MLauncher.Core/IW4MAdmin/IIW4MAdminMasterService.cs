using H2MLauncher.Core.IW4MAdmin.Models;

namespace H2MLauncher.Core.IW4MAdmin
{
    public interface IIW4MAdminMasterService
    {
        /// <summary>
        /// Gets all server instances from the master server asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns all server instances <see cref="IW4MServerInstance"/> asynchronously.</returns>
        Task<IReadOnlyList<IW4MServerInstance>> GetAllServerInstancesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the specified server instance asynchronously.
        /// </summary>
        /// <param name="id">The server id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns the <see cref="IW4MServerInstance"/> for the specified id asynchronously.</returns>
        Task<IW4MServerInstance?> GetServerInstanceAsync(string id, CancellationToken cancellationToken);
    }
}
