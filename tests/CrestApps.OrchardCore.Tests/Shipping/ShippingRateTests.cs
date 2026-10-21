using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Drivers;
using CrestApps.OrchardCore.Shipping.Handlers;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Shipping;

/// <summary>
/// An address falls in the first zone that covers it, and each of the zone's methods prices the order.
/// </summary>
public sealed class ShippingRateTests
{
    private static readonly Address _seattle = new() { Country = "US", Region = "WA" };
    private static readonly Address _anchorage = new() { Country = "US", Region = "AK" };
    private static readonly Address _toronto = new() { Country = "CA", Region = "ON" };
    private static readonly Address _paris = new() { Country = "FR" };

    [Fact]
    public async Task UsAndCanadianAddresses_GetTheRatesTheirZonesSet()
    {
        var service = CreateService(
            Zone("United States", ["US"], Flat("Standard", 5, perItem: 1), Table("Express", TableRateBasis.Weight, (0, 15), (2, 25))),
            Zone("Canada", ["CA"], Flat("Standard", 12)));

        var us = await RatesAsync(service, Request(_seattle, (2, 10m, 0.5m)));
        var canada = await RatesAsync(service, Request(_toronto, (2, 10m, 0.5m)));

        // Standard is 5 plus 1 for each of the 2 items; Express looks up 1 kg in its table.
        Assert.Equal([("Standard", 7m), ("Express", 15m)], us.Select(rate => (rate.Name, rate.Amount)));
        Assert.Equal([("Standard", 12m)], canada.Select(rate => (rate.Name, rate.Amount)));
        Assert.All(us.Concat(canada), rate => Assert.Equal("USD", rate.Currency));
    }

    [Fact]
    public void AZoneNarrowedToRegions_CoversOnlyThem_AndZonesAreTriedInOrder()
    {
        var remote = Zone("Alaska and Hawaii", ["US"]);
        remote.Regions = ["US-AK", "US-HI"];
        remote.Position = 1;

        var mainland = Zone("United States", ["US"]);
        mainland.Position = 2;

        var world = Zone("Everywhere else", []);
        world.RestOfWorld = true;

        // The rest-of-world zone comes last whatever its position.
        world.Position = 0;

        ShippingZone[] zones = [world, mainland, remote];

        Assert.Same(remote, DefaultShippingRateService.FindZone(zones, _anchorage));
        Assert.Same(mainland, DefaultShippingRateService.FindZone(zones, _seattle));
        Assert.Same(world, DefaultShippingRateService.FindZone(zones, _paris));
        Assert.Same(mainland, DefaultShippingRateService.FindZone(zones, new Address { Country = "us" }));
        Assert.Null(DefaultShippingRateService.FindZone([mainland], _paris));
        Assert.Same(world, DefaultShippingRateService.FindZone(zones, null));
    }

    [Theory]
    [InlineData(0.5, 5)]
    [InlineData(2, 8)]
    [InlineData(4.99, 8)]
    [InlineData(5, 12)]
    public void ATable_UsesTheRowTheOrderFallsIn(decimal weight, decimal amount)
    {
        var settings = new TableRateSettings { Rows = [new() { From = 0, Amount = 5 }, new() { From = 2, Amount = 8 }, new() { From = 5, Amount = 12 }] };

        Assert.Equal(amount, TableRateShippingProvider.Lookup(settings, Request(_seattle, (1, 10m, weight))));
    }

    [Fact]
    public void ATable_OffersNothing_BelowItsFirstRowOrAboveItsLimit_AndCanUseSubtotalOrItems()
    {
        var bySubtotal = new TableRateSettings { Basis = TableRateBasis.Subtotal, Rows = [new() { From = 50, Amount = 4 }], Maximum = 500 };
        var byItems = new TableRateSettings { Basis = TableRateBasis.Quantity, Rows = [new() { From = 1, Amount = 3 }, new() { From = 3, Amount = 6 }] };

        Assert.Null(TableRateShippingProvider.Lookup(bySubtotal, Request(_seattle, (1, 20m, 0m))));
        Assert.Equal(4, TableRateShippingProvider.Lookup(bySubtotal, Request(_seattle, (2, 30m, 0m))));
        Assert.Null(TableRateShippingProvider.Lookup(bySubtotal, Request(_seattle, (2, 300m, 0m))));
        Assert.Equal(6, TableRateShippingProvider.Lookup(byItems, Request(_seattle, (4, 1m, 0m))));
    }

