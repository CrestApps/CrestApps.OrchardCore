using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Timeline.Deployments;

internal sealed class TimelineEntriesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TimelineEntriesDeploymentStep>
{
    public override IDisplayResult Display(TimelineEntriesDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("TimelineEntriesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TimelineEntriesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(TimelineEntriesDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<TimelineEntriesDeploymentStepViewModel>("TimelineEntriesDeploymentStep_Fields_Edit", model =>
        {
            model.SubjectKinds = step.SubjectKinds;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(TimelineEntriesDeploymentStep step, UpdateEditorContext context)
    {
        var model = new TimelineEntriesDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        step.SubjectKinds = model.SubjectKinds?.Trim();

        return Edit(step, context);
    }
}
