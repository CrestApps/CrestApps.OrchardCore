using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Json;
using YesSql;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Strategy for handling content type based URIs.
/// Supports:
/// - content://{contentType}/list - List all content items of a type
/// - content://{contentType}/{contentItemId} - Get a specific content item by type and ID
/// </summary>
public sealed class ContentByTypeResourceStrategy : IContentResourceStrategyProvider
{
    private readonly IContentManager _contentManager;
    private readonly ISession _session;
    private readonly DocumentJsonSerializerOptions _jsonOptions;
    private readonly ILogger _logger;

    public ContentByTypeResourceStrategy(
        IContentManager contentManager,
        ISession session,
        IOptions<DocumentJsonSerializerOptions> jsonOptions,
        ILogger<ContentByTypeResourceStrategy> logger)
    {
        _contentManager = contentManager;
        _session = session;
        _jsonOptions = jsonOptions.Value;
        _logger = logger;
    }

    public string[] UriPatterns =>
    [
        "content://{contentType}/list",
        "content://{contentType}/{contentItemId}",
    ];

    public bool CanHandle(Uri uri)
    {
        // Matches content://{contentType}/... where host is NOT "id"
        return uri.Scheme == "content" &&
               !string.Equals(uri.Host, "id", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrEmpty(uri.Host);
    }

    public async Task<ReadResourceResult> ReadAsync(McpResource resource, Uri uri, CancellationToken cancellationToken = default)
    {
        var contentType = uri.Host;
        var path = uri.AbsolutePath.TrimStart('/');

        _logger.LogDebug("Reading content by type: {ContentType}, path: {Path}", contentType, path);

        string json;

        if (string.Equals(path, "list", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(path))
        {
            // List all content items of this type
            json = await ListContentItemsByTypeAsync(contentType, cancellationToken);
        }
        else
        {
            // Get specific content item by ID within this type
            json = await GetContentItemByTypeAndIdAsync(contentType, path, cancellationToken);
        }

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

    private async Task<string> ListContentItemsByTypeAsync(string contentType, CancellationToken cancellationToken)
    {
        var query = _session.Query<ContentItem, ContentItemIndex>()
            .Where(x => x.ContentType == contentType && x.Published);

        var contentItems = await query.ListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} content items of type {ContentType}", contentItems.Count(), contentType);

        return JsonSerializer.Serialize(contentItems, _jsonOptions.SerializerOptions);
    }

    private async Task<string> GetContentItemByTypeAndIdAsync(string contentType, string contentItemId, CancellationToken cancellationToken)
    {
        var contentItem = await _contentManager.GetAsync(contentItemId);

        if (contentItem is null)
        {
            throw new InvalidOperationException($"Content item not found: {contentItemId}");
        }

        // Verify the content type matches
        if (!string.Equals(contentItem.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Content item {contentItemId} is of type '{contentItem.ContentType}', not '{contentType}'.");
        }

        return JsonSerializer.Serialize(contentItem, _jsonOptions.SerializerOptions);
    }
}
