using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Recipes;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.Approvals.Deployments;

internal sealed class ApprovalPoliciesDeploymentSource : NamedCatalogDeploymentSource<ApprovalPoliciesDeploymentStep, ApprovalPolicy>
{
    public ApprovalPoliciesDeploymentSource(INamedCatalogManager<ApprovalPolicy> manager)
        : base(ApprovalPoliciesRecipeStep.EntriesName, manager)
    {
    }
}
