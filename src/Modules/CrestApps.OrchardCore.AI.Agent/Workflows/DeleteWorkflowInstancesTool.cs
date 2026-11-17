using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Deletes workflow instances as the Delete bulk action of the instances page does: through the workflow instance
/// store, which lets the Workflows module delete their journals, and resuming the instance that runs a deleted one as
/// an activity when it had not ended.
/// </summary>
public sealed class DeleteWorkflowInstancesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteWorkflowInstances";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "instanceIds": {
          "type": "array",
          "minItems": 1,
          "maxItems": 50,
          "items": {
            "type": [ "string", "integer" ]
          },
          "description": "The numeric instance IDs or workflow instance IDs, from listWorkflowInstances."
        }
      },
      "required": [
        "instanceIds"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Permanently deletes instances and their journals. Use cancelWorkflowInstances when the history should be kept.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, WorkflowsPermissions.ManageWorkflows))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var error = WorkflowToolSupport.ReadInstanceReferences(arguments, "instanceIds", out var references);

        if (error is not null)
        {
            return error;
        }

        var workflowStore = services.GetRequiredService<IWorkflowStore>();
        var workflows = new List<Workflow>();
        var unknown = new List<string>();

        foreach (var reference in references)
        {
            var workflow = await WorkflowToolSupport.FindInstanceAsync(workflowStore, reference);

            if (workflow is null)
            {
                unknown.Add(reference);
            }
            else if (!workflows.Any(existing => existing.Id == workflow.Id))
            {
                workflows.Add(workflow);
            }
        }

        // Nothing is deleted unless every instance is found, so a wrong ID never deletes only part of what was asked.
        if (unknown.Count > 0)
        {
            return $"Nothing was deleted: there is no workflow instance {string.Join(", ", unknown.Select(reference => $"'{reference}'"))}. Call listWorkflowInstances to find their IDs.";
        }

        var workflowManager = services.GetRequiredService<IWorkflowManager>();
        var deleted = new List<object>();

        foreach (var workflow in workflows)
        {
            await workflowStore.DeleteAsync(workflow);

            if (!WorkflowToolSupport.HasEnded(workflow))
            {
                await workflowManager.ResumeParentOfStoppedChildAsync(workflow, $"The workflow '{workflow.WorkflowId}' was deleted.");
            }

            deleted.Add(new
            {
                instanceId = workflow.Id,
                workflowId = workflow.WorkflowId,
                status = workflow.Status.ToString(),
            });
        }

        return AgentToolResults.Json(new
        {
            deleted,
        });
    }
}
