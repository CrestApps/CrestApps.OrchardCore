using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
      "required": [
        "workflowTypeId"
      ],
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
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<GetWorkflowTypesTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var workflowTypeStore = arguments.Services.GetRequiredService<IWorkflowTypeStore>();
        var options = arguments.Services.GetRequiredService<IOptions<DocumentJsonSerializerOptions>>().Value;

        if (!arguments.TryGetFirst<string>("workflowTypeId", out var workflowTypeId))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument '{ArgumentName}'.", Name, "workflowTypeId");

            return "Unable to find a workflowTypeId argument in the function arguments.";
        }

        var workflowType = await workflowTypeStore.GetAsync(workflowTypeId);

        if (workflowType is null)
        {
            logger.LogWarning("AI tool '{ToolName}' could not find workflow type with ID '{WorkflowTypeId}'.", Name, workflowTypeId);

            return "Unable to find a workflowType with the provided workflowTypeId.";
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return JsonSerializer.Serialize(workflowType, options.SerializerOptions);
    }
}
