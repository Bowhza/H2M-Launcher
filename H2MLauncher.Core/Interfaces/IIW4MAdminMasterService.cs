using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Interfaces
{
    public interface IIW4MAdminMasterService
    {
        /// <summary>
        /// Gets all server instances from the master server asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns all server instances <see cref="IW4MAdminServerInstance"/> asynchronously.</returns>
        Task<IEnumerable<IW4MAdminServerInstance>> GetAllServerInstancesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the specified server instance asynchronously.
        /// </summary>
        /// <param name="id">The server id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns the <see cref="IW4MAdminServerInstance"/> for the specified id asynchronously.</returns>
        Task<IW4MAdminServerInstance> GetServiceInstanceAsync(string id, CancellationToken cancellationToken);
    }
}
