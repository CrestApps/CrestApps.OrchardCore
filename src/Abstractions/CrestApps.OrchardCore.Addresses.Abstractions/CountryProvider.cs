using System.Globalization;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Addresses;

/// <summary>
/// Provides the canonical list of ISO 3166-1 countries. The list is derived from the runtime globalization
/// data so it stays consistent with the platform and does not require a hardcoded dataset.
/// </summary>
public static class CountryProvider
{
    private static readonly IReadOnlyList<CountryInfo> _countries = BuildCountries();

    /// <summary>
    /// Gets the countries ordered by their English display name.
    /// </summary>
    /// <returns>A read-only, ordered list of countries.</returns>
    public static IReadOnlyList<CountryInfo> GetCountries()
    {
        return _countries;
    }

    private static CountryInfo[] BuildCountries()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var countries = new List<CountryInfo>();

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            RegionInfo region;

            try
            {
                region = new RegionInfo(culture.Name);
            }
            catch (ArgumentException)
            {
                continue;
            }

            var code = region.TwoLetterISORegionName;

            if (!IsIsoAlpha2Code(code))
            {
                continue;
            }

            if (seen.Add(code))
            {
                countries.Add(new CountryInfo(code, region.EnglishName));
            }
        }

        return countries
            .OrderBy(country => country.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static bool IsIsoAlpha2Code(string code)
    {
        if (code is null || code.Length != 2)
        {
            return false;
        }

        return char.IsAsciiLetterUpper(code[0]) && char.IsAsciiLetterUpper(code[1]);
    }
}
