using System.Runtime.CompilerServices;

using H2MLauncher.Core.Models;

using Microsoft.Extensions.Caching.Memory;

namespace H2MLauncher.Core.Services
{
    public abstract class CachedMasterServerService(IMemoryCache memoryCache, string cacheKey) : IMasterServerService
    {
        protected IMemoryCache Cache { get; } = memoryCache;
        protected string CacheKey { get; } = cacheKey;

        public async IAsyncEnumerable<ServerConnectionDetails> FetchServersAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            IReadOnlySet<ServerConnectionDetails> servers = await FetchServersCoreAsync(cancellationToken);

            foreach (ServerConnectionDetails server in servers)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return server;
            }
        }        

        public IAsyncEnumerable<ServerConnectionDetails> GetServersAsync(CancellationToken cancellationToken)
        {
            if (Cache.TryGetValue<IReadOnlySet<ServerConnectionDetails>>(CacheKey, out var cachedServers) 
                && cachedServers is not null)
            {
                return cachedServers.ToAsyncEnumerable();
            }

            return FetchServersAsync(cancellationToken);
        }

        protected abstract Task<IReadOnlySet<ServerConnectionDetails>> FetchServersCoreAsync(CancellationToken cancellationToken);
    }
}
