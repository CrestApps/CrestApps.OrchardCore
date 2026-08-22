using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Addresses.Services;

/// <summary>
/// The default <see cref="ICountryService"/> implementation that sources the countries from the runtime
/// globalization data through <see cref="CountryProvider"/>. It is used whenever the Addresses module is
/// not enabled to manage countries as content items.
/// </summary>
public sealed class DefaultCountryService : ICountryService
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<CountryInfo>> GetCountriesAsync()
    {
        return ValueTask.FromResult(CountryProvider.GetCountries());
    }
}
