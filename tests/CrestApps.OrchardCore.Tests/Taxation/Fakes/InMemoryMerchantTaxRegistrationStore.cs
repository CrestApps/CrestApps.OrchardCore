using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Tests.Taxation.Fakes;

/// <summary>
/// In-memory <see cref="IMerchantTaxRegistrationStore"/> used by the taxation tests.
/// </summary>
public sealed class InMemoryMerchantTaxRegistrationStore : InMemoryNamedCatalog<MerchantTaxRegistration>, IMerchantTaxRegistrationStore
{
}
