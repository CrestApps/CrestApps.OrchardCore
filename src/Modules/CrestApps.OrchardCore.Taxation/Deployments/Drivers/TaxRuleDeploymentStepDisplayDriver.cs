using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Deployments.Drivers;

internal sealed class TaxRuleDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TaxRuleDeploymentStep>
{
    public override IDisplayResult Display(TaxRuleDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("TaxRuleDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TaxRuleDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
