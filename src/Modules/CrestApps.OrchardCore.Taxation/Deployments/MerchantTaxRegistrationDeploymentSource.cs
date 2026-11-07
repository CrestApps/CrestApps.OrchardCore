using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Recipes;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Deployments;

/// <summary>
/// Writes every tax registration into a <c>MerchantTaxRegistration</c> recipe step, keyed by its id and its name.
/// </summary>
internal sealed class MerchantTaxRegistrationDeploymentSource : CatalogStoreDeploymentSource<MerchantTaxRegistrationDeploymentStep, MerchantTaxRegistration>
{
    public MerchantTaxRegistrationDeploymentSource(IMerchantTaxRegistrationStore store)
        : base(MerchantTaxRegistrationStep.EntriesName, store)
    {
    }

    protected override IEnumerable<MerchantTaxRegistration> Order(IEnumerable<MerchantTaxRegistration> entries)
        => entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
}