    [Fact]
    public async Task FreeShipping_IsOfferedFromItsMinimum()
    {
        var service = CreateService(Zone("United States", ["US"], Free("Free over 100", 100), Flat("Standard", 5)));

        var small = await RatesAsync(service, Request(_seattle, (1, 99.99m, 1m)));
        var large = await RatesAsync(service, Request(_seattle, (1, 100m, 1m)));

        Assert.Equal(["Standard"], small.Select(rate => rate.Name));
        Assert.Equal([("Free over 100", 0m), ("Standard", 5m)], large.Select(rate => (rate.Name, rate.Amount)));
    }

    [Fact]
    public async Task LocalDelivery_IsOfferedOnlyToItsPostalCodes_AndIsFreeFromItsSubtotal()
    {
        var local = Method("Local delivery", ShippingConstants.Providers.LocalDelivery, new LocalDeliverySettings
        {
            PostalCodes = ["981*", "V6B 1A1"],
            Amount = 8,
            FreeFromSubtotal = 75,
            MinimumSubtotal = 20,
        });
        var service = CreateService(Zone("North America", ["US", "CA"], local, Flat("Standard", 5)));

        ShippingRateRequest At(string country, string postalCode, decimal subtotal)
            => new()
            {
                Destination = new Address { Country = country, PostalCode = postalCode },
                Currency = "USD",
                Lines = [new() { Quantity = 1, UnitPrice = subtotal }],
            };

        Assert.Equal([("Local delivery", 8m), ("Standard", 5m)], (await RatesAsync(service, At("US", "98101", 30))).Select(rate => (rate.Name, rate.Amount)));
        Assert.Equal(0m, (await RatesAsync(service, At("US", "98109-1234", 80)))[0].Amount);
        Assert.Equal("Local delivery", (await RatesAsync(service, At("CA", "v6b1a1", 30)))[0].Name);
        Assert.Equal(["Standard"], (await RatesAsync(service, At("US", "97201", 30))).Select(rate => rate.Name));
        Assert.Equal(["Standard"], (await RatesAsync(service, At("US", "98101", 10))).Select(rate => rate.Name));
        Assert.Equal(["Standard"], (await RatesAsync(service, At("US", null, 30))).Select(rate => rate.Name));
    }

    [Fact]
    public void LocalDeliveryPostalCodes_AreReadOnePerLineOrCommaSeparated_WithoutABareWildcard()
        => Assert.Equal(["981*", "98004", "V6B 1A1"], LocalDeliveryMethodDisplayDriver.ParsePostalCodes("981*, 98004\n*\r\nV6B 1A1\n981*"));

    [Fact]
    public async Task ClassSurcharges_AddPerUnitOfTheClass()
    {
        var standard = Flat("Standard", 5);
        standard.ClassSurcharges["Bulky"] = 15;

        var service = CreateService(Zone("United States", ["US"], standard));
        var request = new ShippingRateRequest
        {
            Destination = _seattle,
            Currency = "USD",
            Lines =
            [
                new() { Quantity = 2, UnitPrice = 300, ShippingClass = "bulky" },
                new() { Quantity = 3, UnitPrice = 10 },
            ],
        };

        Assert.Equal(35m, Assert.Single(await RatesAsync(service, request)).Amount);
    }

    [Fact]
    public async Task MethodsThatAreOff_InAnotherCurrency_OrWithoutTheirProvider_AreLeftOut()
    {
        var off = Flat("Off", 1);
        off.Enabled = false;

        var euros = Flat("Euros", 2);
        euros.Currency = "EUR";

        var dollars = Flat("Dollars", 3);
        dollars.Currency = "usd";

        var carrier = new ShippingMethod { MethodId = "carrier", Name = "Carrier", ProviderName = "SomeCarrier" };

        var service = CreateService(Zone("United States", ["US"], off, euros, dollars, carrier));

        Assert.Equal(["Dollars"], (await RatesAsync(service, Request(_seattle, (1, 10m, 1m)))).Select(rate => rate.Name));
    }

    [Fact]
    public async Task Rates_AreRoundedToTheCurrency_AndAnAddressNoZoneCoversHasNone()
    {
        var service = CreateService(Zone("United States", ["US"], Flat("Standard", 4.995m)));

        Assert.Equal(5.00m, Assert.Single(await RatesAsync(service, Request(_seattle, (1, 10m, 1m)))).Amount);
        Assert.Empty(await RatesAsync(service, Request(_paris, (1, 10m, 1m))));
    }

