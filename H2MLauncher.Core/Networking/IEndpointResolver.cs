using System.Net;

using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Networking
{
    public interface IEndpointResolver
    {
        /// <summary>
        /// Gets the <see cref="IPEndPoint"/> for the given <paramref name="server"/> connetion details.
        /// </summary>
        public Task<IPEndPoint?> GetEndpointAsync(IServerConnectionDetails server, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a dictionary of ip endpoints to servers by resolving the addresses in parallel and filtering out duplicates.
        /// </summary>
        Task<IReadOnlyDictionary<IPEndPoint, TServer>> CreateEndpointServerMap<TServer>(
            IEnumerable<TServer> servers, CancellationToken cancellationToken) where TServer : IServerConnectionDetails;
    }
}