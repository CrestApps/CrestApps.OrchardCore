using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Json;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

public sealed class GetWorkflowTypesTool : AIFunction
{
    public const string TheName = "getWorkflowType";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
        """);

    public override string Name => TheName;

    public override string Description => "Get workflow type information.";

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
        var workflowTypeStore = arguments.Services.GetRequiredService<IWorkflowTypeStore>();
        var options = arguments.Services.GetRequiredService<IOptions<DocumentJsonSerializerOptions>>().Value;

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageWorkflows))
        {
            return "The current user does not have permission to manage workflows.";
        }

        if (!arguments.TryGetFirst<string>("workflowTypeId", out var workflowTypeId))
        {
            return "Unable to find a workflowTypeId argument in the function arguments.";
        }

        var workflowType = await workflowTypeStore.GetAsync(workflowTypeId);

        if (workflowType is null)
        {
            return "Unable to find a workflowType with the provided workflowTypeId.";
        }

        return JsonSerializer.Serialize(workflowType, options.SerializerOptions);
    }
}
