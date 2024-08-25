using H2MLauncher.Core.Interfaces;
using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Services
{
    public class H2MServersService(IIW4MAdminMasterService iW4MAdminMasterService, IErrorHandlingService errorHandlingService) : IH2MServersService
    {
        private readonly IIW4MAdminMasterService _iW4MAdminMasterService = iW4MAdminMasterService ?? throw new ArgumentNullException(nameof(iW4MAdminMasterService));
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
        private readonly List<IW4MServer> _servers = [];

        public async Task<IEnumerable<IW4MServer>> GetServersAsync(CancellationToken cancellationToken)
        {
            IEnumerable<IW4MServerInstance>? servers = null;
            _servers.Clear();

            try
            {
                servers = await _iW4MAdminMasterService.GetAllServerInstancesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Unable to fetch the servers details at this time. Please try again later.");
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
