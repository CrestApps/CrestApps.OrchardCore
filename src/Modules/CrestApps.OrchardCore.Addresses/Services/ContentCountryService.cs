using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Addresses.Indexes;
using CrestApps.OrchardCore.Addresses.Models;
using YesSql;

namespace CrestApps.OrchardCore.Addresses.Services;

/// <summary>
/// Resolves the available countries from published <c>Country</c> content items, falling back to the canonical
/// ISO country list when no country content has been created yet.
/// </summary>
public sealed class ContentCountryService : ICountryService
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentCountryService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to query the geographic area index.</param>
    public ContentCountryService(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<CountryInfo>> GetCountriesAsync()
    {
        var indexed = await _session.QueryIndex<GeographicAreaIndex>(index =>
                index.ContentType == AddressConstants.Country && index.Published)
            .ListAsync();

        var countries = new List<CountryInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var area in indexed)
        {
            var code = area.Code?.Trim();

            if (string.IsNullOrWhiteSpace(code) || !seen.Add(code))
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(area.DisplayText)
                ? code
                : area.DisplayText;

            countries.Add(new CountryInfo(code.ToUpperInvariant(), name));
        }

        if (countries.Count == 0)
        {
            return CountryProvider.GetCountries();
        }

        return countries
            .OrderBy(country => country.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
