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
/// Handles content:// URI resources by reading content items from Orchard Core.
/// Supports pattern: content://id/{contentItemId} - Get a specific content item by ID
/// </summary>
public sealed class ContentResourceTypeHandler : IMcpResourceTypeHandler
{
    public const string TypeName = "content";

    private readonly IContentManager _contentManager;
    private readonly DocumentJsonSerializerOptions _jsonOptions;
    private readonly ILogger _logger;

    public ContentResourceTypeHandler(
        IContentManager contentManager,
        IOptions<DocumentJsonSerializerOptions> jsonOptions,
        ILogger<ContentResourceTypeHandler> logger)
    {
        _contentManager = contentManager;
        _jsonOptions = jsonOptions.Value;
        _logger = logger;
    }

    public string Type => TypeName;

    public async Task<ReadResourceResult> ReadAsync(McpResource resource, CancellationToken cancellationToken = default)
    {
        var uri = resource.Resource?.Uri;

        if (string.IsNullOrEmpty(uri))
        {
            throw new InvalidOperationException("Resource URI is required.");
        }

        // Parse the content:// URI
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var contentUri) || contentUri.Scheme != "content")
        {
            throw new InvalidOperationException($"Invalid content URI: {uri}. Expected format: content://id/{{contentItemId}}");
        }

        // Parse the path segments: host is "id", path contains the value
        var host = contentUri.Host.ToLowerInvariant();
        var path = contentUri.AbsolutePath.TrimStart('/');

        _logger.LogDebug("Reading content resource: host={Host}, path={Path}", host, path);

        if (host != "id")
        {
            throw new InvalidOperationException($"Invalid content URI host: {host}. Expected 'id'.");
        }

        // Get a specific content item by ID
        var content = await GetContentItemByIdAsync(path, cancellationToken);

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = content,
                }
            ]
        };
    }

    private async Task<string> GetContentItemByIdAsync(string contentItemId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(contentItemId))
        {
            throw new InvalidOperationException("Content item ID is required.");
        }

        var contentItem = await _contentManager.GetAsync(contentItemId);

        if (contentItem is null)
        {
            throw new InvalidOperationException($"Content item not found: {contentItemId}");
        }

        return JsonSerializer.Serialize(contentItem, _jsonOptions.SerializerOptions);
    }
}
