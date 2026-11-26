using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ServiceLevels.Deployments;

internal sealed class ServiceLevelPoliciesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ServiceLevelPoliciesDeploymentStep>
{
    internal readonly IStringLocalizer S;

    public ServiceLevelPoliciesDeploymentStepDisplayDriver(IStringLocalizer<ServiceLevelPoliciesDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Display(ServiceLevelPoliciesDeploymentStep step, BuildDisplayContext context)
    {
        return Combine(
            View("ServiceLevelPoliciesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ServiceLevelPoliciesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(ServiceLevelPoliciesDeploymentStep step, BuildEditorContext context)
        => View("ServiceLevelPoliciesDeploymentStep_Fields_Edit", step)
            .Location(l => l.Zone("Content", "1").Card(S["Service Level Policies"], "10"));
}