    [Fact]
    public void AVariantWeighs_ItsOwnWeight_OrTheProducts()
    {
        var part = new ShippingPart { Weight = 1.2m, VariantWeights = { ["large"] = 2.5m } };

        Assert.Equal(2.5m, part.GetWeight("large"));
        Assert.Equal(1.2m, part.GetWeight("small"));
        Assert.Equal(1.2m, part.GetWeight(null));
        Assert.Equal(0m, new ShippingPart().GetWeight("small"));
    }

    [Fact]
    public void ARecipeZone_IsNormalized_AndItsMethodsGetIds()
    {
        var zone = new ShippingZone();

        ShippingZoneHandler.Populate(zone, JsonNode.Parse("""
            {
              "Name": " Domestic ",
              "Countries": ["us", "US", " ca "],
              "Regions": ["us-ak"],
              "Methods": [
                { "Name": "Standard", "ProviderName": "Flat", "Properties": { "FlatRateSettings": { "Amount": 5, "PerItemAmount": 0 } } }
              ]
            }
            """));

        Assert.Equal("Domestic", zone.Name);
        Assert.Equal(["US", "CA"], zone.Countries);
        Assert.Equal(["US-AK"], zone.Regions);

        var method = Assert.Single(zone.Methods);
        Assert.False(string.IsNullOrEmpty(method.MethodId));
        Assert.Equal(5, method.GetOrCreate<FlatRateSettings>().Amount);
    }

    [Fact]
    public void ShippingClasses_AreReadOnePerLine_WithoutDuplicates()
        => Assert.Equal(["Bulky", "Fragile"], ShippingSettingsDisplayDriver.ParseClasses("Bulky\r\n\r\n Fragile , bulky\nfragile"));

    private static Task<IReadOnlyList<ShippingRate>> RatesAsync(DefaultShippingRateService service, ShippingRateRequest request)
        => service.GetRatesAsync(request, TestContext.Current.CancellationToken);

    private static DefaultShippingRateService CreateService(params ShippingZone[] zones)
    {
        var catalog = new Mock<INamedCatalog<ShippingZone>>();
        catalog.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(zones);

        IShippingRateProvider[] providers =
        [
            new FlatRateShippingProvider(new PassThroughStringLocalizer<FlatRateShippingProvider>()),
            new TableRateShippingProvider(new PassThroughStringLocalizer<TableRateShippingProvider>()),
            new FreeShippingProvider(new PassThroughStringLocalizer<FreeShippingProvider>()),
            new LocalDeliveryShippingProvider(new PassThroughStringLocalizer<LocalDeliveryShippingProvider>()),
        ];

        return new DefaultShippingRateService(catalog.Object, providers, NullLogger<DefaultShippingRateService>.Instance);
    }

    private static ShippingZone Zone(string name, string[] countries, params ShippingMethod[] methods)
    {
        for (var i = 0; i < methods.Length; i++)
        {
            methods[i].Position = i;
        }

        return new ShippingZone { ItemId = name.Replace(" ", string.Empty), Name = name, Countries = [.. countries], Methods = [.. methods] };
    }

    private static ShippingMethod Flat(string name, decimal amount, decimal perItem = 0)
        => Method(name, ShippingConstants.Providers.Flat, new FlatRateSettings { Amount = amount, PerItemAmount = perItem });

    private static ShippingMethod Free(string name, decimal minimum)
        => Method(name, ShippingConstants.Providers.Free, new FreeShippingSettings { MinimumSubtotal = minimum });

    private static ShippingMethod Table(string name, TableRateBasis basis, params (decimal From, decimal Amount)[] rows)
        => Method(name, ShippingConstants.Providers.Table, new TableRateSettings { Basis = basis, Rows = [.. rows.Select(row => new TableRateRow { From = row.From, Amount = row.Amount })] });

    private static ShippingMethod Method<TSettings>(string name, string provider, TSettings settings)
        where TSettings : new()
    {
        var method = new ShippingMethod { MethodId = name.ToLowerInvariant().Replace(" ", "-"), Name = name, ProviderName = provider };
        method.Put(settings);

        return method;
    }

    // One line of items: how many, the price of each, and the weight of each.
    private static ShippingRateRequest Request(Address destination, (int Quantity, decimal UnitPrice, decimal Weight) line)
        => new()
        {
            Destination = destination,
            Currency = "USD",
            Lines = [new() { ContentItemId = "item", Quantity = line.Quantity, UnitPrice = line.UnitPrice, Weight = line.Weight }],
        };
}
