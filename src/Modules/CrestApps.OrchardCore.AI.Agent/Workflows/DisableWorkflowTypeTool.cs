using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Disables a workflow: its events no longer start instances.
/// </summary>
public sealed class DisableWorkflowTypeTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "disableWorkflowType";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "workflowTypeId": {
          "type": "string",
          "description": "The workflow type ID, numeric ID or exact name of the workflow, from listWorkflowTypes."
        }
      },
      "required": [
        "workflowTypeId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Disables a workflow: its events no longer start instances and its HTTP URL answers not found. Instances that already wait are kept, not cancelled.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => await WorkflowTypeEnabledState.ChangeAsync(arguments, enable: false);
}
