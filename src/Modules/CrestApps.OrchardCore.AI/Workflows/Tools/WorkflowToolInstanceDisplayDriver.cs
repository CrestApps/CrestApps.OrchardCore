using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Tools;

/// <summary>
/// Edits the workflow a <c>Workflow</c> tool instance runs: one of the workflows that start with the
/// <c>AI Tool Called</c> event.
/// </summary>
internal sealed class WorkflowToolInstanceDisplayDriver : DisplayDriver<AIToolInstance>
{
    private readonly IWorkflowTypeStore _workflowTypeStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowToolInstanceDisplayDriver"/> class.
    /// </summary>
    /// <param name="workflowTypeStore">The workflow type store, which lists the workflows to choose from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkflowToolInstanceDisplayDriver(
        IWorkflowTypeStore workflowTypeStore,
        IStringLocalizer<WorkflowToolInstanceDisplayDriver> stringLocalizer)
    {
        _workflowTypeStore = workflowTypeStore;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        if (!IsWorkflowSource(instance))
        {
            return null;
        }

        return Initialize<WorkflowToolInstanceViewModel>("WorkflowToolInstance_Edit", async model =>
        {
            var settings = instance.GetOrCreate<WorkflowToolSettings>();
            model.WorkflowTypeId = settings.WorkflowTypeId;
            model.TimeoutSeconds = settings.TimeoutSeconds;
            model.WorkflowTypes = (await _workflowTypeStore.ListAsync())
                .Where(type => type.Activities.Any(activity => activity.IsStart && activity.Name == nameof(AIToolCalledEvent)))
                .OrderBy(type => type.Name)
                .Select(type => new SelectListItem(type.Name, type.WorkflowTypeId))
                .ToList();
        }).Location("Content:5");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        if (!IsWorkflowSource(instance))
        {
            return null;
        }

        var model = new WorkflowToolInstanceViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.WorkflowTypeId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.WorkflowTypeId), S["Choose the workflow this tool runs."]);
        }

        if (model.TimeoutSeconds is < 0 or > 300)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TimeoutSeconds), S["The time limit must be between 0 and 300 seconds."]);
        }

        instance.Put(new WorkflowToolSettings
        {
            WorkflowTypeId = model.WorkflowTypeId?.Trim(),
            TimeoutSeconds = model.TimeoutSeconds,
        });

        return Edit(instance, context);
    }

    private static bool IsWorkflowSource(AIToolInstance instance)
        => string.Equals(instance.Source, WorkflowToolConstants.SourceName, StringComparison.OrdinalIgnoreCase);
}
