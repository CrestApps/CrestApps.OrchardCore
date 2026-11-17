using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using OrchardCore.Modules;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// What the workflow tools share: finding a workflow or an instance from what the model passes, the summary of an
/// instance, and the admin pages a result links to.
/// </summary>
internal static class WorkflowToolSupport
{
    /// <summary>
    /// The name of the event a workflow starts with when it can be run by hand with inputs.
    /// </summary>
    public const string StartedByWorkflowEventName = "StartedByWorkflowEvent";

    /// <summary>
    /// The name of the HTTP Request event.
    /// </summary>
    public const string HttpRequestEventName = "HttpRequestEvent";

    /// <summary>
    /// The name of the Signal event.
    /// </summary>
    public const string SignalEventName = "SignalEvent";

    /// <summary>
    /// The name of the User Task event.
    /// </summary>
    public const string UserTaskEventName = "UserTaskEvent";

    /// <summary>
    /// The most instances one call acts on.
    /// </summary>
    public const int MaxInstancesPerCall = 50;

    /// <summary>
    /// The longest text a value of a result keeps.
    /// </summary>
    public const int MaxValueLength = 2_000;

    /// <summary>
    /// The area of the Workflows module, which owns the admin pages.
    /// </summary>
    public const string Area = "OrchardCore.Workflows";

    /// <summary>
    /// The note every tool that writes the live workflow type adds about a draft opened in the designer.
    /// </summary>
    public const string DraftNote = "If someone has unpublished changes to this workflow open in the designer, their draft still holds the previous state and publishing it would bring that state back.";

