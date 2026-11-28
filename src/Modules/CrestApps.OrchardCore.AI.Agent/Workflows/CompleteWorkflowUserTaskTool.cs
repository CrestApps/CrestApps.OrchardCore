using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Workflows;
using OrchardCore.Contents;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Chooses a user task action for a content item, as the "{Action} and Save" buttons of the content editor do: the
/// caller must be allowed to edit the item, and may choose only the actions of the User Task events that wait on it
/// whose roles the caller has (any action when the event names no role). The tool triggers the event with the action;
/// unlike the button it does not save or publish the item.
/// </summary>
public sealed class CompleteWorkflowUserTaskTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "completeWorkflowUserTask";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ID of the content item the workflow waits on."
        },
        "action": {
          "type": "string",
          "description": "The action to choose, such as Approve or Reject. Leave it out to get the actions the current user may choose for this item."
        }
      },
      "required": [
        "contentItemId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Chooses a user task action, such as Approve or Reject, for a content item that a workflow is waiting on, which resumes that workflow on the matching outcome. Called without an action, or with an unknown one, it returns the actions the current user may choose for that item.";

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
        var user = AgentToolAuthorization.GetCaller(services);

        if (user is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var contentItemId = arguments.GetFirstValueOrDefault<string>("contentItemId")?.Trim();

        if (string.IsNullOrEmpty(contentItemId))
        {
            return "Give the ID of the content item the workflow waits on.";
        }

        var contentItem = await services.GetRequiredService<IContentManager>().GetAsync(contentItemId, VersionOptions.Latest);

        if (contentItem is null)
        {
            return $"There is no content item '{contentItemId}'.";
        }

        // The buttons are only in the item's editor.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.EditContent, contentItem))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var available = await GetUserTaskActionsAsync(services.GetRequiredService<IWorkflowStore>(), contentItemId, user);
        var action = arguments.GetFirstValueOrDefault<string>("action")?.Trim();
        var chosen = string.IsNullOrEmpty(action) ? null : available.FirstOrDefault(item => string.Equals(item, action, StringComparison.OrdinalIgnoreCase));

        if (chosen is null)
        {
            return AgentToolResults.Json(new
            {
                completed = false,
                contentItemId,
                availableActions = available,
                message = available.Count == 0
                    ? "No workflow waits on a user task for this item that you may act on."
                    : string.IsNullOrEmpty(action)
                        ? "Choose one of the available actions."
                        : $"'{action}' is not an action you may choose for this item. Choose one of the available actions.",
            });
        }

        var input = new Dictionary<string, object>
        {
            [ContentEventConstants.UserActionInputKey] = chosen,
            [ContentEventConstants.ContentItemInputKey] = contentItem,
            [ContentEventConstants.ContentEventInputKey] = new ContentEventContext
            {
                Name = WorkflowToolSupport.UserTaskEventName,
                ContentType = contentItem.ContentType,
                ContentItemId = contentItem.ContentItemId,
                ContentItemVersionId = contentItem.ContentItemVersionId,
            },
        };

        var contexts = (await services.GetRequiredService<IWorkflowManager>()
            .TriggerEventAsync(WorkflowToolSupport.UserTaskEventName, input, correlationId: contentItem.ContentItemId)).ToList();

        return AgentToolResults.Json(new
        {
            completed = true,
            contentItemId,
            action = chosen,
            runs = contexts.Select(context => new
            {
                instanceId = context.Workflow.Id == 0 ? (long?)null : context.Workflow.Id,
                workflowId = context.Workflow.WorkflowId,
                workflowName = context.WorkflowType?.Name,
                status = context.Status.ToString(),
                faultMessage = WorkflowToolSupport.Truncate(context.Workflow.FaultMessage),
            }).ToList(),
            note = "The content item itself was not saved or published; use the content tools if that is wanted too.",
        });
    }

    // The actions of the User Task events that wait on the item, for the roles of the caller, as the content editor
    // lists them. Each event keeps its actions and roles in the state of the instance.
    private static async Task<List<string>> GetUserTaskActionsAsync(IWorkflowStore workflowStore, string contentItemId, ClaimsPrincipal user)
    {
        var workflows = await workflowStore.ListByActivityNameAsync(WorkflowToolSupport.UserTaskEventName, contentItemId);
        var userRoles = user.Claims.Where(claim => claim.Type == ClaimTypes.Role).Select(claim => claim.Value).ToList();
        var actions = new List<string>();

        foreach (var workflow in workflows)
        {
            var activityStates = workflow.State?
                .FirstOrDefault(property => string.Equals(property.Key, nameof(WorkflowState.ActivityStates), StringComparison.OrdinalIgnoreCase))
                .Value as JsonObject;

            foreach (var blockingActivity in workflow.BlockingActivities.Where(activity => activity.Name == WorkflowToolSupport.UserTaskEventName))
            {
                if (activityStates?[blockingActivity.ActivityId] is not JsonObject activityState)
                {
                    continue;
                }

                var roles = ReadStrings(activityState, "Roles");

                if (roles.Count > 0 && !userRoles.Any(roles.Contains))
                {
                    continue;
                }

                foreach (var action in ReadStrings(activityState, "Actions").Where(action => !actions.Contains(action)))
                {
                    actions.Add(action);
                }
            }
        }

        return actions;
    }

    private static List<string> ReadStrings(JsonObject state, string key)
        => state[key] is JsonArray values
            ? values.OfType<JsonValue>().Select(value => value.TryGetValue<string>(out var text) ? text : null).Where(text => !string.IsNullOrEmpty(text)).ToList()
            : [];
}
