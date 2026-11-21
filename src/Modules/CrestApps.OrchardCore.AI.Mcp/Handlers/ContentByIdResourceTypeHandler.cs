using System.Text.Json;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles content-item resources by returning a specific published content item by its ID or version ID. The caller
/// must be allowed to view the item; a draft or an item the caller can't view answers "not found".
/// Supported variables:
///   {contentItemId} - the content item ID to retrieve.
///   {contentItemVersionId} - (optional) retrieve a specific published version of the content item.
/// </summary>
public sealed class ContentByIdResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "content-item";

    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly DocumentJsonSerializerOptions _jsonOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentByIdResourceTypeHandler"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, whose user is the MCP caller.</param>
    /// <param name="jsonOptions">The json options.</param>
    /// <param name="logger">The logger.</param>
    public ContentByIdResourceTypeHandler(
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<DocumentJsonSerializerOptions> jsonOptions,
        ILogger<ContentByIdResourceTypeHandler> logger)
    : base(TypeName)
    {
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
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
            contentItem = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);
        }

        // A draft, an old version or an item the caller may not view is not revealed.
        if (contentItem is null || !contentItem.Published || !await CanViewAsync(contentItem))
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

    private async Task<bool> CanViewAsync(ContentItem contentItem)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user is not null && await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, contentItem);
    }
}
