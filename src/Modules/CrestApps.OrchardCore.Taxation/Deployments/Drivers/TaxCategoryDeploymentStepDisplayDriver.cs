using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Deployments.Drivers;

internal sealed class TaxCategoryDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TaxCategoryDeploymentStep>
{
    public override IDisplayResult Display(TaxCategoryDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("TaxCategoryDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TaxCategoryDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
