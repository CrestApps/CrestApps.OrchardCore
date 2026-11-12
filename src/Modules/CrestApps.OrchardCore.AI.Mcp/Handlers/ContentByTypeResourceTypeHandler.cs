using System.Globalization;
using System.Text.Json;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.Json;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles content-type resources by listing the published content items of a given type that the caller may view,
/// a page at a time.
/// Supported variables:
///   {contentType} - the content type to query.
///   {page} - (optional) the 1-based page; each page holds at most <see cref="PageSize"/> items.
/// </summary>
public sealed class ContentByTypeResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "content-type";

    /// <summary>
    /// The most items a page lists.
    /// </summary>
    public const int PageSize = 50;

    // The most pages a caller may ask for, so a request can't make the database skip an unbounded number of rows.
    private const int MaxPage = 200;

    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly DocumentJsonSerializerOptions _jsonOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentByTypeResourceTypeHandler"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, whose user is the MCP caller.</param>
    /// <param name="jsonOptions">The json options.</param>
    /// <param name="logger">The logger.</param>
    public ContentByTypeResourceTypeHandler(
        ISession session,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<DocumentJsonSerializerOptions> jsonOptions,
        ILogger<ContentByTypeResourceTypeHandler> logger)
    : base(TypeName)
    {
        _session = session;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
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

        var page = 1;

        if (variables.TryGetValue("page", out var pageValue) && !string.IsNullOrEmpty(pageValue) &&
            (!int.TryParse(pageValue, NumberStyles.None, CultureInfo.InvariantCulture, out page) || page < 1 || page > MaxPage))
        {
            return CreateErrorResult(resource.Resource.Uri, $"The page must be a number from 1 to {MaxPage}.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reading content by type: {ContentType}, page {Page}", contentType, page);
        }

        var contentItems = await _session.Query<ContentItem, ContentItemIndex>()
            .Where(x => x.ContentType == contentType && x.Published)
            .OrderByDescending(x => x.PublishedUtc)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ListAsync(cancellationToken);

        var user = _httpContextAccessor.HttpContext?.User;
        var visible = new List<ContentItem>();

        if (user is not null)
        {
            foreach (var contentItem in contentItems)
            {
                if (await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, contentItem))
                {
                    visible.Add(contentItem);
                }
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Found {Count} content items of type {ContentType} the caller may view", visible.Count, contentType);
        }

        var json = JsonSerializer.Serialize(visible, _jsonOptions.SerializerOptions);

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
