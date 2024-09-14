using System.Collections.Concurrent;

using H2MLauncher.Core.Interfaces;
using H2MLauncher.Core.Models;

using Microsoft.Extensions.Caching.Memory;

namespace MatchmakingServer
{
    public class ServerInstanceCache
    {
        private readonly IIW4MAdminService _iw4mAdminService;
        private readonly IIW4MAdminMasterService _iw4mAdminMasterService;
        private readonly ILogger<ServerInstanceCache> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly ConcurrentDictionary<ConnectionDetails, string> _serverInstanceMap = [];

        private const string WebfrontAvailableCacheKey = "WebfrontAvailable";
        private static readonly TimeSpan WebfrontAvailablilityCacheExpiration = TimeSpan.FromHours(1);

        private record struct ConnectionDetails(string IpOrHostName, int Port) { }

        public ServerInstanceCache(IIW4MAdminService iw4mAdminService, IIW4MAdminMasterService iw4mAdminMasterService,
            IMemoryCache memoryCache, ILogger<ServerInstanceCache> logger)
        {
            _iw4mAdminService = iw4mAdminService;
            _iw4mAdminMasterService = iw4mAdminMasterService;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<IW4MServerInstance?> GetInstanceByIdAsync(string instanceId, CancellationToken cancellationToken)
        {
            // get cached instance
            if (_memoryCache.TryGetValue(instanceId, out IW4MServerInstance? instance))
            {
                return instance;
            }

            // fetch new instance by id
            instance = await _iw4mAdminMasterService.GetServerInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (instance is null)
            {
                return null;
            }

            // cache new instance
            using (ICacheEntry cacheEntry = _memoryCache.CreateEntry(instanceId))
            {
                cacheEntry
                    .SetValue(instance)
                    .RegisterPostEvictionCallback((key, value, reason, state) =>
                    {
                        if (reason is EvictionReason.Expired && value is IW4MServerInstance oldInstance)
                        {
                            foreach (IW4MServer server in oldInstance.Servers)
                            {
                                _serverInstanceMap.TryRemove(new ConnectionDetails(server.Ip, server.Port), out _);
                            }
                        }
                    });
            }

            // update instance map for servers
            foreach (IW4MServer server in instance.Servers)
            {
                _serverInstanceMap[new ConnectionDetails(server.Ip, server.Port)] = instanceId;
            }

            return instance;
        }

        public Task<IW4MServerInstance?> GetInstanceForServerAsync(string serverIp, int port, CancellationToken cancellationToken)
        {
            if (!_serverInstanceMap.TryGetValue(new ConnectionDetails(serverIp, port), out var instanceId))
            {
                // server does not exist, what to do?
                return Task.FromResult<IW4MServerInstance?>(null);
            }

            return GetInstanceByIdAsync(instanceId, cancellationToken);
        }

        public async Task<IReadOnlyList<IW4MServerStatus>> TryGetWebfrontStatusList(string instanceId, CancellationToken cancellationToken)
        {
            var instance = await GetInstanceByIdAsync(instanceId, cancellationToken);
            if (instance is null)
            {
                // instance does not exist
                return [];
            }

            _logger.LogDebug("Trying to get cached webfront availability for {webFrontUrl}...", instance.WebfrontUrlNormalized);

            if (_memoryCache.TryGetValue(WebfrontAvailableCacheKey, out ConcurrentDictionary<string, bool>? availableMap))
            {
                if (availableMap!.TryGetValue(instance.WebfrontUrlNormalized, out var isWebfrontAvailable) && !isWebfrontAvailable)
                {
                    // webfront is not available -> early return
                    return [];
                }
            }
            else
            {
                availableMap = [];
                _memoryCache.Set(WebfrontAvailableCacheKey, availableMap, WebfrontAvailablilityCacheExpiration);
            }

            IReadOnlyList<IW4MServerStatus>? serverStatuses = null;
            try
            {
                serverStatuses = await _iw4mAdminService.GetServerStatusListAsync(instance.WebfrontUrlNormalized, cancellationToken);
                availableMap[instance.WebfrontUrlNormalized] = true;
            }
            catch (TimeoutException)
            {
                _logger.LogDebug("Server status request timed out, webfront {webFrontUrl} is not available.", instance.WebfrontUrlNormalized);
                availableMap[instance.WebfrontUrlNormalized] = false;
            }

            return serverStatuses ?? [];
        }
    }
}
