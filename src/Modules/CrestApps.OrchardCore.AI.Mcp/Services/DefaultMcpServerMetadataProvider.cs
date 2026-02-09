using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

internal sealed class DefaultMcpServerMetadataProvider : IMcpServerMetadataProvider
{
    private static readonly string _cacheKeyPrefix = "McpServerCapabilities_";

    private readonly McpService _mcpService;
    private readonly IDistributedCache _cache;
    private readonly McpMetadataCacheOptions _cacheOptions;
    private readonly ILogger _logger;

    public DefaultMcpServerMetadataProvider(
        McpService mcpService,
        IDistributedCache cache,
        IOptions<McpMetadataCacheOptions> cacheOptions,
        ILogger<DefaultMcpServerMetadataProvider> logger)
    {
        _mcpService = mcpService;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    public async Task<McpServerCapabilities> GetCapabilitiesAsync(McpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var cacheKey = _cacheKeyPrefix + connection.ItemId;

        var cached = await TryGetCachedCapabilitiesAsync(cacheKey);

        if (cached is not null)
        {
            return cached;
        }

        var capabilities = await FetchCapabilitiesAsync(connection);

        if (capabilities is not null)
        {
            await CacheCapabilitiesAsync(cacheKey, capabilities);
        }

        return capabilities;
    }

    public async Task InvalidateAsync(string connectionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionId);

        await _cache.RemoveAsync(_cacheKeyPrefix + connectionId);
    }

    public Task InvalidateAllAsync()
    {
        // IDistributedCache does not support enumeration; callers should invalidate specific keys
        // or use a signal-based approach. For now, individual invalidation is supported.

        return Task.CompletedTask;
    }

    private async Task<McpServerCapabilities> TryGetCachedCapabilitiesAsync(string cacheKey)
    {
        try
        {
            var cachedBytes = await _cache.GetAsync(cacheKey);

            if (cachedBytes is null || cachedBytes.Length == 0)
            {
                return null;
            }

            return JsonSerializer.Deserialize<McpServerCapabilities>(cachedBytes, JOptions.CamelCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read MCP server metadata cache entry '{CacheKey}'.", cacheKey);

            return null;
        }
    }

    private async Task CacheCapabilitiesAsync(string cacheKey, McpServerCapabilities capabilities)
    {
        try
        {
            var cacheEntryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheOptions.GetCacheDuration(),
            };

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(capabilities, JOptions.CamelCase);

            await _cache.SetAsync(cacheKey, jsonBytes, cacheEntryOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache MCP server metadata for '{CacheKey}'.", cacheKey);
        }
    }

    private async Task<McpServerCapabilities> FetchCapabilitiesAsync(McpConnection connection)
    {
        var capabilities = new McpServerCapabilities
        {
            ConnectionId = connection.ItemId,
            ConnectionDisplayText = connection.DisplayText,
            FetchedUtc = DateTime.UtcNow,
        };

        try
        {
            var client = await _mcpService.GetOrCreateClientAsync(connection);

            if (client is null)
            {
                capabilities.IsHealthy = false;

                return capabilities;
            }

            var tools = new List<McpServerCapability>();
            var prompts = new List<McpServerCapability>();
            var resources = new List<McpServerCapability>();

            // Fetch tools.
            try
            {
                foreach (var tool in await client.ListToolsAsync())
                {
                    tools.Add(new McpServerCapability
                    {
                        Type = McpCapabilityType.Tool,
                        Name = tool.Name,
                        Description = tool.Description,
                        InputSchema = tool.JsonSchema is JsonElement schema ? schema : null,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to list tools for MCP connection '{ConnectionId}'.", connection.ItemId);
            }

            // Fetch prompts.
            try
            {
                foreach (var prompt in await client.ListPromptsAsync())
                {
                    prompts.Add(new McpServerCapability
                    {
                        Type = McpCapabilityType.Prompt,
                        Name = prompt.Name,
                        Description = prompt.Description,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to list prompts for MCP connection '{ConnectionId}'.", connection.ItemId);
            }

            // Fetch resources.
            try
            {
                foreach (var resource in await client.ListResourcesAsync())
                {
                    resources.Add(new McpServerCapability
                    {
                        Type = McpCapabilityType.Resource,
                        Name = resource.Name,
                        Description = resource.Description,
                        MimeType = resource.MimeType,
                        Uri = resource.Uri,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to list resources for MCP connection '{ConnectionId}'.", connection.ItemId);
            }

            capabilities.Tools = tools;
            capabilities.Prompts = prompts;
            capabilities.Resources = resources;
            capabilities.IsHealthy = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch capabilities for MCP connection '{ConnectionId}' ('{ConnectionName}').", connection.ItemId, connection.DisplayText);

            capabilities.IsHealthy = false;
        }

        return capabilities;
    }
}
