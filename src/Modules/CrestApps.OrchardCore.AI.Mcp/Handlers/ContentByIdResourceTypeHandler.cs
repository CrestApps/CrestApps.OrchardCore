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
/// Handles content-item resources by returning a specific content item by its ID or version ID.
/// Supported variables:
///   {contentItemId} - the content item ID to retrieve.
///   {contentItemVersionId} - (optional) retrieve a specific version of the content item.
/// </summary>
public sealed class ContentByIdResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "content-item";

    private readonly IContentManager _contentManager;
    private readonly DocumentJsonSerializerOptions _jsonOptions;
    private readonly ILogger _logger;

    public ContentByIdResourceTypeHandler(
        IContentManager contentManager,
        IOptions<DocumentJsonSerializerOptions> jsonOptions,
        ILogger<ContentByIdResourceTypeHandler> logger)
        : base(TypeName)
    {
        _contentManager = contentManager;
        _jsonOptions = jsonOptions.Value;
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        variables.TryGetValue("contentItemId", out var contentItemId);
        variables.TryGetValue("contentItemVersionId", out var contentItemVersionId);

        if (string.IsNullOrEmpty(contentItemId) && string.IsNullOrEmpty(contentItemVersionId))
        {
            return CreateErrorResult(resource.Resource.Uri, "A content item ID or version ID is required. Include {contentItemId} or {contentItemVersionId} in the URI pattern.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reading content item by ID: {ContentItemId}, VersionId: {ContentItemVersionId}", contentItemId, contentItemVersionId);
        }

        ContentItem contentItem;

        if (!string.IsNullOrEmpty(contentItemVersionId))
        {
            contentItem = await _contentManager.GetVersionAsync(contentItemVersionId);
        }
        else
        {
            contentItem = await _contentManager.GetAsync(contentItemId);
        }

        if (contentItem is null)
        {
            var identifier = !string.IsNullOrEmpty(contentItemVersionId)
                ? $"version '{contentItemVersionId}'"
                : $"'{contentItemId}'";

            return CreateErrorResult(resource.Resource.Uri, $"Content item not found: {identifier}");
        }

        var json = JsonSerializer.Serialize(contentItem, _jsonOptions.SerializerOptions);

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = resource.Resource.Uri,
                    MimeType = "application/json",
                    Text = json,
                }
            ]
        };
    }
}
