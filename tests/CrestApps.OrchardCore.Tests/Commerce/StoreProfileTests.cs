using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Commerce.Core.Stores;
using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Receipts.Core;
using CrestApps.OrchardCore.Receipts.Core.Services;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// The store profile is who the store is: receipts, the address parcels ship from and the tax origin fall back to it
/// when their own settings are empty, and never override settings a store already saved.
/// </summary>
public sealed class StoreProfileTests
{
    private static readonly Address _store = new()
    {
        AddressLine1 = "500 Pine St",
        City = "Seattle",
        Region = "WA",
        PostalCode = "98101",
        Country = "US",
    };

    [Fact]
    public async Task AReceipt_TakesTheStoreProfile_WhereItsOwnSettingsAreEmpty()
    {
        var site = Site(new ReceiptSettings(), Profile());
        var siteService = SiteService(site);
        var receipts = new DefaultReceiptService(siteService, new DefaultStoreLocationService(siteService, [], []));

        var receipt = await receipts.BuildAsync(new ReceiptRequest());

        Assert.Equal("Contoso Goods", receipt.BusinessName);
        Assert.Equal("/media/logo.png", receipt.LogoUrl);
        Assert.Equal("hello@contoso.test", receipt.ContactEmail);
        Assert.Equal("+12065550100", receipt.ContactPhone);
        Assert.Equal("500 Pine St, 98101 Seattle WA, US", receipt.BusinessAddress);
    }

    [Fact]
    public async Task AReceiptsOwnSettings_StillWin()
    {
        var site = Site(new ReceiptSettings { BusinessName = "Receipts name", BusinessAddress = "1 Receipt Rd", ContactEmail = "receipts@contoso.test" }, Profile());
        var siteService = SiteService(site);
        var receipts = new DefaultReceiptService(siteService, new DefaultStoreLocationService(siteService, [], []));

        var receipt = await receipts.BuildAsync(new ReceiptRequest());

        Assert.Equal("Receipts name", receipt.BusinessName);
        Assert.Equal("1 Receipt Rd", receipt.BusinessAddress);
        Assert.Equal("receipts@contoso.test", receipt.ContactEmail);
        Assert.Equal("+12065550100", receipt.ContactPhone);
    }

    [Fact]
    public async Task WithoutAProfile_AReceiptIsAsBefore()
    {
        var site = Site(new ReceiptSettings(), profile: null, siteName: "My site");
        var siteService = SiteService(site);

        var receipt = await new DefaultReceiptService(siteService, new DefaultStoreLocationService(siteService, [], [])).BuildAsync(new ReceiptRequest());

        Assert.Equal("My site", receipt.BusinessName);
        Assert.Null(receipt.BusinessAddress);
        Assert.Null(receipt.LogoUrl);
    }

    [Fact]
    public async Task TheTaxOrigin_IsTheStoresAddress_UntilTheTaxSettingsNameOne()
    {
        var withProfile = new TaxOriginCheckoutTaxProfileHandler(SiteService(Site(new TaxOriginSettings(), Profile())));
        var withOrigin = new TaxOriginCheckoutTaxProfileHandler(SiteService(Site(new TaxOriginSettings { Origin = new Address { Country = "CA", Region = "BC" } }, Profile())));

        var fromProfile = new CheckoutTaxProfile();
        var fromOrigin = new CheckoutTaxProfile();

        await withProfile.BuildingAsync(fromProfile, new CheckoutSession(), TestContext.Current.CancellationToken);
        await withOrigin.BuildingAsync(fromOrigin, new CheckoutSession(), TestContext.Current.CancellationToken);

        Assert.Equal("Seattle", fromProfile.Origin.City);
        Assert.Equal("BC", fromOrigin.Origin.Region);
    }

