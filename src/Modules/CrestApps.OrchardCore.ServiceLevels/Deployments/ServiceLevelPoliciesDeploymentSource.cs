using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Recipes;
using CrestApps.OrchardCore.ServiceLevels.Services;

namespace CrestApps.OrchardCore.ServiceLevels.Deployments;

/// <summary>
/// Exports every service level policy as the <c>ServiceLevelPolicies</c> recipe step.
/// </summary>
internal sealed class ServiceLevelPoliciesDeploymentSource : CatalogDeploymentSource<ServiceLevelPoliciesDeploymentStep, ServiceLevelPolicy>
{
    public ServiceLevelPoliciesDeploymentSource(IServiceLevelPolicyManager manager)
        : base(ServiceLevelPoliciesRecipeStep.EntriesName, manager)
    {
    }
}
