using System.IO;
using System.Reflection;
using System.Text.Json;
using CrestApps.OrchardCore.Addresses.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;

namespace CrestApps.OrchardCore.Tests.Addresses;

/// <summary>
/// Each country's address format, read from the Google address data the package ships.
/// </summary>
public sealed class AddressFormatTests
{
    private static readonly DefaultAddressFormatProvider _provider = new();

    [Fact]
    public void TheShippedData_CoversTheWorld()
    {
        var catalog = DefaultAddressFormatProvider.Catalog;

        Assert.True(catalog.CountryCodes.Count >= 240, $"Only {catalog.CountryCodes.Count} countries were read.");
        Assert.Equal("CC-BY-4.0", catalog.Source.License);
        Assert.False(string.IsNullOrEmpty(catalog.Source.Retrieved));

        // The runtime's own list depends on the operating system (ICU on Linux adds the Canary Islands, Ceuta and
        // Melilla, and Diego Garcia, which the data files under Spain and the British Indian Ocean Territory), so
        // the check is on the countries every store ships to. Any other code falls back to the generic format.
        string[] major = ["US", "CA", "MX", "BR", "AR", "GB", "IE", "FR", "DE", "ES", "IT", "NL", "BE", "CH", "AT", "SE", "NO", "DK", "FI", "PL", "PT", "GR", "TR", "RU", "UA", "IL", "AE", "SA", "EG", "ZA", "NG", "KE", "IN", "PK", "CN", "HK", "TW", "JP", "KR", "SG", "MY", "TH", "VN", "ID", "PH", "AU", "NZ"];

        Assert.All(major, code => Assert.Contains(code, catalog.CountryCodes));
    }

    [Fact]
    public void TheNotice_NamesTheSnapshotItCredits()
    {
        var notice = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Core", "CrestApps.OrchardCore.Addresses.Core", "THIRD-PARTY-NOTICES.md"));

        Assert.Contains("CC BY 4.0", notice, StringComparison.Ordinal);
        Assert.Contains("retrieved " + DefaultAddressFormatProvider.Catalog.Source.Retrieved, notice, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryLabelInTheData_IsOneTheEditorKnows()
    {
        var known = typeof(AddressLabels)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetValue(null))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var code in DefaultAddressFormatProvider.Catalog.CountryCodes)
        {
            var format = DefaultAddressFormatProvider.Catalog.Get(code);

            Assert.Contains(format.RegionLabel, known);
            Assert.Contains(format.CityLabel, known);
            Assert.Contains(format.DependentLocalityLabel, known);
            Assert.Contains(format.PostalCodeLabel, known);
        }
    }

    [Fact]
    public void EveryPostalCodePattern_IsAValidPattern()
    {
        foreach (var code in DefaultAddressFormatProvider.Catalog.CountryCodes)
        {
            var format = DefaultAddressFormatProvider.Catalog.Get(code);

            if (format.PostalCodePattern is null)
            {
                continue;
            }

            foreach (var example in format.PostalCodeExamples)
            {
                Assert.True(AddressPatterns.IsMatch(format.PostalCodePattern, example), $"{code}: '{example}' does not match '{format.PostalCodePattern}'.");
            }
        }
    }

    [Fact]
    public async Task TheUnitedStates_WritesCityStateAndZipOnOneLine()
    {
        var format = await _provider.GetFormatAsync("us");

        Assert.Equal("US", format.CountryCode);
        Assert.Equal(
            [[AddressComponent.Name], [AddressComponent.Company], [AddressComponent.StreetAddress], [AddressComponent.City, AddressComponent.Region, AddressComponent.PostalCode]],
            format.Layout.Select(line => line.Components.ToArray()));
        Assert.Equal([AddressComponent.StreetAddress, AddressComponent.City, AddressComponent.Region, AddressComponent.PostalCode], format.Required.Order());
        Assert.Equal(AddressLabels.State, format.RegionLabel);
        Assert.Equal(AddressLabels.Zip, format.PostalCodeLabel);
        Assert.Equal("California", format.FindRegion("ca").Name);
        Assert.Equal("CA", format.FindRegion("California").Code);
    }

    [Fact]
    public async Task Japan_WritesItsOwnScriptLargestPlaceFirst_AndHasALatinOrder()
    {
        var format = await _provider.GetFormatAsync("JP");

        Assert.Equal(AddressLabels.Prefecture, format.RegionLabel);
        Assert.Equal([AddressComponent.PostalCode], format.Layout[0].Components);
        Assert.Equal("〒", format.Layout[0].Tokens[0].Text);
        Assert.NotNull(format.LatinLayout);
        Assert.Equal([AddressComponent.Name], format.LatinLayout[0].Components);

        // Tokyo is stored by its ISO 3166-2 code, and found by its name in either script.
        Assert.Equal("13", format.FindRegion("東京都").Code);
        Assert.Equal("13", format.FindRegion("tokyo").Code);
    }

    [Fact]
    public async Task TheUnitedKingdom_PutsThePostTownAndPostcodeOnTheirOwnLines()
    {
        var format = await _provider.GetFormatAsync("GB");

        Assert.Equal(AddressLabels.PostTown, format.CityLabel);
        Assert.Contains(format.Layout, line => line.Components.SequenceEqual([AddressComponent.City]));
        Assert.Contains(format.Layout, line => line.Components.SequenceEqual([AddressComponent.PostalCode]));
        Assert.Empty(format.Regions);
    }

    [Fact]
    public async Task ACountryTheDataDoesNotKnow_GetsEveryCommonPart()
    {
        var format = await _provider.GetFormatAsync("QQ");

        Assert.Null(format.CountryCode);
        Assert.True(format.Uses(AddressComponent.Region));
        Assert.True(format.Uses(AddressComponent.PostalCode));
        Assert.Equal([AddressComponent.StreetAddress, AddressComponent.City], format.Required.Order());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
