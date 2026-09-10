using System.Globalization;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Provides the list of selectable countries and resolves the soft phone's default country code.
/// </summary>
internal static class SoftPhoneCountries
{
    private static readonly IReadOnlyList<SoftPhoneCountry> _countries = BuildCountries();

    /// <summary>
    /// Gets the distinct list of ISO 3166-1 alpha-2 countries, ordered by display name.
    /// </summary>
    public static IReadOnlyList<SoftPhoneCountry> All
        => _countries;

    /// <summary>
    /// Resolves the effective ISO 3166-1 alpha-2 country code, in lower case, used to initialize the
    /// soft phone's phone number input.
    /// </summary>
    /// <param name="configuredCountryCode">The explicitly configured country code, if any.</param>
    /// <returns>
    /// The configured country code when provided; otherwise the region derived from the current
    /// culture; or an empty string when the region cannot be determined.
    /// </returns>
    public static string ResolveDefaultCountryCode(string configuredCountryCode)
    {
        if (!string.IsNullOrWhiteSpace(configuredCountryCode))
        {
            return configuredCountryCode.Trim().ToLowerInvariant();
        }

        try
        {
            var region = new RegionInfo(CultureInfo.CurrentCulture.Name);

            return region.TwoLetterISORegionName.ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    private static List<SoftPhoneCountry> BuildCountries()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var countries = new List<SoftPhoneCountry>();

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

            if (seen.Add(region.TwoLetterISORegionName))
            {
                countries.Add(new SoftPhoneCountry(region.TwoLetterISORegionName.ToLowerInvariant(), region.EnglishName));
            }
        }

        countries.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

        return countries;
    }
}
