using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Json;
using OrchardCore.Navigation;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

public sealed class ListWorkflowTypesTool : AIFunction
{
    public const string TheName = "listWorkflowTypes";

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

    public override string Description => "List all workflow types";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();
        var workflowTypeStore = arguments.Services.GetRequiredService<IWorkflowTypeStore>();
        var options = arguments.Services.GetRequiredService<IOptions<DocumentJsonSerializerOptions>>().Value;
        var pagerOptions = arguments.Services.GetRequiredService<IOptions<PagerOptions>>().Value;

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageWorkflows))
        {
            return "The current user does not have permission to manage workflows.";
        }

        var page = arguments.GetFirstValueOrDefault("pageNumber", 1);

        if (page < 1)
        {
            page = 1;
        }

        var startingIndex = (page - 1) * pagerOptions.PageSize;

        var workflowTypes = await workflowTypeStore.ListAsync();

        var count = workflowTypes.Count();

        if (arguments.TryGetFirstString("term", out var term))
        {
            workflowTypes = workflowTypes.Where(x => x.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var items = workflowTypes
            .Skip(startingIndex)
            .Take(pagerOptions.PageSize)
            .ToList();

        return
        $$"""
            {
                "workflows": {{JsonSerializer.Serialize(items, options.SerializerOptions)}},
                "workflowsCount": {{count}},
                "totalPages": {{Math.Ceiling((double)count / pagerOptions.PageSize)}},
                "pageSize": {{pagerOptions.PageSize}}
            }
            """;
    }
}
