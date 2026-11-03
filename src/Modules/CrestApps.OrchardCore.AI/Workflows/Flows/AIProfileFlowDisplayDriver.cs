using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Flows;

/// <summary>
/// Chooses the conversation flow an AI profile runs, on the profile's Instructions tab.
/// </summary>
public sealed class AIProfileFlowDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IWorkflowTypeStore _workflowTypeStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="workflowTypeStore">The workflow type store, which lists the flows.</param>
    public AIProfileFlowDisplayDriver(IWorkflowTypeStore workflowTypeStore)
    {
        _workflowTypeStore = workflowTypeStore;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<AIProfileFlowViewModel>("AIProfileFlow_Edit", async model =>
        {
            var settings = profile.GetSettings<AIProfileFlowSettings>();
            model.WorkflowTypeId = settings.WorkflowTypeId;
            model.Flexible = settings.Flexible;
            model.Flows = (await _workflowTypeStore.ListAsync())
                .Where(type => type.Activities.Any(activity => activity.IsStart && activity.Name == nameof(AIConversationFlowStartedEvent)))
                .OrderBy(type => type.Name)
                .Select(type => new SelectListItem(type.Name, type.WorkflowTypeId))
                .ToList();
        }).Location("Content:9%Instructions;4");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new AIProfileFlowViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.AlterSettings<AIProfileFlowSettings>(settings =>
        {
            settings.WorkflowTypeId = string.IsNullOrWhiteSpace(model.WorkflowTypeId) ? null : model.WorkflowTypeId;
            settings.Flexible = model.Flexible;
        });

        return Edit(profile, context);
    }
}
