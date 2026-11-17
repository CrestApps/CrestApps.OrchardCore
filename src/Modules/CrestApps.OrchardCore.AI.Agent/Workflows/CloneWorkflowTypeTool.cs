using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Helpers;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Copies a workflow under a new name, as the Clone action of the Workflows page does: a new workflow type ID, the
/// published activities, transitions, variables and execution settings, saved through the workflow type store. Unlike
/// the page, the copy is always disabled, so it does not react to the same events as the original.
/// </summary>
public sealed class CloneWorkflowTypeTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "cloneWorkflowType";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "workflowTypeId": {
          "type": "string",
          "description": "The workflow type ID, numeric ID or exact name of the workflow to copy, from listWorkflowTypes."
        },
        "name": {
          "type": "string",
          "description": "The name of the copy. Defaults to 'Copy-' followed by the name of the original."
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
    public override string Description => "Creates a copy of a workflow under a new name, with its published activities, transitions, variables and settings. The copy is created disabled so it does not react to the same events as the original.";

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

        var store = services.GetRequiredService<IWorkflowTypeStore>();
        var reference = arguments.GetFirstValueOrDefault<string>("workflowTypeId");
        var source = await WorkflowToolSupport.FindWorkflowTypeAsync(store, reference);

        if (source is null)
        {
            return $"Nothing was copied: there is no workflow '{reference}'. Call listWorkflowTypes to find its ID.";
        }

        var name = arguments.GetFirstValueOrDefault<string>("name")?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            name = "Copy-" + source.Name;
        }

        var copy = new WorkflowType
        {
            Name = name,
            IsEnabled = false,
            Activities = source.Activities.Select(activity => activity.Clone()).ToList(),
            Transitions = source.Transitions.Select(transition => transition.Clone()).ToList(),
            Variables = source.Variables.Select(variable => variable.Clone()).ToList(),
        };

        source.CopyExecutionSettingsTo(copy);
        copy.WorkflowTypeId = services.GetRequiredService<IWorkflowTypeIdGenerator>().GenerateUniqueId(copy);

        // The URL of an HTTP Request event is signed for the original workflow, so the copy would start the original.
        var httpEvents = copy.Activities.Where(activity => activity.Name == WorkflowToolSupport.HttpRequestEventName).ToList();

        foreach (var activity in httpEvents)
        {
            activity.Properties.Remove("Url");
        }

        await store.SaveAsync(copy);

        return AgentToolResults.Json(new
        {
            copied = true,
            source = new
            {
                workflowTypeId = source.WorkflowTypeId,
                name = source.Name,
            },
            id = copy.Id,
            workflowTypeId = copy.WorkflowTypeId,
            name = copy.Name,
            isEnabled = copy.IsEnabled,
            activities = copy.Activities.Count,
            designerUrl = WorkflowToolSupport.GetDesignerPath(services, copy),
            note = httpEvents.Count == 0
                ? "The copy is disabled. Turn it on with enableWorkflowType when the user wants it to run."
                : "The copy is disabled. Its HTTP Request events have no URL yet: generate them in the designer before the copy is called.",
        });
    }
}
