using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Enables a workflow so its start events start instances again.
/// </summary>
public sealed class EnableWorkflowTypeTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "enableWorkflowType";

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
    public override string Description => "Enables a workflow so its start events start instances again. Takes effect at once, without publishing.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => await WorkflowTypeEnabledState.ChangeAsync(arguments, enable: true);
}
