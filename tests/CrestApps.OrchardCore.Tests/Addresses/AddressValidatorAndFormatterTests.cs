using CrestApps.OrchardCore.Addresses.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;

namespace CrestApps.OrchardCore.Tests.Addresses;

/// <summary>
/// Addresses are checked against their country's rules and printed the way its post expects.
/// </summary>
public sealed class AddressValidatorAndFormatterTests
{
    private static readonly DefaultAddressFormatProvider _formats = new();
    private static readonly DefaultCountryService _countries = new();
    private static readonly DefaultAddressValidator _validator = new(_formats, _countries);
    private static readonly DefaultAddressFormatter _formatter = new(_formats, _countries);

    private static Address California(string region = "California", string postalCode = "94043") => new()
    {
        Name = "Pat Lee",
        Company = "Contoso",
        AddressLine1 = "1600 Amphitheatre Pkwy",
        City = "Mountain View",
        Region = region,
        PostalCode = postalCode,
        Country = "us",
    };

    private static AddressProblem[] Problems(AddressValidationResult result)
        => [.. result.Problems.OrderBy(problem => problem.Component).ThenBy(problem => problem.Kind)];

    [Fact]
    public async Task AValidAddress_IsTidied_WithTheRegionStoredByItsCode()
    {
        var result = await _validator.ValidateAsync(California());

        Assert.True(result.IsValid);
        Assert.Equal("US", result.Address.Country);
        Assert.Equal("CA", result.Address.Region);
        Assert.Equal("Pat Lee", result.Address.Name);
    }

    [Fact]
    public async Task WhatTheCountryRequires_IsMissing()
    {
        var result = await _validator.ValidateAsync(new Address { Country = "US", AddressLine1 = "1 Main St" });

        Assert.Equal(
            [
                new AddressProblem(AddressComponent.City, AddressProblemKind.Missing),
                new AddressProblem(AddressComponent.Region, AddressProblemKind.Missing),
                new AddressProblem(AddressComponent.PostalCode, AddressProblemKind.Missing),
            ],
            Problems(result));
    }

    [Fact]
    public async Task TheCountry_IsRequired_AndMustBeKnown()
    {
        Assert.Contains(new AddressProblem(AddressComponent.Country, AddressProblemKind.Missing), (await _validator.ValidateAsync(new Address { AddressLine1 = "1 Main St", City = "Springfield" })).Problems);
        Assert.Contains(new AddressProblem(AddressComponent.Country, AddressProblemKind.Unknown), (await _validator.ValidateAsync(new Address { AddressLine1 = "1 Main St", City = "Springfield", Country = "QQ" })).Problems);
    }

    [Fact]
    public async Task APostalCode_MustHaveTheCountrysForm_AndBelongToTheRegion()
    {
        Assert.Equal([new AddressProblem(AddressComponent.PostalCode, AddressProblemKind.Invalid)], Problems(await _validator.ValidateAsync(California(postalCode: "ABCDE"))));

        // 10001 is in New York.
        Assert.Equal([new AddressProblem(AddressComponent.PostalCode, AddressProblemKind.NotInRegion)], Problems(await _validator.ValidateAsync(California(postalCode: "10001"))));
    }

    [Fact]
    public async Task ARegion_MustBeOneOfTheCountrys()
    {
        Assert.Equal([new AddressProblem(AddressComponent.Region, AddressProblemKind.Unknown)], Problems(await _validator.ValidateAsync(California(region: "Atlantis"))));
    }

    [Theory]
    [InlineData("GB", "sw1a 1aa", "SW1A 1AA")]
    [InlineData("CA", "k1a 0b1", "K1A 0B1")]
    [InlineData("DE", "10115", "10115")]
    [InlineData("KR", "03051", "03051")]
    [InlineData("BR", "01310-100", "01310-100")]
    public async Task PostalCodes_AreCheckedAndCapitalized(string country, string postalCode, string expected)
    {
        var format = await _formats.GetFormatAsync(country);
        var result = await _validator.ValidateAsync(new Address
        {
            AddressLine1 = "1 High Street",
            City = "Town",
            PostalCode = postalCode,
            Country = country,
            Region = format.Regions.FirstOrDefault(region => region.PostalCodePrefix is null || AddressPatterns.StartsWith(region.PostalCodePrefix, expected))?.Code,
        });

        Assert.True(result.IsValid, string.Join(", ", result.Problems));
        Assert.Equal(expected, result.Address.PostalCode);
    }

    [Fact]
    public async Task AnAddressWithoutAPostalSystem_NeedsNoPostalCode()
    {
        var result = await _validator.ValidateAsync(new Address { AddressLine1 = "Sheikh Zayed Road", City = "Dubai", Region = "Dubai", Country = "AE" });

        Assert.True(result.IsValid, string.Join(", ", result.Problems));
    }

    [Fact]
    public async Task TheUnitedStates_PrintsCityStateAndZipOnOneLine()
    {
        var lines = await _formatter.GetLinesAsync(new Address
        {
            Name = "Pat Lee",
            Company = "Contoso",
            AddressLine1 = "1600 Amphitheatre Pkwy",
            AddressLine2 = "Building 4",
            City = "Mountain View",
            Region = "CA",
            PostalCode = "94043",
            Country = "US",
        });

        Assert.Equal(["Pat Lee", "Contoso", "1600 Amphitheatre Pkwy", "Building 4", "Mountain View, CA 94043", "United States"], lines);
    }

