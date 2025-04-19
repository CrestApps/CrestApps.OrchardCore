using System.Text.Json;
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

namespace CrestApps.OrchardCore.AI.Agents.Contents;

public sealed class SearchForContentsTool : AIFunction
{
    public const string TheName = "getContentItemById";

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
                        "description": "The string representation the query to look for."
                    },
                    "pageNumber": {
                        "type": "integer",
                        "description": "The page number to return.",
                        "default": 1
                    }
                },
                "additionalProperties": false,
                "required": ["term"]
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

        if (!arguments.TryGetValue("term", out var data))
        {
            return "Unable to find a term argument in the function arguments.";
        }

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.ListContent))
        {
            return "You do not have permission to list content items.";
        }

        var page = 1;

        if (arguments.TryGetValue("pageNumber", out var pageNumberArg))
        {
            if (pageNumberArg is JsonElement pageNumberArgElement)
            {
                page = pageNumberArgElement.GetInt32();
            }
            else if (int.TryParse(pageNumberArg.ToString(), out var pageNumberArgInt))
            {
                page = pageNumberArgInt;
            }
        }

        if (page < 1)
        {
            page = 1;
        }

        string term;

        if (data is JsonElement jsonElement)
        {
            term = jsonElement.GetString();
        }
        else
        {
            term = data.ToString();
        }

        var startingIndex = (page - 1) * _pagerOptions.PageSize;

        var query = await _contentsAdminListQueryService.QueryAsync(new ContentOptionsViewModel()
        {
            SearchText = term,
            OriginalSearchText = term,
            StartIndex = startingIndex,
        }, _updateModelAccessor.ModelUpdater);

        var contentItemsCount = await query.CountAsync();

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
