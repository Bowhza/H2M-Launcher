using System.Net;

using H2MLauncher.Core.IW4MAdmin;
using H2MLauncher.Core.IW4MAdmin.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Networking;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace H2MLauncher.Core.Services
{
    public class H2MServersService(
        IServiceScopeFactory serviceScopeFactory,
        IErrorHandlingService errorHandlingService,
        IEndpointResolver endpointResolver,
        IMemoryCache memoryCache) : CachedMasterServerService(memoryCache, "H2M_SERVERS")
    {
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService;
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly IEndpointResolver _endpointResolver = endpointResolver;

        protected override async Task<IReadOnlySet<ServerConnectionDetails>> FetchServersCoreAsync(CancellationToken cancellationToken)
        {
            IServiceScope scope = _serviceScopeFactory.CreateScope();
            try
            {
                IIW4MAdminMasterService iw4mAdminMasterService = scope.ServiceProvider.GetRequiredService<IIW4MAdminMasterService>();
                IReadOnlyList<IW4MServerInstance> instances = await iw4mAdminMasterService.GetAllServerInstancesAsync(cancellationToken);

                if (instances is not null)
                {
                    IEnumerable<IW4MServer> filteredServers = instances
                        .SelectMany(instance => instance.Servers)
                        .Where(server => server.Game == "H2M");

                    IReadOnlyDictionary<IPEndPoint, IW4MServer> endpointMap = await _endpointResolver.CreateEndpointServerMap(
                        filteredServers, cancellationToken);

                    HashSet<ServerConnectionDetails> ipv4Servers = endpointMap.Keys.Where(key =>
                                key.AddressFamily is System.Net.Sockets.AddressFamily.InterNetwork ||
                                key.Address.IsIPv4MappedToIPv6)
                            .Select(ep => new ServerConnectionDetails(ep.Address.GetRealAddress().ToString(), ep.Port))
                            .ToHashSet();

                    Cache.Set(CacheKey, ipv4Servers, TimeSpan.FromMinutes(5));

                    return ipv4Servers;
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to fetch the servers details at this time. Please try again later.");
            }
            finally
            {
                scope.Dispose();
            }

            return Cache.Get<IReadOnlySet<ServerConnectionDetails>>(CacheKey) ?? new HashSet<ServerConnectionDetails>();
        }
    }
}
