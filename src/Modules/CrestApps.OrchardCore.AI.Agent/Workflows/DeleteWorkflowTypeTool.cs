using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Deletes workflows as the Delete action and bulk action of the Workflows page do, through the workflow type store,
/// which also deletes their instances, journals and versions, and lets the Workflows module delete their drafts.
/// </summary>
public sealed class DeleteWorkflowTypeTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteWorkflowType";

    private const int MaxWorkflows = 20;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "workflowTypeIds": {
          "type": "array",
          "minItems": 1,
          "maxItems": 20,
          "items": {
            "type": "string"
          },
          "description": "The workflow type IDs, numeric IDs or exact names of the workflows to delete, from listWorkflowTypes."
        }
      },
      "required": [
        "workflowTypeIds"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Permanently deletes one or more workflows with their versions, draft, instances and journals. Prefer disableWorkflowType when the user only wants a workflow to stop.";

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

        if (!AgentToolArguments.TryReadStrings(arguments, "workflowTypeIds", out var references) || references is null || references.Count == 0)
        {
            return "Nothing was deleted: give 'workflowTypeIds', the IDs of the workflows from listWorkflowTypes.";
        }

        if (references.Count > MaxWorkflows)
        {
            return $"Nothing was deleted: give at most {MaxWorkflows} workflows at a time.";
        }

        var store = services.GetRequiredService<IWorkflowTypeStore>();
        var workflowStore = services.GetRequiredService<IWorkflowStore>();
        var workflowTypes = new List<WorkflowType>();
        var unknown = new List<string>();

        foreach (var reference in references)
        {
            var workflowType = await WorkflowToolSupport.FindWorkflowTypeAsync(store, reference);

            if (workflowType is null)
            {
                unknown.Add(reference);
            }
            else if (!workflowTypes.Any(existing => existing.WorkflowTypeId == workflowType.WorkflowTypeId))
            {
                workflowTypes.Add(workflowType);
            }
        }

        // Nothing is deleted unless every workflow is found, so a typo never deletes only part of what was asked.
        if (unknown.Count > 0)
        {
            return $"Nothing was deleted: there is no workflow {string.Join(", ", unknown.Select(reference => $"'{reference}'"))}. Call listWorkflowTypes to find their IDs.";
        }

        var deleted = new List<object>();

        foreach (var workflowType in workflowTypes)
        {
            var instances = await workflowStore.CountAsync(workflowType.WorkflowTypeId);

            await store.DeleteAsync(workflowType);

            deleted.Add(new
            {
                id = workflowType.Id,
                workflowTypeId = workflowType.WorkflowTypeId,
                name = workflowType.Name,
                deletedInstances = instances,
            });
        }

        return AgentToolResults.Json(new
        {
            deleted,
        });
    }
}
