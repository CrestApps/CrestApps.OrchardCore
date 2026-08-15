using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Defines the persistence contract for <see cref="TaxJurisdiction"/> entries.
/// </summary>
public interface ITaxJurisdictionStore : INamedCatalog<TaxJurisdiction>
{
}
