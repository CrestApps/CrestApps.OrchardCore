using CrestApps.OrchardCore.Addresses;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Addresses;

public sealed class CountryProviderTests
{
    [Fact]
    public void GetCountries_ReturnsEntries()
    {
        // Act
        var countries = CountryProvider.GetCountries();

        // Assert
        Assert.NotEmpty(countries);
    }

    [Fact]
    public void GetCountries_ContainsWellKnownCountries()
    {
        // Act
        var countries = CountryProvider.GetCountries();

        // Assert
        Assert.Contains(countries, country => country.Code == "US");
        Assert.Contains(countries, country => country.Code == "CA");
    }

    [Fact]
    public void GetCountries_HasNoDuplicateCodes()
    {
        // Act
        var countries = CountryProvider.GetCountries();

        // Assert
        var distinctCodes = countries
            .Select(country => country.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.Equal(countries.Count, distinctCodes);
    }

    [Fact]
    public void GetCountries_IsOrderedByName()
    {
        // Act
        var countries = CountryProvider.GetCountries();

        // Assert
        var ordered = countries
            .OrderBy(country => country.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(country => country.Code);

        Assert.Equal(ordered, countries.Select(country => country.Code));
    }
}
