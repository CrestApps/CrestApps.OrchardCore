using A2A;
using CrestApps.OrchardCore.AI.A2A.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.A2A.Services;

internal sealed class DefaultA2AAgentCardCacheService : IA2AAgentCardCacheService
{
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _memoryCache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    public DefaultA2AAgentCardCacheService(
        IMemoryCache memoryCache,
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DefaultA2AAgentCardCacheService> logger)
    {
        _memoryCache = memoryCache;
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<AgentCard> GetAgentCardAsync(string connectionId, A2AConnection connection, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(connectionId);

        if (_memoryCache.TryGetValue(cacheKey, out AgentCard cachedCard))
        {
            return cachedCard;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            var metadata = connection.As<A2AConnectionMetadata>();

            // Resolve the scoped auth service from the current request to avoid
            // capturing a scoped service in this singleton.
            var authService = _httpContextAccessor.HttpContext?.RequestServices
                .GetService<IA2AConnectionAuthService>();

            if (authService is not null)
            {
                await authService.ConfigureHttpClientAsync(httpClient, metadata, cancellationToken);
            }

            var resolver = new A2ACardResolver(new Uri(connection.Endpoint), httpClient);

            var agentCard = await resolver.GetAgentCardAsync(cancellationToken);

            if (agentCard is not null)
            {
                _memoryCache.Set(cacheKey, agentCard, _cacheDuration);
            }

            return agentCard;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch agent card from A2A host '{Endpoint}' for connection '{ConnectionId}'.", connection.Endpoint, connectionId);

            return null;
        }
    }

    public void Invalidate(string connectionId)
    {
        _memoryCache.Remove(GetCacheKey(connectionId));
    }

    private static string GetCacheKey(string connectionId)
        => $"A2AAgentCard:{connectionId}";
}
