using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Timeline.Deployments;

internal sealed class TimelineEntriesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TimelineEntriesDeploymentStep>
{
    internal readonly IStringLocalizer S;

    public TimelineEntriesDeploymentStepDisplayDriver(IStringLocalizer<TimelineEntriesDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

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
        }).Location(l => l.Zone("Content", "1").Card(S["Timeline Entries"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(TimelineEntriesDeploymentStep step, UpdateEditorContext context)
    {
        var model = new TimelineEntriesDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        step.SubjectKinds = model.SubjectKinds?.Trim();

        return Edit(step, context);
    }
}
