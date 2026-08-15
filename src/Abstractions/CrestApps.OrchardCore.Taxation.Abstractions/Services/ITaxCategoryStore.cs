using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Defines the persistence contract for <see cref="TaxCategory"/> entries.
/// </summary>
public interface ITaxCategoryStore : INamedCatalog<TaxCategory>
{
}
