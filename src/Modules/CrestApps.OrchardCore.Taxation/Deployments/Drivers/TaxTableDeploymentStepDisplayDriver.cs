using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Deployments.Drivers;

internal sealed class TaxTableDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TaxTableDeploymentStep>
{
    public override IDisplayResult Display(TaxTableDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("TaxTableDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TaxTableDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
