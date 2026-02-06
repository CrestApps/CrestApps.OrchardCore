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
/// Strategy for handling content type based URI paths.
/// Supports:
/// - {contentType}/list - List all content items of a type
/// - {contentType}/{contentItemId} - Get a specific content item by type and ID
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
        "{contentType}/list",
        "{contentType}/{contentItemId}",
    ];

    public bool CanHandle(McpResourceUri uri)
    {
        // Matches any path with at least one segment that does NOT start with "id/"
        // (id/ paths are handled by ContentByIdResourceStrategy).
        return !string.IsNullOrEmpty(uri.Path) &&
               !uri.Path.StartsWith("id/", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ReadResourceResult> ReadAsync(McpResource resource, McpResourceUri uri, CancellationToken cancellationToken = default)
    {
        // Path is "{contentType}/{rest}" - split into first segment (contentType) and remainder.
        var slashIndex = uri.Path.IndexOf('/');

        string contentType;
        string rest;

        if (slashIndex < 0)
        {
            contentType = uri.Path;
            rest = string.Empty;
        }
        else
        {
            contentType = uri.Path[..slashIndex];
            rest = uri.Path[(slashIndex + 1)..];
        }

        _logger.LogDebug("Reading content by type: {ContentType}, path: {Path}", contentType, rest);

        string json;

        if (string.Equals(rest, "list", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(rest))
        {
            json = await ListContentItemsByTypeAsync(contentType, cancellationToken);
        }
        else
        {
            json = await GetContentItemByTypeAndIdAsync(contentType, rest, cancellationToken);

            if (json is null)
            {
                return McpResourceTypeHandlerBase.CreateErrorResult(uri.ToString(), $"Content item not found: {rest} (type: {contentType})");
            }
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
            return null;
        }

        if (!string.Equals(contentItem.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return JsonSerializer.Serialize(contentItem, _jsonOptions.SerializerOptions);
    }
}
