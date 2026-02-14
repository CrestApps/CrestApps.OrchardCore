using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles content:// URI resources by delegating to registered IContentResourceStrategyProvider implementations.
/// This allows for extensible content URI handling where each strategy can define its own patterns.
/// </summary>
public sealed class ContentResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "content";

    private readonly IEnumerable<IContentResourceStrategyProvider> _strategyProviders;
    private readonly ILogger _logger;

    public ContentResourceTypeHandler(
        IEnumerable<IContentResourceStrategyProvider> strategyProviders,
        ILogger<ContentResourceTypeHandler> logger)
        : base(TypeName)
    {
        _strategyProviders = strategyProviders;
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, McpResourceUri resourceUri, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reading content resource: {Uri}", resource.Resource.Uri);
        }

        // Find a strategy that can handle this URI's path
        foreach (var strategy in _strategyProviders)
        {
            if (strategy.CanHandle(resourceUri))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Using strategy {Strategy} for URI {Uri}", strategy.GetType().Name, resource.Resource.Uri);
                }
                return await strategy.ReadAsync(resource, resourceUri, cancellationToken);
            }
        }

        // No strategy found - provide helpful error with supported patterns
        var supportedPatterns = _strategyProviders.SelectMany(s => s.UriPatterns).Distinct();

        return CreateErrorResult(resource.Resource.Uri,
            $"No handler found for content URI: {resource.Resource.Uri}. Supported patterns: {string.Join(", ", supportedPatterns)}");
    }
}
