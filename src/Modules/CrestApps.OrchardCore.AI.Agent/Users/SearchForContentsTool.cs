using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
        """);

    public override string Name => TheName;

    public override string Description => "Search for users that match the given query along with a way to paginate the results.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services, nameof(arguments.Services));

        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();
        var usersAdminListQueryService = arguments.Services.GetRequiredService<IUsersAdminListQueryService>();
        var updateModelAccessor = arguments.Services.GetRequiredService<IUpdateModelAccessor>();
        var options = arguments.Services.GetRequiredService<IOptions<DocumentJsonSerializerOptions>>().Value;
        var pagerOptions = arguments.Services.GetRequiredService<IOptions<PagerOptions>>().Value;

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, UsersPermissions.ListUsers))
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

        var startingIndex = (page - 1) * pagerOptions.PageSize;

        var query = await usersAdminListQueryService.QueryAsync(new UserIndexOptions()
        {
            SearchText = term,
            OriginalSearchText = term,
            StartIndex = startingIndex,
            FilterResult = new QueryFilterResult<User>(new Dictionary<string, QueryTermOption<User>>()),
        }, updateModelAccessor.ModelUpdater);

        var contentItemsCount = await query.CountAsync(cancellationToken);

        var contentItems = await query.Skip(startingIndex)
            .Take(pagerOptions.PageSize)
            .ListAsync(cancellationToken);

        return
        $$"""
            {
                "users": {{JsonSerializer.Serialize(contentItems, options.SerializerOptions)}},
                "usersCount": {{contentItemsCount}},
                "totalPages": {{Math.Ceiling((double)contentItemsCount / pagerOptions.PageSize)}},
                "pageSize": {{pagerOptions.PageSize}}
            }
            """;
    }
}
