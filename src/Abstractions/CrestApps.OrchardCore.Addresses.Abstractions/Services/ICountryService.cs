using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Addresses.Services;

/// <summary>
/// Provides the set of countries available for selection across the platform. Implementations may source
/// the countries from the runtime globalization data or from user-managed country content items.
/// </summary>
public interface ICountryService
{
    /// <summary>
    /// Gets the countries available for selection, ordered by their display name.
    /// </summary>
    /// <returns>A read-only, ordered list of countries.</returns>
    ValueTask<IReadOnlyList<CountryInfo>> GetCountriesAsync();
}
