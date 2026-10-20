using System.Net;
using CrestApps.OrchardCore.Addresses.Endpoints;
using CrestApps.OrchardCore.Addresses.GooglePlaces;
using Microsoft.Extensions.Caching.Memory;

namespace CrestApps.OrchardCore.Tests.Addresses;

/// <summary>
/// A picked address fills the editor in the way its country writes it, and a visitor can't run up the store's bill by
/// asking for suggestions without end.
/// </summary>
public sealed class AddressSuggestionTests
{
    [Fact]
    public void AGermanAddress_PutsTheNumberAfterTheStreet()
    {
        var address = GooglePlacesAddressAutocompleteProvider.ToAddress(
        [
            Component("5", "5", "street_number"),
            Component("Hauptstraße", "Hauptstraße", "route"),
            Component("Berlin", "Berlin", "locality", "political"),
            Component("Berlin", "BE", "administrative_area_level_1", "political"),
            Component("10115", "10115", "postal_code"),
            Component("Deutschland", "DE", "country", "political"),
        ]);

        Assert.Equal("Hauptstraße 5", address.AddressLine1);
        Assert.Equal("Berlin", address.City);
        Assert.Equal("BE", address.Region);
        Assert.Equal("10115", address.PostalCode);
        Assert.Equal("DE", address.Country);
    }

    [Fact]
    public void AnAmericanAddress_PutsTheNumberFirst_AndKeepsTheUnitOnTheSecondLine()
    {
        var address = GooglePlacesAddressAutocompleteProvider.ToAddress(
        [
            Component("100", "100", "street_number"),
            Component("Main Street", "Main St", "route"),
            Component("Suite 4", "Suite 4", "subpremise"),
            Component("Las Vegas", "Las Vegas", "locality"),
            Component("Nevada", "NV", "administrative_area_level_1"),
            Component("89101", "89101", "postal_code"),
            Component("United States", "us", "country"),
        ]);

        Assert.Equal("100 Main Street", address.AddressLine1);
        Assert.Equal("Suite 4", address.AddressLine2);
        Assert.Equal("NV", address.Region);
        Assert.Equal("US", address.Country);
    }

    [Fact]
    public void ABritishTown_IsTakenFromThePostalTown()
    {
        var address = GooglePlacesAddressAutocompleteProvider.ToAddress(
        [
            Component("10", "10", "street_number"),
            Component("Downing Street", "Downing St", "route"),
            Component("London", "London", "postal_town"),
            Component("SW1A 2AA", "SW1A 2AA", "postal_code"),
            Component("United Kingdom", "GB", "country"),
        ]);

        Assert.Equal("10 Downing Street", address.AddressLine1);
        Assert.Equal("London", address.City);
    }

    [Fact]
    public void AVisitor_IsHeldToSoManyRequestsAMinute()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var visitor = IPAddress.Parse("203.0.113.7");
        var other = IPAddress.Parse("203.0.113.8");

        for (var i = 0; i < AddressAutocompleteEndpoints.RequestsPerMinute; i++)
        {
            Assert.True(AddressAutocompleteEndpoints.Allow(cache, visitor));
        }

        Assert.False(AddressAutocompleteEndpoints.Allow(cache, visitor));
        Assert.True(AddressAutocompleteEndpoints.Allow(cache, other));
    }

    private static GooglePlacesAddressAutocompleteProvider.AddressComponent Component(string longText, string shortText, params string[] types)
        => new() { LongText = longText, ShortText = shortText, Types = [.. types] };
}
