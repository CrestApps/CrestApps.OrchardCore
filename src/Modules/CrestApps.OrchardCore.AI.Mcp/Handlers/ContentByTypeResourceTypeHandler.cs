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
/// Handles content-type resources by listing all published content items of a given type.
/// Supported variable: {contentType} - the content type to query.
/// </summary>
public sealed class ContentByTypeResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "content-type";

    private readonly ISession _session;
    private readonly DocumentJsonSerializerOptions _jsonOptions;
    private readonly ILogger _logger;

    public ContentByTypeResourceTypeHandler(
        ISession session,
        IOptions<DocumentJsonSerializerOptions> jsonOptions,
        ILogger<ContentByTypeResourceTypeHandler> logger)
        : base(TypeName)
    {
        _session = session;
        _jsonOptions = jsonOptions.Value;
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        variables.TryGetValue("contentType", out var contentType);

        if (string.IsNullOrEmpty(contentType))
        {
            return CreateErrorResult(resource.Resource.Uri, "Content type is required. Include {contentType} in the URI pattern.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reading content by type: {ContentType}", contentType);
        }

        var query = _session.Query<ContentItem, ContentItemIndex>()
            .Where(x => x.ContentType == contentType && x.Published);

        var contentItems = await query.ListAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Found {Count} content items of type {ContentType}", contentItems.Count(), contentType);
        }

        var json = JsonSerializer.Serialize(contentItems, _jsonOptions.SerializerOptions);

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
