using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that design, run and manage workflows and their instances, when the Workflows feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkflowsToolsStartup(IStringLocalizer<WorkflowsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetWorkflowActivityTool>(GetWorkflowActivityTool.TheName)
            .WithTitle(S["Get Workflow Activity"])
            .WithDescription(S["Returns, for up to 10 activity names, the JSON schema of the activity's Properties, its outcomes (and whether they depend on its settings), its outputs and the values it provides to later activities. Call it for every activity type before adding or editing one."])
            .WithCategory(S["Workflow Management"])
            .Selectable();

        services.AddCoreAITool<SaveWorkflowTypeTool>(SaveWorkflowTypeTool.TheName)
            .WithTitle(S["Save Workflow Type"])
            .WithDescription(S["Creates a workflow (disabled unless told otherwise) from a full definition: settings, variables, activities and transitions. The result lists the validation issues to fix, and validateOnly checks a definition without saving it."])
            .WithCategory(S["Workflow Management"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ListWorkflowTypeVersionsTool>(ListWorkflowTypeVersionsTool.TheName)
            .WithTitle(S["List Workflow Type Versions"])
            .WithDescription(S["Lists a workflow's versions (number, who published it, when)."])
            .WithCategory(S["Workflow Management"])
            .Selectable();

        services.AddCoreAITool<CloneWorkflowTypeTool>(CloneWorkflowTypeTool.TheName)
            .WithTitle(S["Clone Workflow Type"])
            .WithDescription(S["Creates a copy of a workflow under a new name, with its activities, transitions, variables and settings. The copy is created disabled so it does not react to the same events as the original."])
            .WithCategory(S["Workflow Management"])
            .Selectable();

        services.AddCoreAITool<EnableWorkflowTypeTool>(EnableWorkflowTypeTool.TheName)
            .WithTitle(S["Enable Workflow Type"])
            .WithDescription(S["Enables a workflow so its start events start instances again. Takes effect at once, without publishing."])
            .WithCategory(S["Workflow Management"])
            .Selectable();

        services.AddCoreAITool<DisableWorkflowTypeTool>(DisableWorkflowTypeTool.TheName)
            .WithTitle(S["Disable Workflow Type"])
            .WithDescription(S["Disables a workflow: its events no longer start instances and its HTTP URL answers not found. Instances that already wait are kept, not cancelled."])
            .WithCategory(S["Workflow Management"])
            .Selectable();

        services.AddCoreAITool<DeleteWorkflowTypeTool>(DeleteWorkflowTypeTool.TheName)
            .WithTitle(S["Delete Workflow Type"])
            .WithDescription(S["Permanently deletes one or more workflows with their versions, draft, instances and journals. Prefer disableWorkflowType when the user only wants a workflow to stop."])
            .WithCategory(S["Workflow Management"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ListWorkflowInstancesTool>(ListWorkflowInstancesTool.TheName)
            .WithTitle(S["List Workflow Instances"])
            .WithDescription(S["Lists the instances of one workflow or of all workflows, with paging. Each row has the instance id, workflow, version, status, creation date, fault message, what it waits on and when a retry is due."])
            .WithCategory(S["Workflow Management"])
            .Selectable();

        services.AddCoreAITool<GetWorkflowInstanceTool>(GetWorkflowInstanceTool.TheName)
            .WithTitle(S["Get Workflow Instance"])
            .WithDescription(S["Returns one instance: status, the version it runs on, the activities it waits on, its variable values, its fault and faulted activity, pending retry, parent and child instances, and its journal (activities run, outcomes, durations, errors). Use it to explain what a run did or why it failed; pass sequence to get the recorded data of one activity run."])
            .WithCategory(S["Workflow Management"])
            .Selectable();

        services.AddCoreAITool<RunWorkflowTypeTool>(RunWorkflowTypeTool.TheName)
            .WithTitle(S["Run Workflow Type"])
            .WithDescription(S["Starts a new instance of an enabled workflow's published version that starts with \"Started by Workflow\", with values for its input variables, and returns the instance id, status, fault and output variables. Workflows that start with another event cannot be started this way; for an HTTP Request start it returns the URL and method instead."])
            .WithCategory(S["Workflow Management"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<RerunWorkflowInstancesTool>(RerunWorkflowInstancesTool.TheName)
            .WithTitle(S["Rerun Workflow Instances"])
            .WithDescription(S["Runs instances again: retry continues faulted instances from the activity that faulted (or a given activity) with their state; restart starts a new instance on the published version with the original input. Activities run again, so emails, HTTP calls and content changes can repeat."])
            .WithCategory(S["Workflow Management"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<CancelWorkflowInstancesTool>(CancelWorkflowInstancesTool.TheName)
            .WithTitle(S["Cancel Workflow Instances"])
            .WithDescription(S["Cancels instances that have not ended: they become Aborted, stop waiting for their events and drop a pending retry. A cancelled instance cannot be resumed."])
            .WithCategory(S["Workflow Management"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteWorkflowInstancesTool>(DeleteWorkflowInstancesTool.TheName)
            .WithTitle(S["Delete Workflow Instances"])
            .WithDescription(S["Permanently deletes instances and their journals. Use cancelWorkflowInstances when the history should be kept."])
            .WithCategory(S["Workflow Management"])
            .RequireApproval()
            .Selectable();
    }
}
