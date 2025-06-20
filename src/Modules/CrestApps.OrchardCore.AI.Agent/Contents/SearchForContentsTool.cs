using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Contents.Services;
using OrchardCore.Contents.ViewModels;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Json;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

public sealed class SearchForContentsTool : AIFunction
{
    public const string TheName = "searchForContentItems";

    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IContentsAdminListQueryService _contentsAdminListQueryService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly DocumentJsonSerializerOptions _options;
    private readonly PagerOptions _pagerOptions;

    public SearchForContentsTool(
        IContentManager contentManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IContentsAdminListQueryService contentsAdminListQueryService,
        IUpdateModelAccessor updateModelAccessor,
        IOptions<DocumentJsonSerializerOptions> options,
        IOptions<PagerOptions> pagerOptions)
    {
        _contentManager = contentManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _contentsAdminListQueryService = contentsAdminListQueryService;
        _updateModelAccessor = updateModelAccessor;
        _pagerOptions = pagerOptions.Value;
        _options = options.Value;

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
              "type": "object",
              "properties": {
                "term": {
                  "type": "string",
                  "description": "The query string to search for."
                },
                "pageNumber": {
                  "type": "integer",
                  "description": "The page number of results to return.",
                  "default": 1
                }
              },
              "required": ["term"],
              "additionalProperties": false
            }     
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Search for content items that match the given query along with a way to paginate the results.";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.ListContent))
        {
            return "You do not have permission to list content items.";
        }

        if (!arguments.TryGetFirstString("term", out var term))
        {
            return "Unable to find a term argument in the function arguments.";
        }

        var page = arguments.GetFirstValueOrDefault("pageNumber", 1);

        if (page < 1)
        {
            page = 1;
        }

        var startingIndex = (page - 1) * _pagerOptions.PageSize;

        var query = await _contentsAdminListQueryService.QueryAsync(new ContentOptionsViewModel()
        {
            SearchText = term,
            OriginalSearchText = term,
            StartIndex = startingIndex,
        }, _updateModelAccessor.ModelUpdater);

        var contentItemsCount = await query.CountAsync(cancellationToken);

        var contentItems = await query.Skip(startingIndex)
            .Take(_pagerOptions.PageSize)
            .ListAsync(_contentManager);

        return
        $$"""
            {
                "contentItems": {{JsonSerializer.Serialize(contentItems, _options.SerializerOptions)}},
                "pageSize": {{_pagerOptions.PageSize}},
                "contentItemsCount": {{contentItemsCount}},
                "totalPages": {{Math.Ceiling((double)contentItemsCount / _pagerOptions.PageSize)}},
                "pageSize": {{_pagerOptions.PageSize}},
            }
            """;
    }
}
