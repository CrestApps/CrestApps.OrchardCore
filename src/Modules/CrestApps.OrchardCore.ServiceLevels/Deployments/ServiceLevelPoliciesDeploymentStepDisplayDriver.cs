using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ServiceLevels.Deployments;

internal sealed class ServiceLevelPoliciesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ServiceLevelPoliciesDeploymentStep>
{
    public override IDisplayResult Display(ServiceLevelPoliciesDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("ServiceLevelPoliciesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ServiceLevelPoliciesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(ServiceLevelPoliciesDeploymentStep step, BuildEditorContext context)
        => View("ServiceLevelPoliciesDeploymentStep_Fields_Edit", step).Location("Content");
}
