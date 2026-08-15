using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Defines the persistence contract for <see cref="TaxRule"/> entries.
/// </summary>
public interface ITaxRuleStore : INamedCatalog<TaxRule>
{
}
