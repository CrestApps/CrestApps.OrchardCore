using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Deployments.Drivers;

internal sealed class TaxTypeDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TaxTypeDeploymentStep>
{
    public override IDisplayResult Display(TaxTypeDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("TaxTypeDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TaxTypeDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
