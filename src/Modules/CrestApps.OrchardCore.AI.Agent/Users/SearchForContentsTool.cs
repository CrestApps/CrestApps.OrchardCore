using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Json;
using OrchardCore.Navigation;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using OrchardCore.Users.ViewModels;
using YesSql.Filters.Query;
using YesSql.Filters.Query.Services;

namespace CrestApps.OrchardCore.AI.Agent.Users;

public sealed class SearchForUsersTool : AIFunction
{
    public const string TheName = "searchForUsers";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUsersAdminListQueryService _usersAdminListQueryService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly DocumentJsonSerializerOptions _options;
    private readonly PagerOptions _pagerOptions;

    public SearchForUsersTool(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IUsersAdminListQueryService usersAdminListQueryService,
        IUpdateModelAccessor updateModelAccessor,
        IOptions<DocumentJsonSerializerOptions> options,
        IOptions<PagerOptions> pagerOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _usersAdminListQueryService = usersAdminListQueryService;
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

    public override string Description => "Search for users that match the given query along with a way to paginate the results.";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, UsersPermissions.ListUsers))
        {
            return "You do not have permission to list users.";
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

        var query = await _usersAdminListQueryService.QueryAsync(new UserIndexOptions()
        {
            SearchText = term,
            OriginalSearchText = term,
            StartIndex = startingIndex,
            FilterResult = new QueryFilterResult<User>(new Dictionary<string, QueryTermOption<User>>()),
        }, _updateModelAccessor.ModelUpdater);

        var contentItemsCount = await query.CountAsync(cancellationToken);

        var contentItems = await query.Skip(startingIndex)
            .Take(_pagerOptions.PageSize)
            .ListAsync(cancellationToken);

        return
        $$"""
            {
                "users": {{JsonSerializer.Serialize(contentItems, _options.SerializerOptions)}},
                "pageSize": {{_pagerOptions.PageSize}},
                "usersCount": {{contentItemsCount}},
                "totalPages": {{Math.Ceiling((double)contentItemsCount / _pagerOptions.PageSize)}},
                "pageSize": {{_pagerOptions.PageSize}},
            }
            """;
    }
}
