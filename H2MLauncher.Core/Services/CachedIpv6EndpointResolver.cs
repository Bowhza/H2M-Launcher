using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Services
{
    public sealed class CachedIpv6EndpointResolver(ILogger<CachedIpv6EndpointResolver> logger, IMemoryCache memoryCache) : IEndpointResolver
    {
        private readonly ILogger<CachedIpv6EndpointResolver> _logger = logger;
        private readonly IMemoryCache _memoryCache = memoryCache;

        private record struct IpEndpointCacheKey(string IpOrHostName, int Port) { }

        /// <summary>
        /// Tries to resolve the <see cref="IPEndPoint"/> of the given <paramref name="server"/> using it's hostname.
        /// </summary>
        private async Task<IPEndPoint?> ResolveEndpointAsync(IServerConnectionDetails server, CancellationToken cancellationToken)
        {
            _logger.LogTrace("Resolving endpoint for server {Server}...", server);

            // ip likely contains a hostname
            try
            {
                // resolve ip addresses from hostname
                var ipAddressList = await Dns.GetHostAddressesAsync(server.Ip, cancellationToken);
                var compatibleIp = ipAddressList.OrderByDescending(ip => ip.AddressFamily is System.Net.Sockets.AddressFamily.InterNetwork)
                                                .FirstOrDefault();
                if (compatibleIp == null)
                {
                    // could not resolve ip address
                    _logger.LogDebug("Not IP address found for {HostName}", server.Ip);
                    return null;
                }

                _logger.LogTrace("Found IP address for {HostName}: {IP} ", server.Ip, compatibleIp);
                return new IPEndPoint(compatibleIp.MapToIPv6(), server.Port);
            }
            catch (Exception ex)
            {
                // invalid ip field
                _logger.LogWarning(ex, "Error while resolving endpoint for server {Server}", server);
                return null;
            }
        }

        /// <summary>
        /// Gets the <see cref="IPEndPoint"/> for the given <paramref name="server"/> from the cache,
        /// or tries to create / resolve it when no valid cache entry is found.
        /// </summary>
        private Task<IPEndPoint?> GetOrResolveEndpointAsync(IServerConnectionDetails server, CancellationToken cancellationToken)
        {
            return _memoryCache.GetOrCreateAsync(
                new IpEndpointCacheKey(server.Ip, server.Port),
                async (cacheEntry) =>
                {
                    if (IPAddress.TryParse(server.Ip, out var ipAddress))
                    {
                        // ip contains an actual ip address -> use that to create endpoint
                        return new IPEndPoint(ipAddress.MapToIPv6(), server.Port);
                    }

                    var endpoint = await ResolveEndpointAsync(server, cancellationToken);
                    if (endpoint is null)
                    {
                        cacheEntry.SlidingExpiration = TimeSpan.FromMinutes(2);
                        return null;
                    }

                    return endpoint;
                });
        }

        public Task<IPEndPoint?> GetEndpointAsync(IServerConnectionDetails server, CancellationToken cancellationToken)
        {
            return GetOrResolveEndpointAsync(server, cancellationToken);
        }
    }
}