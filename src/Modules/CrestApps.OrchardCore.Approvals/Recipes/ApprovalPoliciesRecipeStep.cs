using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.Approvals.Recipes;

/// <summary>
/// Imports approval policies, matched by id and then by technical name.
/// </summary>
internal sealed class ApprovalPoliciesRecipeStep : NamedCatalogRecipeStep<ApprovalPolicy>
{
    /// <summary>
    /// The property that holds the policies.
    /// </summary>
    public const string EntriesName = "Policies";

    public ApprovalPoliciesRecipeStep(INamedCatalogManager<ApprovalPolicy> manager)
        : base(ApprovalsConstants.PoliciesRecipeStepName, EntriesName, manager)
    {
    }

    protected override bool MatchByName => true;
}
