using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles content:// URI resources by delegating to registered IContentResourceStrategyProvider implementations.
/// This allows for extensible content URI handling where each strategy can define its own patterns.
/// </summary>
public sealed class ContentResourceTypeHandler : IMcpResourceTypeHandler
{
    public const string TypeName = "content";

    private readonly IEnumerable<IContentResourceStrategyProvider> _strategyProviders;
    private readonly ILogger _logger;

    public ContentResourceTypeHandler(
        IEnumerable<IContentResourceStrategyProvider> strategyProviders,
        ILogger<ContentResourceTypeHandler> logger)
    {
        _strategyProviders = strategyProviders;
        _logger = logger;
    }

    public string Type => TypeName;

    public async Task<ReadResourceResult> ReadAsync(McpResource resource, CancellationToken cancellationToken = default)
    {
        var uriString = resource.Resource?.Uri;

        if (string.IsNullOrEmpty(uriString))
        {
            throw new InvalidOperationException("Resource URI is required.");
        }

        // Parse the content:// URI
        if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri) || uri.Scheme != "content")
        {
            throw new InvalidOperationException($"Invalid content URI: {uriString}. Expected scheme: content://");
        }

        _logger.LogDebug("Reading content resource: {Uri}", uriString);

        // Find a strategy that can handle this URI
        foreach (var strategy in _strategyProviders)
        {
            if (strategy.CanHandle(uri))
            {
                _logger.LogDebug("Using strategy {Strategy} for URI {Uri}", strategy.GetType().Name, uriString);
                return await strategy.ReadAsync(resource, uri, cancellationToken);
            }
        }

        // No strategy found - provide helpful error with supported patterns
        var supportedPatterns = _strategyProviders.SelectMany(s => s.UriPatterns).Distinct();
        throw new InvalidOperationException(
            $"No handler found for content URI: {uriString}. Supported patterns: {string.Join(", ", supportedPatterns)}");
    }
}
