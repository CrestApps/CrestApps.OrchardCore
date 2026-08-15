using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Tests.Taxation.Fakes;

/// <summary>
/// In-memory <see cref="ITaxRuleStore"/> used by the taxation tests.
/// </summary>
public sealed class InMemoryTaxRuleStore : InMemoryNamedCatalog<TaxRule>, ITaxRuleStore
{
}
