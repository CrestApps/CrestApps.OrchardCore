using System.Text.Json;
using CrestApps.Core.AI.Extensions;
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
/// Runs workflow instances again as the instances page does: <c>retry</c> continues faulted instances from the
/// activity that faulted (the Retry bulk action) or from a given activity (Retry from here), and <c>restart</c> starts
/// a new instance with the original input on the published version (the Restart action), with the same lock and
/// singleton checks.
/// </summary>
public sealed class RerunWorkflowInstancesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "rerunWorkflowInstances";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "mode": {
          "type": "string",
          "enum": [ "retry", "restart" ],
          "description": "retry continues faulted instances from the activity that faulted with their state; restart starts a new instance on the published version with the original input."
        },
        "instanceIds": {
          "type": "array",
          "minItems": 1,
          "maxItems": 50,
          "items": {
            "type": [ "string", "integer" ]
          },
          "description": "The numeric instance IDs or workflow instance IDs, from listWorkflowInstances."
        },
        "activityId": {
          "type": "string",
          "description": "With retry and a single instance: the activity to run from instead of the one that faulted, from getWorkflowInstance."
        }
      },
      "required": [
        "mode",
        "instanceIds"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Runs instances again: retry continues faulted instances from the activity that faulted (or a given activity) with their state; restart starts a new instance on the published version with the original input. Activities run again, so emails, HTTP calls and content changes can repeat.";

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
        var mode = arguments.GetFirstValueOrDefault<string>("mode")?.Trim().ToLowerInvariant();

        // The instances pages need Manage workflows; running an activity again also needs Execute workflows.
        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, WorkflowsPermissions.ManageWorkflows) ||
            (mode != "restart" && !await AIToolCallerAuthorization.IsAuthorizedAsync(services, WorkflowsPermissions.ExecuteWorkflows)))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (mode is not ("retry" or "restart"))
        {
            return "Nothing was run: mode must be retry or restart.";
        }

        var error = WorkflowToolSupport.ReadInstanceReferences(arguments, "instanceIds", out var references);

        if (error is not null)
        {
            return error;
        }

        var activityId = arguments.GetFirstValueOrDefault<string>("activityId")?.Trim();

        if (!string.IsNullOrEmpty(activityId) && (mode != "retry" || references.Count != 1))
        {
            return "Nothing was run: activityId can only be given with retry and a single instance.";
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

        if (unknown.Count > 0)
        {
            return $"Nothing was run: there is no workflow instance {string.Join(", ", unknown.Select(reference => $"'{reference}'"))}. Call listWorkflowInstances to find their IDs.";
        }

        var results = new List<object>();

        foreach (var workflow in workflows)
        {
            results.Add(mode == "retry"
                ? await RetryAsync(services, workflow, activityId)
                : await RestartAsync(services, workflowStore, workflow));
        }

        return AgentToolResults.Json(new
        {
            mode,
            results,
        });
    }

    private static async Task<object> RetryAsync(IServiceProvider services, Workflow workflow, string activityId)
    {
        if (workflow.Status != WorkflowStatus.Faulted)
        {
            return Outcome(workflow, false, $"Only a faulted instance can be retried; this one is {workflow.Status}.");
        }

        activityId ??= WorkflowToolSupport.GetFaultedActivityId(workflow, await services.GetRequiredService<IWorkflowExecutionJournal>().ListAsync(workflow.WorkflowId));

        if (string.IsNullOrEmpty(activityId))
        {
            return Outcome(workflow, false, "The activity this instance faulted at is not known. Give the activityId to run from, from getWorkflowInstance.");
        }

        try
        {
            var context = await services.GetRequiredService<IWorkflowManager>().RetryActivityAsync(workflow, activityId);

            if (context is null)
            {
                return Outcome(workflow, false, "The instance is running. Try again in a moment.");
            }

            return new
            {
                instanceId = workflow.Id,
                done = true,
                fromActivityId = activityId,
                status = context.Status.ToString(),
                faultMessage = WorkflowToolSupport.Truncate(context.Workflow.FaultMessage),
            };
        }
        catch (ArgumentException)
        {
            return Outcome(workflow, false, $"The definition this instance runs on has no activity '{activityId}'.");
        }
        catch (InvalidOperationException)
        {
            return Outcome(workflow, false, "The instance is no longer faulted, or its workflow was deleted.");
        }
    }

    private static async Task<object> RestartAsync(IServiceProvider services, IWorkflowStore workflowStore, Workflow workflow)
    {
        if (workflow.Status is WorkflowStatus.Executing or WorkflowStatus.Halted or WorkflowStatus.Resuming or WorkflowStatus.Starting)
        {
            return Outcome(workflow, false, $"The instance is {workflow.Status}: only an instance that is not running or waiting can be restarted.");
        }

        var workflowType = await services.GetRequiredService<IWorkflowTypeStore>().GetAsync(workflow.WorkflowTypeId);

        if (workflowType is null)
        {
            return Outcome(workflow, false, "The workflow of this instance was deleted.");
        }

        // If a singleton, the lock of the workflow type, or of its correlation ID, is held while it starts.
        var (locker, locked) = await services.GetRequiredService<IDistributedLock>().TryAcquireWorkflowTypeLockAsync(workflowType, correlationId: workflow.CorrelationId);

        if (!locked)
        {
            return Outcome(workflow, false, "Another instance is already running.");
        }

        await using var acquiredLock = locker;

        if ((workflowType.IsSingleton && await workflowStore.HasHaltedInstanceAsync(workflowType.WorkflowTypeId)) ||
            (workflowType.IsSingletonPerCorrelation &&
                !string.IsNullOrEmpty(workflow.CorrelationId) &&
                await workflowStore.HasHaltedInstanceAsync(workflowType.WorkflowTypeId, workflow.CorrelationId)))
        {
            return Outcome(workflow, false, "Another instance is already running.");
        }

        var context = await services.GetRequiredService<IWorkflowManager>().RestartWorkflowAsync(workflow, workflowType);

        return new
        {
            instanceId = workflow.Id,
            done = true,
            newInstanceId = context.Workflow.Id == 0 ? (long?)null : context.Workflow.Id,
            newWorkflowId = context.Workflow.WorkflowId,
            status = context.Status.ToString(),
            faultMessage = WorkflowToolSupport.Truncate(context.Workflow.FaultMessage),
        };
    }

    private static object Outcome(Workflow workflow, bool done, string message)
        => new
        {
            instanceId = workflow.Id,
            done,
            message,
        };
}
