using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Defines the persistence contract for <see cref="TaxTable"/> entries.
/// </summary>
public interface ITaxTableStore : INamedCatalog<TaxTable>
{
}
