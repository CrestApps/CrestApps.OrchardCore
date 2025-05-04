using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Json;
using OrchardCore.Navigation;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agents.Workflows;

public sealed class ListWorkflowTypesTool : AIFunction
{
    public const string TheName = "listWorkflowTypes";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IWorkflowTypeStore _workflowTypeStore;
    private readonly DocumentJsonSerializerOptions _options;
    private readonly PagerOptions _pagerOptions;

    public ListWorkflowTypesTool(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IWorkflowTypeStore workflowTypeStore,
        IOptions<DocumentJsonSerializerOptions> options,
        IOptions<PagerOptions> pagerOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _workflowTypeStore = workflowTypeStore;
        _options = options.Value;
        _pagerOptions = pagerOptions.Value;
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

    public override string Description => "List all workflow types";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageWorkflows))
        {
            return "The current user does not have permission to manage workflows.";
        }

        var page = arguments.GetFirstValueOrDefault("pageNumber", 1);

        if (page < 1)
        {
            page = 1;
        }

        var startingIndex = (page - 1) * _pagerOptions.PageSize;

        var workflowTypes = await _workflowTypeStore.ListAsync();

        var count = workflowTypes.Count();

        if (arguments.TryGetFirstString("term", out var term))
        {
            workflowTypes = workflowTypes.Where(x => x.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var items = workflowTypes
            .Skip(startingIndex)
            .Take(_pagerOptions.PageSize)
            .ToList();

        return
        $$"""
            {
                "workflows": {{JsonSerializer.Serialize(items, _options.SerializerOptions)}},
                "pageSize": {{_pagerOptions.PageSize}},
                "workflowsCount": {{count}},
                "totalPages": {{Math.Ceiling((double)count / _pagerOptions.PageSize)}},
                "pageSize": {{_pagerOptions.PageSize}},
            }
            """;
    }
}
