using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using OrchardCore.ContentManagement;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Strategy for handling content URIs with the path pattern: id/{contentItemId}.
/// Returns a specific content item by its ID.
/// </summary>
public sealed class ContentByIdResourceStrategy : IContentResourceStrategyProvider
{
    private readonly IContentManager _contentManager;
    private readonly DocumentJsonSerializerOptions _jsonOptions;
    private readonly ILogger _logger;

    public ContentByIdResourceStrategy(
        IContentManager contentManager,
        IOptions<DocumentJsonSerializerOptions> jsonOptions,
        ILogger<ContentByIdResourceStrategy> logger)
    {
        _contentManager = contentManager;
        _jsonOptions = jsonOptions.Value;
        _logger = logger;
    }

    public string[] UriPatterns => ["id/{contentItemId}"];

    public bool CanHandle(McpResourceUri uri)
    {
        // Matches path starting with "id/"
        return uri.Path.StartsWith("id/", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ReadResourceResult> ReadAsync(McpResource resource, McpResourceUri uri, CancellationToken cancellationToken = default)
    {
        // Path is "id/{contentItemId}" - extract the content item ID after "id/".
        var contentItemId = uri.Path["id/".Length..];

        if (string.IsNullOrEmpty(contentItemId))
        {
            return McpResourceTypeHandlerBase.CreateErrorResult(uri.ToString(), "Content item ID is required in the URI path.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reading content item by ID: {ContentItemId}", contentItemId);
        }

        var contentItem = await _contentManager.GetAsync(contentItemId);

        if (contentItem is null)
        {
            return McpResourceTypeHandlerBase.CreateErrorResult(uri.ToString(), $"Content item not found: {contentItemId}");
        }

        var json = JsonSerializer.Serialize(contentItem, _jsonOptions.SerializerOptions);

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = uri.ToString(),
                    MimeType = "application/json",
                    Text = json,
                }
            ]
        };
    }
}
