using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Drivers;

internal sealed class OmnichannelSubjectFlowSettingsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, OmnichannelSubjectFlowSettingsDeploymentStep>
{
    public override IDisplayResult Display(OmnichannelSubjectFlowSettingsDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("OmnichannelSubjectFlowSettingsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("OmnichannelSubjectFlowSettingsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }
}
