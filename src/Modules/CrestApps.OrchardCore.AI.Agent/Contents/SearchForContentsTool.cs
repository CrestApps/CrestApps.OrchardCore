using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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

    public override string Description => "Search for content items that match the given query along with a way to paginate the results.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var contentManager = arguments.Services.GetRequiredService<IContentManager>();
        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();
        var contentsAdminListQueryService = arguments.Services.GetRequiredService<IContentsAdminListQueryService>();
        var updateModelAccessor = arguments.Services.GetRequiredService<IUpdateModelAccessor>();
        var options = arguments.Services.GetRequiredService<IOptions<DocumentJsonSerializerOptions>>().Value;
        var pagerOptions = arguments.Services.GetRequiredService<IOptions<PagerOptions>>().Value;

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, CommonPermissions.ListContent))
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

        var startingIndex = (page - 1) * pagerOptions.PageSize;

        var query = await contentsAdminListQueryService.QueryAsync(new ContentOptionsViewModel()
        {
            SearchText = term,
            OriginalSearchText = term,
            StartIndex = startingIndex,
        }, updateModelAccessor.ModelUpdater);

        var contentItemsCount = await query.CountAsync(cancellationToken);

        var contentItems = await query.Skip(startingIndex)
            .Take(pagerOptions.PageSize)
            .ListAsync(contentManager);

        return
        $$"""
            {
                "contentItems": {{JsonSerializer.Serialize(contentItems, options.SerializerOptions)}},
                "contentItemsCount": {{contentItemsCount}},
                "totalPages": {{Math.Ceiling((double)contentItemsCount / pagerOptions.PageSize)}},
                "pageSize": {{pagerOptions.PageSize}}
            }
            """;
    }
}
