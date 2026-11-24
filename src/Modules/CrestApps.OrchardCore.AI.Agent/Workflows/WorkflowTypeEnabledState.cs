using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Turns a workflow on or off on its live workflow type, as the designer's settings do: the enabled state is not part
/// of a version, so it applies at once without publishing and creates no new version.
/// </summary>
internal static class WorkflowTypeEnabledState
{
    /// <summary>
    /// Enables or disables a workflow for the tool invocation, as its <c>enabled</c> argument says.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <returns>The result of the tool.</returns>
    public static async Task<object> ChangeAsync(AIFunctionArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, WorkflowsPermissions.ManageWorkflows))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetValue("enabled", out var enabledValue) || enabledValue is null)
        {
            return "Nothing was changed: give enabled, true to enable the workflow or false to disable it.";
        }

        var enable = arguments.GetFirstValueOrDefault("enabled", false);
        var store = services.GetRequiredService<IWorkflowTypeStore>();
        var reference = arguments.GetFirstValueOrDefault<string>("workflowTypeId");
        var workflowType = await WorkflowToolSupport.FindWorkflowTypeAsync(store, reference);

        if (workflowType is null)
        {
            return $"Nothing was changed: there is no workflow '{reference}'. Call listWorkflowTypes to find its ID.";
        }

        var changed = workflowType.IsEnabled != enable;

        if (changed)
        {
            workflowType.IsEnabled = enable;
            await store.SaveAsync(workflowType);
        }

        return AgentToolResults.Json(new
        {
            workflowTypeId = workflowType.WorkflowTypeId,
            name = workflowType.Name,
            isEnabled = workflowType.IsEnabled,
            changed,
            note = !changed
                ? $"The workflow was already {(enable ? "enabled" : "disabled")}; nothing was changed."
                : (enable
                    ? "The workflow is enabled: its start events start instances from now on. "
                    : "The workflow is disabled: its events no longer start instances and its HTTP URLs answer not found. Instances that already wait are kept, not cancelled. ") + WorkflowToolSupport.DraftNote,
        });
    }
}
