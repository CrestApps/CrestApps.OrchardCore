using System.Linq;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Addresses.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Addresses;

public sealed class DefaultCountryServiceTests
{
    [Fact]
    public async Task GetCountriesAsync_ReturnsCanonicalCountryProviderList()
    {
        // Arrange
        var service = new DefaultCountryService();

        // Act
        var countries = await service.GetCountriesAsync();

        // Assert
        Assert.NotEmpty(countries);
        Assert.Equal(CountryProvider.GetCountries().Count, countries.Count);
    }

    [Fact]
    public async Task GetCountriesAsync_ContainsWellKnownCountries()
    {
        // Arrange
        var service = new DefaultCountryService();

        // Act
        var countries = await service.GetCountriesAsync();

        // Assert
        Assert.Contains(countries, country => country.Code == "US");
        Assert.Contains(countries, country => country.Code == "CA");
    }

    [Fact]
    public async Task GetCountriesAsync_ReturnsCountriesOrderedByName()
    {
        // Arrange
        var service = new DefaultCountryService();

        // Act
        var countries = await service.GetCountriesAsync();

        // Assert
        var ordered = countries
            .OrderBy(country => country.Name, System.StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        Assert.Equal(ordered.Select(country => country.Code), countries.Select(country => country.Code));
    }
}
