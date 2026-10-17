using CrestApps.OrchardCore.Stripe.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Stripe.Deployments.Drivers;

internal sealed class StripeSettingsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, StripeSettingsDeploymentStep>
{
    public override IDisplayResult Display(StripeSettingsDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("StripeSettingsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("StripeSettingsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
