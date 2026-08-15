using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Tests.Taxation.Fakes;

/// <summary>
/// In-memory <see cref="ITaxTableStore"/> used by the taxation tests.
/// </summary>
public sealed class InMemoryTaxTableStore : InMemoryNamedCatalog<TaxTable>, ITaxTableStore
{
}
