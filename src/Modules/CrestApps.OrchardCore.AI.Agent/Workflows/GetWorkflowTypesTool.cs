using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Json;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

public sealed class GetWorkflowTypesTool : AIFunction
{
    public const string TheName = "getWorkflowType";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IWorkflowTypeStore _workflowTypeStore;
    private readonly DocumentJsonSerializerOptions _options;

    public GetWorkflowTypesTool(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IWorkflowTypeStore workflowTypeStore,
        IOptions<DocumentJsonSerializerOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _workflowTypeStore = workflowTypeStore;
        _options = options.Value;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
              "type": "object",
              "properties": {
                "workflowTypeId": {
                  "type": "string",
                  "description": "The workflowTypeId to get the information for."
                }
              },
              "required": ["workflowTypeId"],
              "additionalProperties": false
            }     
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Get workflow type information.";

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

        if (!arguments.TryGetFirst<string>("workflowTypeId", out var workflowTypeId))
        {
            return "Unable to find a workflowTypeId argument in the function arguments.";
        }

        var workflowType = await _workflowTypeStore.GetAsync(workflowTypeId);

        if (workflowType is null)
        {
            return "Unable to find a workflowType with the provided workflowTypeId.";
        }

        return JsonSerializer.Serialize(workflowType, _options.SerializerOptions);
    }
}
