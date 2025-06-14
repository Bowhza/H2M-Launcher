using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Services
{
    public interface IGameServerCommunicationService<TServer> : 
        IGameServerInfoService<TServer>, 
        IGameServerStatusService<TServer> where TServer : IServerConnectionDetails;
}