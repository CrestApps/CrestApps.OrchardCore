using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Tests.Taxation.Fakes;

/// <summary>
/// In-memory <see cref="ITaxJurisdictionStore"/> used by the taxation tests.
/// </summary>
public sealed class InMemoryTaxJurisdictionStore : InMemoryNamedCatalog<TaxJurisdiction>, ITaxJurisdictionStore
{
}
