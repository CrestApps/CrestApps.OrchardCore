using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Enables or disables a workflow on its live workflow type, as the designer's settings do.
/// </summary>
internal sealed class SetWorkflowTypeEnabledTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setWorkflowTypeEnabled";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "workflowTypeId": {
          "type": "string",
          "description": "The workflow type ID, numeric ID or exact name of the workflow, from listWorkflowTypes."
        },
        "enabled": {
          "type": "boolean",
          "description": "True to enable the workflow so its start events start instances, false to disable it."
        }
      },
      "required": [
        "workflowTypeId",
        "enabled"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Enables or disables a workflow. Enabled, its start events start instances again; disabled, its events no longer start instances and its HTTP URL answers not found, while instances that already wait are kept, not cancelled. Takes effect at once, without publishing.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => await WorkflowTypeEnabledState.ChangeAsync(arguments);
}
