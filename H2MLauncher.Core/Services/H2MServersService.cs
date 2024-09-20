using H2MLauncher.Core.IW4MAdmin;
using H2MLauncher.Core.IW4MAdmin.Models;
using H2MLauncher.Core.Utilities;

using Microsoft.Extensions.DependencyInjection;

namespace H2MLauncher.Core.Services
{
    public class H2MServersService(IServiceScopeFactory serviceScopeFactory, IErrorHandlingService errorHandlingService) : IH2MServersService
    {
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService;
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly List<IW4MServer> _servers = [];
        public IReadOnlyCollection<IW4MServer> Servers => _servers.AsReadOnly();

        public async Task<IReadOnlyList<IW4MServer>> FetchServersAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<IW4MServerInstance>? servers = null;
            _servers.Clear();

            IServiceScope scope = _serviceScopeFactory.CreateScope();
            try
            {
                IIW4MAdminMasterService iw4mAdminMasterService = scope.ServiceProvider.GetRequiredService<IIW4MAdminMasterService>();
                servers = await iw4mAdminMasterService.GetAllServerInstancesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to fetch the servers details at this time. Please try again later.");
            }
            finally
            {
                scope.Dispose();
            }

            if (servers is not null)
                _servers.AddRange(servers
                    .SelectMany(instance => instance.Servers)
                    .Where(server => server.Game == "H2M")
                    .ToList());

            return _servers;
        }
    }
}
