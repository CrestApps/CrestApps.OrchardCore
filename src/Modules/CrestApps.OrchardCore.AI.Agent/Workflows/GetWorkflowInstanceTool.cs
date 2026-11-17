using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Returns one workflow instance as its page shows it: status, the version it runs on, the activities it waits on, its
/// variable values, its fault and the activity that faulted, the pending retry, the parent and child instances and the
/// journal. The state as JSON and the recorded data of one activity run are returned only when asked for, as they can
/// hold personal data.
/// </summary>
public sealed class GetWorkflowInstanceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getWorkflowInstance";

    private const int MaxJournalRecords = 50;

    private const int MaxStateLength = 20_000;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "instanceId": {
          "type": "string",
          "description": "The numeric instance ID or the workflow instance ID, from listWorkflowInstances."
        },
        "sequence": {
          "type": "integer",
          "minimum": 0,
          "description": "The sequence number of one journal record. When given, the result is the data that activity run recorded (what it evaluated and set), if the workflow records activity data."
        },
        "includeState": {
          "type": "boolean",
          "description": "Also return the instance's state as JSON, cut to 20,000 characters. Leave it out unless the user needs it: it can hold personal data."
        }
      },
      "required": [
        "instanceId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one instance: status, the version it runs on, the activities it waits on, its variable values, its fault and faulted activity, pending retry, parent and child instances, and its journal (activities run, outcomes, durations, errors). Use it to explain what a run did or why it failed; pass sequence to get the recorded data of one activity run.";

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

        var reference = arguments.GetFirstValueOrDefault<string>("instanceId");
        var workflow = await WorkflowToolSupport.FindInstanceAsync(services.GetRequiredService<IWorkflowStore>(), reference);

        if (workflow is null)
        {
            return $"There is no workflow instance '{reference}'. Call listWorkflowInstances to find its ID.";
        }

        var journal = services.GetRequiredService<IWorkflowExecutionJournal>();

        if (arguments.TryGetFirst("sequence", out _))
        {
            var sequence = arguments.GetFirstValueOrDefault("sequence", -1);
            var record = sequence < 0 ? null : await journal.GetAsync(workflow.WorkflowId, sequence);

            if (record is null)
            {
                return $"The instance {workflow.Id} has no journal record {sequence}. The journal of getWorkflowInstance lists the sequence numbers.";
            }

            return AgentToolResults.Json(new
            {
                instanceId = workflow.Id,
                sequence = record.Sequence,
                activityId = record.ActivityId,
                activityName = record.ActivityName,
                data = record.Data is null ? null : JsonSerializer.SerializeToNode(record.Data),
                note = record.Data is null ? "This activity run recorded no data: the workflow records activity data only when its Record activity data setting is on." : null,
            });
        }

        var localClock = services.GetRequiredService<ILocalClock>();
        var versionStore = services.GetRequiredService<IWorkflowTypeVersionStore>();
        var workflowType = await services.GetRequiredService<IWorkflowTypeStore>().GetAsync(workflow.WorkflowTypeId);
        var version = string.IsNullOrEmpty(workflow.WorkflowTypeVersionId) ? null : await versionStore.GetAsync(workflow.WorkflowTypeVersionId);
        var definition = workflowType is null ? null : await versionStore.GetWorkflowTypeAsync(workflowType, workflow.WorkflowTypeVersionId);
        var records = await journal.ListAsync(workflow.WorkflowId);
        var shownRecords = records.Skip(Math.Max(0, records.Count - MaxJournalRecords)).ToList();
        var journalItems = new List<object>();

        foreach (var record in shownRecords)
        {
            journalItems.Add(new
            {
                sequence = record.Sequence,
                activityId = record.ActivityId,
                activityName = record.ActivityName,
                title = record.ActivityTitle,
                isResume = record.IsResume ? true : (bool?)null,
                status = record.Status.ToString(),
                outcomes = record.Outcomes,
                startedAt = await AgentToolResults.ToLocalAsync(localClock, record.StartedUtc),
                durationMilliseconds = Math.Round(record.DurationMilliseconds, 1),
                error = WorkflowToolSupport.Truncate(record.Error),
                childWorkflowId = record.ChildWorkflowId,
                hasData = record.Data is null ? (bool?)null : true,
            });
        }

        return AgentToolResults.Json(new
        {
            instance = await WorkflowToolSupport.SummarizeInstanceAsync(workflow, workflowType, version?.Version, localClock),
            versionId = workflow.WorkflowTypeVersionId,
            faultedActivityId = workflow.Status == WorkflowStatus.Faulted ? WorkflowToolSupport.GetFaultedActivityId(workflow, records) : null,
            pendingRetry = workflow.Status == WorkflowStatus.Faulted && workflow.PendingRetry is { } retry
                ? new
                {
                    activityId = retry.ActivityId,
                    failedAttempts = retry.FailedAttempts,
                    maxRetries = retry.MaxRetries,
                    dueAt = await AgentToolResults.ToLocalAsync(localClock, retry.DueUtc),
                }
                : null,
            childWorkflowIds = records.Select(record => record.ChildWorkflowId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList(),
            activities = definition?.Activities.Select(activity => new
            {
                activityId = activity.ActivityId,
                name = activity.Name,
                isStart = activity.IsStart,
            }).ToList(),
            variables = VariableValuesOf(workflow, definition?.Variables),
            journal = journalItems,
            journalTotal = records.Count,
            journalTruncated = records.Count > shownRecords.Count,
            journalNote = journal.IsEnabled ? null : "The journal is turned off on this site, so the activities run are not recorded.",
            state = arguments.GetFirstValueOrDefault("includeState", false) ? TruncateState(workflow.State) : null,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            instanceUrl = WorkflowToolSupport.GetInstancePath(services, workflow),
        });
    }

    // The stored values of the declared variables, which the state keeps in its properties, as the instance page reads them.
    private static Dictionary<string, string> VariableValuesOf(Workflow workflow, IList<WorkflowVariableDefinition> variables)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (variables is null || variables.Count == 0 || workflow.State is null)
        {
            return values;
        }

        var properties = workflow.State
            .FirstOrDefault(property => string.Equals(property.Key, nameof(WorkflowState.Properties), StringComparison.OrdinalIgnoreCase))
            .Value as JsonObject;

        if (properties is null)
        {
            return values;
        }

        foreach (var variable in variables)
        {
            if (!string.IsNullOrEmpty(variable?.Name) && properties.TryGetPropertyValue(variable.Name, out var value))
            {
                values[variable.Name] = WorkflowToolSupport.FormatValue(value);
            }
        }

        return values;
    }

    private static string TruncateState(JsonObject state)
    {
        var text = state?.ToJsonString() ?? "{}";

        return text.Length <= MaxStateLength ? text : string.Concat(text.AsSpan(0, MaxStateLength), "…");
    }
}
