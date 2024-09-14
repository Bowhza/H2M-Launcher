using System.Net;

using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Services
{
    public interface IEndpointResolver
    {
        /// <summary>
        /// Gets the <see cref="IPEndPoint"/> for the given <paramref name="server"/> connetion details.
        /// </summary>
        public Task<IPEndPoint?> GetEndpointAsync(IServerConnectionDetails server, CancellationToken cancellationToken);
    }
}