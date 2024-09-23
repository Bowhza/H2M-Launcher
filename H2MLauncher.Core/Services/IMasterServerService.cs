using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Services
{
    public interface IMasterServerService
    {
        Task<IReadOnlyList<ServerConnectionDetails>> FetchServersAsync(CancellationToken cancellationToken);
    }
}