    [Fact]
    public async Task CarrierRates_ShipFromTheStoresAddress_WhenShippingNamesNone()
    {
        CarrierShipmentRequest quoted = null;
        var carrier = new Mock<ICarrierProvider>();
        carrier.SetupGet(value => value.Name).Returns("Test");
        carrier.Setup(value => value.IsEnabledAsync()).ReturnsAsync(true);
        carrier.Setup(value => value.QuoteAsync(It.IsAny<CarrierShipmentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CarrierShipmentRequest, CancellationToken>((request, _) => quoted = request)
            .ReturnsAsync(new CarrierQuote { QuoteId = "q", Rates = [new() { RateId = "1", Carrier = "USPS", Service = "Ground", Amount = 5, Currency = "USD" }] });

        var resolver = new Mock<ICarrierProviderResolver>();
        resolver.Setup(value => value.GetDefaultAsync()).ReturnsAsync(carrier.Object);

        var provider = new CarrierShippingRateProvider(resolver.Object, SiteService(Site(new ShippingSettings(), Profile())), new MemoryCache(new MemoryCacheOptions()), NullLogger<CarrierShippingRateProvider>.Instance, new PassThroughStringLocalizer<CarrierShippingRateProvider>());
        var method = new ShippingMethod { MethodId = "carrier", Name = "Carrier", ProviderName = ShippingConstants.Providers.Carrier };
        method.Put(new CarrierRateSettings { FallbackAmount = 9 });
        var zone = new ShippingZone { ItemId = "zone", Name = "US", Countries = ["US"], Methods = [method] };

        var rates = await provider.GetRatesAsync(new ShippingRateContext(zone, method, new ShippingRateRequest { Destination = new Address { Country = "US", PostalCode = "10001" }, Currency = "USD", Lines = [new() { Quantity = 1, UnitPrice = 10, Weight = 1 }] }), TestContext.Current.CancellationToken);

        Assert.NotEmpty(rates);
        Assert.Equal("Seattle", quoted.From.City);
    }

    [Fact]
    public async Task TheStoresPlaces_StartWithItsOwnAddress_ThenEachSourcesPlaces()
    {
        var siteService = SiteService(Site(new ReceiptSettings(), Profile()));
        var source = new Mock<IStoreLocationSource>();
        source.Setup(value => value.GetLocationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new NamedAddress { Id = "inventory:w1", Name = "Warehouse", Address = new Address { City = "Tacoma", Country = "US" } }]);

        var service = new DefaultStoreLocationService(siteService, [source.Object], []);

        var locations = await service.ListLocationsAsync(TestContext.Current.CancellationToken);
        var profile = await service.GetProfileAsync();

        Assert.Equal([StoreLocationIds.Store, "inventory:w1"], locations.Select(location => location.Id));
        Assert.Equal("Contoso Goods", locations[0].Name);
        Assert.Equal("Seattle", (await service.GetAddressAsync()).City);
        Assert.Equal("+12065550100", profile.ToContactInfo().Phone.Value);
    }

    [Fact]
    public async Task AStoreWithoutAName_IsNamedAfterTheSite()
    {
        var profile = await new DefaultStoreLocationService(SiteService(Site(new ReceiptSettings(), new StoreProfileSettings(), "My site")), [], []).GetProfileAsync();

        Assert.Equal("My site", profile.Name);
    }

    private static StoreProfileSettings Profile()
        => new()
        {
            Name = "Contoso Goods",
            LogoUrl = "/media/logo.png",
            TaxId = "EIN-1",
            Email = "hello@contoso.test",
            Phone = "+12065550100",
            Address = _store,
        };

    private static SiteSettings Site<T>(T settings, StoreProfileSettings profile, string siteName = "Site")
        where T : class, new()
    {
        var site = new SiteSettings { SiteName = siteName };
        site.Put(settings);

        if (profile is not null)
        {
            site.Put(profile);
        }

        return site;
    }

    private static ISiteService SiteService(ISite site)
    {
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site);

        return siteService.Object;
    }
}
