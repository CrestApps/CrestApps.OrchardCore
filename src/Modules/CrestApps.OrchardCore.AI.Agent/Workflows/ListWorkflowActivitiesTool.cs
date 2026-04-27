using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Represents the list workflow activities tool.
/// </summary>
public sealed class ListWorkflowActivitiesTool : AIFunction
{
    public const string TheName = "listWorkflowActivities";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {},
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description => "List all available workflow activities like tasks and events.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<ListWorkflowActivitiesTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var activityLibrary = arguments.Services.GetRequiredService<IActivityLibrary>();

        if (!arguments.TryGetFirst<string>("workflowTypeId", out var workflowTypeId))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument '{ArgumentName}'.", Name, "workflowTypeId");

            return "Unable to find a workflowTypeId argument in the function arguments.";
        }

        var activities = activityLibrary.ListActivities();

        if (!activities.Any())
        {
            logger.LogWarning("AI tool '{ToolName}' found no available workflow activities.", Name);

            return "There are no available activities.";
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return JsonSerializer.Serialize(activities.Select(x => new
        {
            x.Name,
            x.DisplayText,
            x.Category,
            IsEvent = x is IEvent,
            IsTask = x is ITask,
        }));
    }
}
