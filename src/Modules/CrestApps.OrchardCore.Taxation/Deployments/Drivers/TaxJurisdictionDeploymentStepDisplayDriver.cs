using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Deployments.Drivers;

internal sealed class TaxJurisdictionDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TaxJurisdictionDeploymentStep>
{
    public override IDisplayResult Display(TaxJurisdictionDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("TaxJurisdictionDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TaxJurisdictionDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
