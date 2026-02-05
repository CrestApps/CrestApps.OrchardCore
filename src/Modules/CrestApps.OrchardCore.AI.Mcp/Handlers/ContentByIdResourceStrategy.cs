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
/// Strategy for handling content://id/{contentItemId} URIs.
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

    public string[] UriPatterns => ["content://id/{contentItemId}"];

    public bool CanHandle(Uri uri)
    {
        // Matches content://id/{contentItemId}
        return uri.Scheme == "content" &&
               string.Equals(uri.Host, "id", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ReadResourceResult> ReadAsync(McpResource resource, Uri uri, CancellationToken cancellationToken = default)
    {
        var contentItemId = uri.AbsolutePath.TrimStart('/');

        if (string.IsNullOrEmpty(contentItemId))
        {
            throw new InvalidOperationException("Content item ID is required in the URI path.");
        }

        _logger.LogDebug("Reading content item by ID: {ContentItemId}", contentItemId);

        var contentItem = await _contentManager.GetAsync(contentItemId);

        if (contentItem is null)
        {
            throw new InvalidOperationException($"Content item not found: {contentItemId}");
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
