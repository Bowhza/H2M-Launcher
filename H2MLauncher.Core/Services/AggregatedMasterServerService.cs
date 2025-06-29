using H2MLauncher.Core.Models;
using H2MLauncher.Core.Utilities;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Services
{
    public class AggregatedMasterServerService : IMasterServerService
    {
        private readonly IMasterServerService _hmwMasterServerService;
        private readonly IMasterServerService _iw4mMasterServerService;
        private readonly ILogger<AggregatedMasterServerService> _logger;

        public AggregatedMasterServerService(
            [FromKeyedServices("HMW")] IMasterServerService hmwMasterServerService,
            [FromKeyedServices("H2M")] IMasterServerService iw4mMasterServerService,
            ILogger<AggregatedMasterServerService> logger)
        {
            _hmwMasterServerService = hmwMasterServerService;
            _iw4mMasterServerService = iw4mMasterServerService;
            _logger = logger;
        }

        public IAsyncEnumerable<ServerConnectionDetails> FetchServersAsync(CancellationToken cancellationToken)
        {
            IEnumerable<IAsyncEnumerable<ServerConnectionDetails>> sources = [
                _iw4mMasterServerService.FetchServersAsync(cancellationToken),
                _hmwMasterServerService.FetchServersAsync(cancellationToken),
            ];

            return sources.Interleave().Distinct();
        }

        public IAsyncEnumerable<ServerConnectionDetails> GetServersAsync(CancellationToken cancellationToken)
        {
            IEnumerable<IAsyncEnumerable<ServerConnectionDetails>> sources = [
                _iw4mMasterServerService.GetServersAsync(cancellationToken),
                _hmwMasterServerService.GetServersAsync(cancellationToken),
            ];

            return sources.Interleave().Distinct();
        }
    }
}
