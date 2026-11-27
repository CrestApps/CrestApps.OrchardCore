using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Calendars.Deployments;

internal sealed class CalendarsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, CalendarsDeploymentStep>
{
    internal readonly IStringLocalizer S;

    public CalendarsDeploymentStepDisplayDriver(IStringLocalizer<CalendarsDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(CalendarsDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("CalendarsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("CalendarsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(CalendarsDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<CalendarsDeploymentStepViewModel>("CalendarsDeploymentStep_Fields_Edit", model =>
        {
            model.IncludeEvents = step.IncludeEvents;
            model.IncludeCategories = step.IncludeCategories;
        }).Location(l => l.Zone("Content", "1").Card(S["Calendars"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(CalendarsDeploymentStep step, UpdateEditorContext context)
    {
        var model = new CalendarsDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        step.IncludeEvents = model.IncludeEvents;
        step.IncludeCategories = model.IncludeCategories;

        return Edit(step, context);
    }
}
