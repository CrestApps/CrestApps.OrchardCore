using System.Text;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// The default <see cref="IPayrollHomeJurisdictionResolver"/>: the country and the country with its region of the
/// home address (US, US-MI), the city by the convention <c>{Country}-{Region}-{CITY}</c> (the city upper-case, letters
/// and digits only: US-MI-DETROIT, US-MI-GRANDRAPIDS), and the code of every Taxation tax jurisdiction whose country,
/// region, county, city and postal code match the address (Taxation's jurisdiction resolver) when the code starts with
/// the country, such as a county's US-OH-FRANKLIN.
/// </summary>
public sealed class PayrollHomeJurisdictionResolver : IPayrollHomeJurisdictionResolver
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollHomeJurisdictionResolver"/> class.
    /// </summary>
    /// <param name="services">The services, for Taxation's jurisdiction resolver when it is there.</param>
    public PayrollHomeJurisdictionResolver(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ResolveAsync(EmployeePayProfile profile, DateOnly on, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var jurisdictions = PayrollHomeJurisdictions.FromAddress(profile.HomeAddress);

        if (jurisdictions.Count == 0 || _services.GetService<ITaxJurisdictionResolver>() is not { } resolver)
        {
            return jurisdictions;
        }

        var country = jurisdictions[0];

        foreach (var jurisdiction in await resolver.ResolveAsync(profile.HomeAddress, on.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), cancellationToken))
        {
            var code = jurisdiction?.Code?.Trim().ToUpperInvariant();

            if (!string.IsNullOrEmpty(code) && code.StartsWith(country + "-", StringComparison.Ordinal) && !jurisdictions.Contains(code, StringComparer.Ordinal))
            {
                jurisdictions.Add(code);
            }
        }

        return jurisdictions;
    }
}

/// <summary>
/// The home jurisdictions an address gives by convention, without any jurisdiction set up.
/// </summary>
public static class PayrollHomeJurisdictions
{
    /// <summary>
    /// Gets the country, the country with its region and the city of an address, such as US, US-MI and US-MI-DETROIT.
    /// </summary>
    /// <param name="address">The address, or <see langword="null"/>.</param>
    /// <returns>The codes; none without a country.</returns>
    public static List<string> FromAddress(Address address)
    {
        var jurisdictions = new List<string>();

        if (string.IsNullOrWhiteSpace(address?.Country))
        {
            return jurisdictions;
        }

        var country = address.Country.Trim().ToUpperInvariant();

        jurisdictions.Add(country);

        if (string.IsNullOrWhiteSpace(address.Region))
        {
            return jurisdictions;
        }

        var region = $"{country}-{address.Region.Trim().ToUpperInvariant()}";

        jurisdictions.Add(region);

        if (CityCode(address.City) is { Length: > 0 } city)
        {
            jurisdictions.Add($"{region}-{city}");
        }

        return jurisdictions;
    }

    /// <summary>
    /// Gets the part of a jurisdiction code a city name gives: upper-case, letters and digits only (St. Clair Shores
    /// gives STCLAIRSHORES).
    /// </summary>
    /// <param name="city">The city.</param>
    /// <returns>The code, or an empty string.</returns>
    public static string CityCode(string city)
    {
        var code = new StringBuilder();

        foreach (var character in city ?? string.Empty)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                code.Append(char.ToUpperInvariant(character));
            }
        }

        return code.ToString();
    }
}
