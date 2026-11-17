using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Lists the instances of one workflow or of all workflows, the most recent first, through the workflow instance
/// store. The status and date filters and the counts per status of the instances page query an index of the Workflows
/// module and are not offered: the status of each row is returned instead.
/// </summary>
public sealed class ListWorkflowInstancesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listWorkflowInstances";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "workflowTypeId": {
          "type": "string",
          "description": "The workflow type ID, numeric ID or exact name of the workflow whose instances to list, from listWorkflowTypes. Leave it out to list the instances of every workflow."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of instances per page, 20 by default and 50 at most."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the instances of one workflow or of all workflows, the most recent first, with paging. Each row has the instance id, workflow, version, status (Halted, Running, Faulted, Finished, Aborted...), creation date, fault message, what it waits on and when a retry is due.";

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

        var typeStore = services.GetRequiredService<IWorkflowTypeStore>();
        var reference = arguments.GetFirstValueOrDefault<string>("workflowTypeId");
        string workflowTypeId = null;

        if (!string.IsNullOrWhiteSpace(reference))
        {
            var workflowType = await WorkflowToolSupport.FindWorkflowTypeAsync(typeStore, reference);

            if (workflowType is null)
            {
                return $"There is no workflow '{reference}'. Call listWorkflowTypes to find its ID.";
            }

            workflowTypeId = workflowType.WorkflowTypeId;
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var store = services.GetRequiredService<IWorkflowStore>();
        var localClock = services.GetRequiredService<ILocalClock>();
        var total = await store.CountAsync(workflowTypeId);
        var workflows = (await store.ListAsync(workflowTypeId, (page - 1) * pageSize, pageSize)).ToList();
        var workflowTypes = (await typeStore.ListAsync()).ToDictionary(type => type.WorkflowTypeId, StringComparer.Ordinal);
        var versionNumbers = await WorkflowToolSupport.GetVersionNumbersAsync(services.GetRequiredService<IWorkflowTypeVersionStore>(), workflows);
        var items = new List<object>();

        foreach (var workflow in workflows)
        {
            workflowTypes.TryGetValue(workflow.WorkflowTypeId ?? string.Empty, out var workflowType);

            items.Add(await WorkflowToolSupport.SummarizeInstanceAsync(
                workflow,
                workflowType,
                workflow.WorkflowTypeVersionId is not null && versionNumbers.TryGetValue(workflow.WorkflowTypeVersionId, out var number) ? number : null,
                localClock));
        }

        return AgentToolResults.Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = total > page * pageSize,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
        });
    }
}