    /// <summary>
    /// Finds a workflow by its workflow type ID, its numeric ID or its exact name.
    /// </summary>
    /// <param name="store">The workflow type store.</param>
    /// <param name="reference">What the model passed.</param>
    /// <returns>The workflow, or <see langword="null"/> when none or several match.</returns>
    public static async Task<WorkflowType> FindWorkflowTypeAsync(IWorkflowTypeStore store, string reference)
    {
        ArgumentNullException.ThrowIfNull(store);

        var value = reference?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var workflowType = await store.GetAsync(value);

        if (workflowType is not null)
        {
            return workflowType;
        }

        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) &&
            await store.GetAsync(id) is { } byId)
        {
            return byId;
        }

        var byName = (await store.ListAsync())
            .Where(type => string.Equals(type.Name?.Trim(), value, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        return byName.Count == 1 ? byName[0] : null;
    }

    /// <summary>
    /// Reads the instance references a tool acts on: numeric instance IDs or workflow instance IDs.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="key">The name of the argument.</param>
    /// <param name="references">The references without repeats, or <see langword="null"/> when the argument is missing or invalid.</param>
    /// <returns>An error sentence, or <see langword="null"/> when the references were read.</returns>
    public static string ReadInstanceReferences(AIFunctionArguments arguments, string key, out List<string> references)
    {
        references = null;

        if (!AgentToolArguments.TryReadJsonArray(arguments, key, out var array) || array is null || array.Count == 0)
        {
            return $"Nothing was done: give '{key}', the IDs of the instances from listWorkflowInstances.";
        }

        var values = new List<string>();

        foreach (var node in array)
        {
            var text = node switch
            {
                JsonValue value when value.TryGetValue<long>(out var number) => number.ToString(CultureInfo.InvariantCulture),
                JsonValue value when value.TryGetValue<string>(out var stringValue) => stringValue?.Trim(),
                _ => null,
            };

            if (string.IsNullOrEmpty(text))
            {
                return $"Nothing was done: every entry of '{key}' must be an instance ID from listWorkflowInstances.";
            }

            if (!values.Contains(text, StringComparer.Ordinal))
            {
                values.Add(text);
            }
        }

        if (values.Count > MaxInstancesPerCall)
        {
            return $"Nothing was done: give at most {MaxInstancesPerCall} instances at a time.";
        }

        references = values;

        return null;
    }

    /// <summary>
    /// Finds an instance by its numeric ID or its workflow instance ID.
    /// </summary>
    /// <param name="store">The workflow instance store.</param>
    /// <param name="reference">What the model passed.</param>
    /// <returns>The instance, or <see langword="null"/> when there is none.</returns>
    public static async Task<Workflow> FindInstanceAsync(IWorkflowStore store, string reference)
    {
        ArgumentNullException.ThrowIfNull(store);

        var value = reference?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) &&
            await store.GetAsync(id) is { } byId)
        {
            return byId;
        }

        return await store.GetAsync(value);
    }

    /// <summary>
    /// Whether an instance ended, which already resumed the instance that runs it as an activity, if any: a faulted
    /// instance that is retried later has not. The same rule as the instances page.
    /// </summary>
    /// <param name="workflow">The instance.</param>
    /// <returns><see langword="true"/> when the instance ended.</returns>
    public static bool HasEnded(Workflow workflow)
        => workflow.Status is WorkflowStatus.Finished or WorkflowStatus.Aborted ||
            (workflow.Status == WorkflowStatus.Faulted && workflow.PendingRetry is null);

    /// <summary>
    /// The activity a faulted instance faulted at: the one its retry policy retries, or the last one its journal
    /// records as faulted, as the instances page finds it.
    /// </summary>
    /// <param name="workflow">The instance.</param>
    /// <param name="journal">The journal records of the instance.</param>
    /// <returns>The activity ID, or <see langword="null"/> when it is not known.</returns>
    public static string GetFaultedActivityId(Workflow workflow, IEnumerable<WorkflowExecutionRecord> journal)
        => workflow.PendingRetry?.ActivityId ??
            journal?.LastOrDefault(record => record.Status == WorkflowExecutionRecordStatus.Faulted)?.ActivityId;

    /// <summary>
    /// Builds the summary of an instance that the instance tools return.
    /// </summary>
    /// <param name="workflow">The instance.</param>
    /// <param name="workflowType">The workflow it belongs to, or <see langword="null"/> when it is gone.</param>
    /// <param name="versionNumber">The number of the version the instance runs on, or <see langword="null"/> when it is not known.</param>
    /// <param name="localClock">The local clock.</param>
    /// <returns>The summary.</returns>
    public static async Task<object> SummarizeInstanceAsync(Workflow workflow, WorkflowType workflowType, int? versionNumber, ILocalClock localClock)
        => new
        {
            id = workflow.Id,
            workflowId = workflow.WorkflowId,
            workflowTypeId = workflow.WorkflowTypeId,
            workflowName = workflowType?.Name,
            version = versionNumber,
            runsOnPublishedVersion = workflowType is null || string.IsNullOrEmpty(workflow.WorkflowTypeVersionId)
                ? (bool?)null
                : workflow.WorkflowTypeVersionId == workflowType.VersionId,
            status = workflow.Status.ToString(),
            createdAt = await AgentToolResults.ToLocalAsync(localClock, workflow.CreatedUtc),
            correlationId = string.IsNullOrEmpty(workflow.CorrelationId) ? null : workflow.CorrelationId,
            faultMessage = Truncate(workflow.FaultMessage),
            waitsOn = workflow.BlockingActivities.Count == 0
                ? null
                : workflow.BlockingActivities.Select(activity => new
                {
                    activityId = activity.ActivityId,
                    name = activity.Name,
                }).ToList(),
            retryDueAt = workflow.PendingRetry is null ? null : await AgentToolResults.ToLocalAsync(localClock, workflow.PendingRetry.DueUtc),
            parentWorkflowId = string.IsNullOrEmpty(workflow.ParentWorkflowId) ? null : workflow.ParentWorkflowId,
        };

    /// <summary>
    /// Gets the numbers of the versions instances run on.
    /// </summary>
    /// <param name="versionStore">The version store.</param>
    /// <param name="workflows">The instances.</param>
    /// <returns>The version number by version ID.</returns>
    public static async Task<Dictionary<string, int>> GetVersionNumbersAsync(IWorkflowTypeVersionStore versionStore, IEnumerable<Workflow> workflows)
    {
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var versionId in workflows.Select(workflow => workflow.WorkflowTypeVersionId).Where(id => !string.IsNullOrEmpty(id)).Distinct())
        {
            if (await versionStore.GetAsync(versionId) is { } version)
            {
                numbers[versionId] = version.Version;
            }
        }

        return numbers;
    }

    /// <summary>
    /// Cuts a text to <see cref="MaxValueLength"/> characters.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, cut with an ellipsis when it was longer.</returns>
    public static string Truncate(string text)
        => text is null || text.Length <= MaxValueLength ? text : string.Concat(text.AsSpan(0, MaxValueLength), "…");

    /// <summary>
    /// Writes a value of an instance as JSON text cut to <see cref="MaxValueLength"/> characters, as the model only
    /// needs to read it and the value may be large.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The JSON text of the value.</returns>
    public static string FormatValue(object value)
    {
        string text;

        try
        {
            text = value switch
            {
                null => "null",
                JsonNode node => node.ToJsonString(),
                _ => JsonSerializer.Serialize(value),
            };
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or JsonException)
        {
            text = value.ToString();
        }

        return Truncate(text);
    }

    /// <summary>
    /// Gets the address of the designer of a workflow.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="workflowType">The workflow.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetDesignerPath(IServiceProvider services, WorkflowType workflowType)
        => AgentToolLinks.GetAdminPath(services, Area, "WorkflowType", "Edit", new RouteValueDictionary { ["id"] = workflowType.Id });

    /// <summary>
    /// Gets the address of the page of an instance.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="workflow">The instance.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetInstancePath(IServiceProvider services, Workflow workflow)
        => AgentToolLinks.GetAdminPath(services, Area, "Workflow", "Details", new RouteValueDictionary { ["id"] = workflow.Id });
}