    [Fact]
    public async Task Germany_PrintsThePostalCodeBeforeTheCity()
    {
        var lines = await _formatter.GetLinesAsync(new Address { Name = "Pat Lee", AddressLine1 = "Unter den Linden 1", City = "Berlin", PostalCode = "10117", Country = "DE" }, new AddressFormatOptions { IncludeCountry = false });

        Assert.Equal(["Pat Lee", "Unter den Linden 1", "10117 Berlin"], lines);
    }

    [Fact]
    public async Task TheUnitedKingdom_OnAnEnvelope_CapitalizesThePostTownAndPostcode()
    {
        var lines = await _formatter.GetLinesAsync(new Address { Name = "Pat Lee", AddressLine1 = "10 Downing Street", City = "London", PostalCode = "sw1a 2aa", Country = "GB" }, new AddressFormatOptions { Envelope = true });

        Assert.Equal(["Pat Lee", "10 Downing Street", "LONDON", "SW1A 2AA", "UNITED KINGDOM"], lines);
    }

    [Fact]
    public async Task Japan_InItsOwnScript_StartsWithThePostalCode()
    {
        // Japan writes the city and ward as part of the street address.
        var lines = await _formatter.GetLinesAsync(new Address { Name = "山田太郎", AddressLine1 = "千代田区千代田1-1", Region = "13", PostalCode = "100-0001", Country = "JP" }, new AddressFormatOptions { IncludeCountry = false });

        Assert.Equal(["〒100-0001", "東京都", "千代田区千代田1-1", "山田太郎"], lines);
    }

    [Fact]
    public async Task Japan_InLatinLetters_UsesTheLatinOrder()
    {
        var lines = await _formatter.GetLinesAsync(new Address { Name = "Taro Yamada", AddressLine1 = "1-1 Chiyoda, Chiyoda-ku", Region = "13", PostalCode = "100-0001", Country = "JP" }, new AddressFormatOptions { IncludeCountry = false });

        Assert.Equal(["Taro Yamada", "1-1 Chiyoda, Chiyoda-ku, Tokyo", "100-0001"], lines);
    }

    [Fact]
    public async Task TextBetweenParts_IsLeftOut_WhenAPartIsMissing()
    {
        // "%C, %S %Z" without a state still keeps the city and the ZIP code apart.
        var unitedStates = await _formatter.GetLinesAsync(new Address { City = "Mountain View", PostalCode = "94043", Country = "US" }, new AddressFormatOptions { IncludeCountry = false });

        // Japan's Latin "%A, %S" leaves no trailing comma without a prefecture.
        var japan = await _formatter.GetLinesAsync(new Address { AddressLine1 = "1-1 Chiyoda, Chiyoda-ku", Country = "JP" }, new AddressFormatOptions { IncludeCountry = false });

        Assert.Equal(["Mountain View 94043"], unitedStates);
        Assert.Equal(["1-1 Chiyoda, Chiyoda-ku"], japan);
    }

    [Fact]
    public async Task TextBeforeAPart_BelongsToIt()
    {
        // Monaco writes "MC-%Z %C": without a postal code there is no "MC-".
        var withPostalCode = await _formatter.GetLinesAsync(new Address { AddressLine1 = "1 Avenue de la Costa", City = "Monaco", PostalCode = "98000", Country = "MC" }, new AddressFormatOptions { IncludeCountry = false });
        var withoutPostalCode = await _formatter.GetLinesAsync(new Address { AddressLine1 = "1 Avenue de la Costa", City = "Monaco", Country = "MC" }, new AddressFormatOptions { IncludeCountry = false });

        Assert.Equal(["1 Avenue de la Costa", "MC-98000 Monaco"], withPostalCode);
        Assert.Equal(["1 Avenue de la Costa", "Monaco"], withoutPostalCode);
    }

    [Fact]
    public async Task Reunion_OnAnEnvelope_WritesTheCedexAfterTheCity()
    {
        var lines = await _formatter.GetLinesAsync(new Address { Company = "Contoso", AddressLine1 = "1 rue de Paris", City = "Saint-Denis", PostalCode = "97400", SortingCode = "Cedex 9", Country = "RE" }, new AddressFormatOptions { IncludeCountry = false, Envelope = true });

        Assert.Equal(["Contoso", "1 RUE DE PARIS", "97400 SAINT-DENIS CEDEX 9"], lines);
    }

    [Fact]
    public async Task France_PrintsThePostalCodeBeforeTheCity()
    {
        var lines = await _formatter.GetLinesAsync(new Address { Company = "Contoso", AddressLine1 = "1 rue de Rivoli", City = "Paris", PostalCode = "75001", Country = "FR" }, new AddressFormatOptions { IncludeCountry = false, Envelope = true });

        Assert.Equal(["Contoso", "1 rue de Rivoli", "75001 PARIS"], lines);
    }
}
