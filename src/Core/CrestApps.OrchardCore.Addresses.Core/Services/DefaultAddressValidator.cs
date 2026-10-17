using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;

namespace CrestApps.OrchardCore.Addresses.Core.Services;

public sealed class DefaultAddressValidator : IAddressValidator
{
    private readonly IAddressFormatProvider _formatProvider;
    private readonly ICountryService _countryService;

    public DefaultAddressValidator(IAddressFormatProvider formatProvider, ICountryService countryService)
    {
        _formatProvider = formatProvider;
        _countryService = countryService;
    }

    public async ValueTask<AddressValidationResult> ValidateAsync(Address address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var problems = new List<AddressProblem>();
        var country = Clean(address.Country)?.ToUpperInvariant();

        if (country is null)
        {
            problems.Add(new AddressProblem(AddressComponent.Country, AddressProblemKind.Missing));
        }
        else if (!(await _countryService.GetCountriesAsync()).Any(candidate => string.Equals(candidate.Code, country, StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add(new AddressProblem(AddressComponent.Country, AddressProblemKind.Unknown));
        }

        var format = await _formatProvider.GetFormatAsync(country);

        foreach (var component in format.Required)
        {
            if (IsEmpty(address, component))
            {
                problems.Add(new AddressProblem(component, AddressProblemKind.Missing));
            }
        }

        var postalCode = Clean(address.PostalCode)?.ToUpperInvariant();
        var postalCodeIsValid = postalCode is not null &&
            (format.PostalCodePattern is null || AddressPatterns.IsMatch(format.PostalCodePattern, postalCode));

        if (postalCode is not null && !postalCodeIsValid)
        {
            problems.Add(new AddressProblem(AddressComponent.PostalCode, AddressProblemKind.Invalid));
        }

        // A country with a list of regions takes one of them, by code, key or name; it is kept by its code.
        var region = Clean(address.Region);
        AddressRegion knownRegion = null;

        if (region is not null && format.Regions.Count > 0 && format.Uses(AddressComponent.Region))
        {
            knownRegion = format.FindRegion(region);

            if (knownRegion is null)
            {
                problems.Add(new AddressProblem(AddressComponent.Region, AddressProblemKind.Unknown));
            }
        }

        if (knownRegion?.PostalCodePrefix is not null && postalCodeIsValid && !AddressPatterns.StartsWith(knownRegion.PostalCodePrefix, postalCode))
        {
            problems.Add(new AddressProblem(AddressComponent.PostalCode, AddressProblemKind.NotInRegion));
        }

        var tidied = new Address
        {
            Name = Clean(address.Name),
            Company = Clean(address.Company),
            AddressLine1 = Clean(address.AddressLine1),
            AddressLine2 = Clean(address.AddressLine2),
            DependentLocality = Clean(address.DependentLocality),
            City = Clean(address.City),
            Region = knownRegion?.Code ?? region,
            County = Clean(address.County),
            District = Clean(address.District),
            PostalCode = postalCode,
            SortingCode = Clean(address.SortingCode),
            Country = country,
            Phone = Clean(address.Phone),
        };

        return new AddressValidationResult(tidied, problems);
    }

    private static bool IsEmpty(Address address, AddressComponent component)
        => component switch
        {
            AddressComponent.Name => Clean(address.Name) is null,
            AddressComponent.Company => Clean(address.Company) is null,
            AddressComponent.StreetAddress => Clean(address.AddressLine1) is null && Clean(address.AddressLine2) is null,
            AddressComponent.DependentLocality => Clean(address.DependentLocality) is null,
            AddressComponent.City => Clean(address.City) is null,
            AddressComponent.Region => Clean(address.Region) is null,
            AddressComponent.PostalCode => Clean(address.PostalCode) is null,
            AddressComponent.SortingCode => Clean(address.SortingCode) is null,
            AddressComponent.Country => Clean(address.Country) is null,
            _ => false,
        };

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
