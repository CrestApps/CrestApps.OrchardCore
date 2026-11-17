using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that chooses a user task action for a content item, when the features the Workflows module's user
/// tasks need (Workflows, Contents and Roles) are on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows, OrchardCoreFeatureIds.Contents, OrchardCoreFeatureIds.Roles)]
public sealed class WorkflowUserTasksToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowUserTasksToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkflowUserTasksToolsStartup(IStringLocalizer<WorkflowUserTasksToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<CompleteWorkflowUserTaskTool>(CompleteWorkflowUserTaskTool.TheName)
            .WithTitle(S["Complete Workflow User Task"])
            .WithDescription(S["Chooses a user task action, such as Approve or Reject, for a content item that a workflow is waiting on, which resumes that workflow on the matching outcome. Called without an action, or with an unknown one, it returns the actions the current user may choose for that item."])
            .WithCategory(S["Workflow Management"])
            .RequireApproval()
            .Selectable();
    }
}
