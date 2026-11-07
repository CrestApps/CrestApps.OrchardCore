using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Recipes;
using CrestApps.OrchardCore.Customers.Services;

namespace CrestApps.OrchardCore.Customers.Deployments;

/// <summary>
/// Writes every referral profile into a <c>ReferralProfiles</c> recipe step, keyed by its id and its customer.
/// </summary>
internal sealed class ReferralProfilesDeploymentSource : CatalogStoreDeploymentSource<ReferralProfilesDeploymentStep, ReferralProfile>
{
    public ReferralProfilesDeploymentSource(IReferralStore store)
        : base(ReferralProfilesStep.EntriesName, store)
    {
    }
}
