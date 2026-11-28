using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Locking.Distributed;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Helpers;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Cancels workflow instances as the Cancel bulk action of the instances page does: under the instance's lock, an
/// instance that has not ended becomes Aborted, stops waiting and drops its pending retry, and the instance that runs
/// it as an activity, if any, is resumed.
/// </summary>
public sealed class CancelWorkflowInstancesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "cancelWorkflowInstances";

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
    public override string Description => "Cancels instances that have not ended: they become Aborted, stop waiting for their events and drop a pending retry. A cancelled instance cannot be resumed.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, WorkflowsPermissions.ManageWorkflows))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var error = WorkflowToolSupport.ReadInstanceReferences(arguments, "instanceIds", out var references);

        if (error is not null)
        {
            return error;
        }

        var workflowStore = services.GetRequiredService<IWorkflowStore>();
        var distributedLock = services.GetRequiredService<IDistributedLock>();
        var workflowManager = services.GetRequiredService<IWorkflowManager>();
        var results = new List<object>();
        var cancelled = 0;

        foreach (var reference in references)
        {
            var loaded = await WorkflowToolSupport.FindInstanceAsync(workflowStore, reference);

            if (loaded is null)
            {
                results.Add(new { instanceId = reference, cancelled = false, message = "There is no such instance." });

                continue;
            }

            if (loaded.Status is WorkflowStatus.Finished or WorkflowStatus.Aborted)
            {
                results.Add(new { instanceId = loaded.Id, cancelled = false, message = $"The instance already ended ({loaded.Status})." });

                continue;
            }

            // An instance that runs holds its lock: it can be cancelled once it stops.
            var (locker, locked) = await distributedLock.TryAcquireWorkflowExclusiveLockAsync(loaded);

            if (!locked)
            {
                results.Add(new { instanceId = loaded.Id, cancelled = false, message = "The instance is running. Try again in a moment." });

                continue;
            }

            await using var acquiredLock = locker;

            var workflow = await workflowStore.GetAsync(loaded.Id);

            if (workflow is null || workflow.Status is WorkflowStatus.Finished or WorkflowStatus.Aborted)
            {
                results.Add(new { instanceId = loaded.Id, cancelled = false, message = "The instance ended or was deleted in the meantime." });

                continue;
            }

            var hadEnded = WorkflowToolSupport.HasEnded(workflow);

            workflow.Status = WorkflowStatus.Aborted;
            workflow.BlockingActivities.Clear();
            workflow.PendingRetry = null;
            await workflowStore.SaveAsync(workflow);

            if (!hadEnded)
            {
                await workflowManager.ResumeParentOfStoppedChildAsync(workflow, $"The workflow '{workflow.WorkflowId}' was canceled.");
            }

            cancelled++;
            results.Add(new { instanceId = workflow.Id, cancelled = true, message = "The instance is now Aborted." });
        }

        return AgentToolResults.Json(new
        {
            cancelled,
            results,
        });
    }
}
