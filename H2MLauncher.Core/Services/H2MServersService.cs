using H2MLauncher.Core.IW4MAdmin;
using H2MLauncher.Core.IW4MAdmin.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Networking;

using Microsoft.Extensions.DependencyInjection;

namespace H2MLauncher.Core.Services
{

    public class H2MServersService(IServiceScopeFactory serviceScopeFactory, IErrorHandlingService errorHandlingService, IEndpointResolver endpointResolver)
        : IMasterServerService
    {
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService;
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly List<ServerConnectionDetails> _servers = [];
        private readonly IEndpointResolver _endpointResolver = endpointResolver;
        public IReadOnlyCollection<ServerConnectionDetails> Servers => _servers.AsReadOnly();

        public async Task<IReadOnlyList<ServerConnectionDetails>> FetchServersAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<IW4MServerInstance>? instances = null;

            IServiceScope scope = _serviceScopeFactory.CreateScope();
            try
            {
                IIW4MAdminMasterService iw4mAdminMasterService = scope.ServiceProvider.GetRequiredService<IIW4MAdminMasterService>();
                instances = await iw4mAdminMasterService.GetAllServerInstancesAsync(cancellationToken);

                if (instances is not null)
                {
                    var filteredServers = instances
                        .SelectMany(instance => instance.Servers)
                        .Where(server => server.Game == "H2M");

                    var endpointMap = await _endpointResolver.CreateEndpointServerMap(filteredServers, cancellationToken);

                    _servers.Clear();
                    _servers.AddRange(
                        endpointMap.Keys.Where(key =>
                                key.AddressFamily is System.Net.Sockets.AddressFamily.InterNetwork ||
                                key.Address.IsIPv4MappedToIPv6)
                            .Select(ep => new ServerConnectionDetails(ep.Address.GetRealAddress().ToString(), ep.Port)
                        )
                    );
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

            return _servers;
        }
    }
}
