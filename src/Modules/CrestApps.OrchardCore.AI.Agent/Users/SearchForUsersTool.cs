using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Navigation;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using OrchardCore.Users.ViewModels;
using YesSql.Filters.Query;
using YesSql.Filters.Query.Services;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Represents the search for users tool.
/// </summary>
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
      "required": [
        "term"
      ],
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
        ArgumentNullException.ThrowIfNull(arguments.Services);

        if (!await AIToolCallerAuthorization.IsAuthorizedForToolAsync(arguments.Services, Name))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var logger = arguments.Services.GetRequiredService<ILogger<SearchForUsersTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var usersAdminListQueryService = arguments.Services.GetRequiredService<IUsersAdminListQueryService>();
        var updateModelAccessor = arguments.Services.GetRequiredService<IUpdateModelAccessor>();

        var pagerOptions = arguments.Services.GetRequiredService<IOptions<PagerOptions>>().Value;

        if (!arguments.TryGetFirstString("term", out var term))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument '{ArgumentName}'.", Name, "term");

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

        // Only the fields an admin list shows go to the model; the stored user document also holds the password hash,
        // the security stamp, reset tokens, authenticator keys, recovery codes, passkeys and external logins.
        var users = new List<object>();

        foreach (var user in contentItems)
        {
            if (await AIToolCallerAuthorization.IsAuthorizedAsync(arguments.Services, UsersPermissions.ListUsers, user))
            {
                users.Add(user.AsSearchResultAIObject());
            }
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return
        $$"""
{
"users": {{JsonSerializer.Serialize(users)}},
"usersCount": {{contentItemsCount}},
"totalPages": {{Math.Ceiling((double)contentItemsCount / pagerOptions.PageSize)}},
"pageSize": {{pagerOptions.PageSize}}
}
""";
    }
}
