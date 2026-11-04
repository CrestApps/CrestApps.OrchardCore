using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Teams.Deployments;

internal sealed class TeamsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TeamsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(TeamsDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("TeamsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TeamsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(TeamsDeploymentStep step, BuildEditorContext context)
    {
        return View("TeamsDeploymentStep_Fields_Edit", step).Location("Content");
    }
}
