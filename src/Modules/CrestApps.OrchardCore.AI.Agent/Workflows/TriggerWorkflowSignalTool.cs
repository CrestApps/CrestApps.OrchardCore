using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Locking.Distributed;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Helpers;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Sends a named signal as the signal URL of a workflow does (<c>workflows/Trigger</c>): to one instance, which is
/// resumed on each Signal event it waits on under its lock, or to a correlation ID, which resumes the instances waiting
/// on a Signal event for it and starts the enabled workflows whose start activity is a Signal event. Only the Signal
/// events whose name matches the signal go on.
/// </summary>
public sealed class TriggerWorkflowSignalTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "triggerWorkflowSignal";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "signal": {
          "type": "string",
          "description": "The name of the signal, as set on the Signal event of the workflow."
        },
        "instanceId": {
          "type": "string",
          "description": "The numeric instance ID or workflow instance ID of the instance to signal, from listWorkflowInstances. Leave it out to signal by correlation ID."
        },
        "correlationId": {
          "type": "string",
          "description": "When no instanceId is given: the correlation ID the waiting instances have, for example a content item ID. Leave both out to signal the instances that wait without a correlation ID."
        }
      },
      "required": [
        "signal"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sends a named signal: resumes the instances waiting on a Signal event with that name for a given instance id or correlation id, and starts enabled workflows whose start activity is that signal. Use it to move a waiting workflow forward, for example an approval step.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, WorkflowsPermissions.ExecuteWorkflows))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var signal = arguments.GetFirstValueOrDefault<string>("signal")?.Trim();

        if (string.IsNullOrEmpty(signal))
        {
            return "Nothing was sent: give the name of the signal.";
        }

        var input = new Dictionary<string, object> { ["Signal"] = signal };
        var workflowManager = services.GetRequiredService<IWorkflowManager>();
        var reference = arguments.GetFirstValueOrDefault<string>("instanceId");

        if (string.IsNullOrWhiteSpace(reference))
        {
            var correlationId = arguments.GetFirstValueOrDefault<string>("correlationId")?.Trim();
            var contexts = (await workflowManager.TriggerEventAsync(WorkflowToolSupport.SignalEventName, input, string.IsNullOrEmpty(correlationId) ? null : correlationId)).ToList();

            return AgentToolResults.Json(new
            {
                signal,
                correlationId,
                runs = contexts.Select(context => new
                {
                    instanceId = context.Workflow.Id == 0 ? (long?)null : context.Workflow.Id,
                    workflowId = context.Workflow.WorkflowId,
                    workflowTypeId = context.WorkflowType?.WorkflowTypeId,
                    workflowName = context.WorkflowType?.Name,
                    status = context.Status.ToString(),
                    faultMessage = WorkflowToolSupport.Truncate(context.Workflow.FaultMessage),
                }).ToList(),
                note = contexts.Count == 0
                    ? "No instance waits on a Signal event for this name and correlation ID, and no enabled workflow starts with it."
                    : null,
            });
        }

        var workflowStore = services.GetRequiredService<IWorkflowStore>();
        var workflow = await WorkflowToolSupport.FindInstanceAsync(workflowStore, reference);

        if (workflow is null)
        {
            return $"Nothing was sent: there is no workflow instance '{reference}'. Call listWorkflowInstances to find its ID.";
        }

        if (!workflow.BlockingActivities.Any(activity => activity.Name == WorkflowToolSupport.SignalEventName))
        {
            return $"Nothing was sent: the instance {workflow.Id} does not wait on a Signal event (it is {workflow.Status}).";
        }

        // If atomic, the instance's lock is held while it resumes.
        var (locker, locked) = await services.GetRequiredService<IDistributedLock>().TryAcquireWorkflowLockAsync(workflow);

        if (!locked)
        {
            return $"Nothing was sent: the instance {workflow.Id} is running. Try again in a moment.";
        }

        await using var acquiredLock = locker;

        // The instance may have moved on while the lock was awaited.
        workflow = workflow.IsAtomic ? await workflowStore.GetAsync(workflow.WorkflowId) : workflow;

        if (workflow is null)
        {
            return $"Nothing was sent: the instance '{reference}' was deleted in the meantime.";
        }

        var resumed = workflow;

        // Only the Signal event whose name matches goes on: the event checks the signal it receives.
        foreach (var signalActivity in workflow.BlockingActivities.Where(activity => activity.Name == WorkflowToolSupport.SignalEventName).ToList())
        {
            var context = await workflowManager.ResumeWorkflowAsync(workflow, signalActivity, input);

            resumed = context?.Workflow ?? resumed;
        }

        return AgentToolResults.Json(new
        {
            signal,
            instanceId = resumed.Id,
            workflowId = resumed.WorkflowId,
            status = resumed.Status.ToString(),
            faultMessage = WorkflowToolSupport.Truncate(resumed.FaultMessage),
            waitsOn = resumed.BlockingActivities.Select(activity => activity.Name).ToList(),
            note = resumed.BlockingActivities.Any(activity => activity.Name == WorkflowToolSupport.SignalEventName)
                ? "The instance still waits on a Signal event: the signal name may not match the one it waits for. getWorkflowInstance shows the activities it waits on."
                : null,
        });
    }
}
