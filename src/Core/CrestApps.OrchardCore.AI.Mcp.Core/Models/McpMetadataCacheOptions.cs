namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

/// <summary>
/// Configuration options for caching MCP server metadata.
/// </summary>
public sealed class McpMetadataCacheOptions
{
    private static readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the cache expiration time in minutes for MCP metadata entries.
    /// Default is 30 minutes.
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 30;

    public TimeSpan GetCacheDuration()
    {
        var minutes = CacheExpirationMinutes;

        return minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : _defaultCacheDuration;
    }
}
