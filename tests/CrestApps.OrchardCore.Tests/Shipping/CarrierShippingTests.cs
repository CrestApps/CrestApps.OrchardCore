using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Shipping;

/// <summary>
/// Carrier rates and labels come through the store's shipping account: EasyPost's answers are read into rates and
/// labels, a shipping method offers the cheapest services with its markup, and signed tracking updates move parcels on.
/// </summary>
public sealed class CarrierShippingTests
{
    private static readonly Address _seattle = new() { Name = "Sam", AddressLine1 = "1 Pike Pl", City = "Seattle", Region = "WA", PostalCode = "98101", Country = "US" };

    [Fact]
    public async Task EasyPostsRates_AreReadAsTheCarriersServices_WithTheKeyAsBasicAuth()
    {
        HttpRequestMessage sent = null;
        var service = EasyPost(request =>
        {
            sent = request;

            return Json(HttpStatusCode.Created, """
                {"id":"shp_1","rates":[
                  {"id":"rate_a","carrier":"USPS","service":"GroundAdvantage","rate":"5.93","currency":"USD","delivery_days":3},
                  {"id":"rate_b","carrier":"UPS","service":"Ground","rate":"11.20","currency":"USD","delivery_days":null}]}
                """);
        });

        var quote = await service.QuoteAsync(new CarrierShipmentRequest { From = _seattle, To = _seattle, Parcel = new CarrierParcel { WeightOunces = 12 } }, TestContext.Current.CancellationToken);

        Assert.True(quote.Succeeded);
        Assert.Equal("shp_1", quote.QuoteId);
        Assert.Equal([("USPS", "GroundAdvantage", 5.93m, (int?)3), ("UPS", "Ground", 11.20m, null)], quote.Rates.Select(rate => (rate.Carrier, rate.Service, rate.Amount, rate.DeliveryDays)));
        Assert.Equal("https://api.easypost.test/v2/shipments", sent.RequestUri.ToString());
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("EZTK_test:")), sent.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task ABoughtLabel_HasTheCarriersTrackingNumberAndLabel_AndAnErrorIsTheCarriersMessage()
    {
        var bought = EasyPost(_ => Json(HttpStatusCode.OK, """
            {"id":"shp_1","tracking_code":"9400100000000000000000","postage_label":{"label_url":"https://easypost.test/label.png"},
             "selected_rate":{"carrier":"USPS","service":"GroundAdvantage","rate":"5.93","currency":"USD"},"tracker":{"public_url":"https://track.easypost.test/x"}}
            """));
        var refused = EasyPost(_ => Json(HttpStatusCode.UnprocessableEntity, """{"error":{"code":"ADDRESS.VERIFY.FAILURE","message":"Address not found"}}"""));

        var label = await bought.BuyLabelAsync("shp_1", "rate_a", TestContext.Current.CancellationToken);
        var failed = await refused.QuoteAsync(new CarrierShipmentRequest { From = _seattle, To = _seattle, Parcel = new CarrierParcel { WeightOunces = 1 } }, TestContext.Current.CancellationToken);

        Assert.Equal(("USPS", "9400100000000000000000", "https://easypost.test/label.png", 5.93m), (label.Carrier, label.TrackingNumber, label.LabelUrl, label.Amount));
        Assert.Equal("Address not found", failed.Error);
    }

    [Fact]
    public async Task ACarrierMethod_OffersTheCheapestServicesWithItsMarkup_AndFallsBackWhenTheCarriersFail()
    {
        var carrier = new Mock<ICarrierProvider>();
        carrier.SetupGet(value => value.Name).Returns("Test");
        carrier.Setup(value => value.IsEnabledAsync()).ReturnsAsync(true);
        carrier.SetupSequence(value => value.QuoteAsync(It.IsAny<CarrierShipmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CarrierQuote
            {
                QuoteId = "q",
                Rates =
                [
                    new() { RateId = "1", Carrier = "UPS", Service = "2ndDayAir", Amount = 20, Currency = "USD", DeliveryDays = 2 },
                    new() { RateId = "2", Carrier = "USPS", Service = "GroundAdvantage", Amount = 5, Currency = "USD" },
                    new() { RateId = "3", Carrier = "FedEx", Service = "Ground", Amount = 4, Currency = "USD" },
                ],
            })
            .ReturnsAsync(new CarrierQuote { Error = "Down" });

        var resolver = new Mock<ICarrierProviderResolver>();
        resolver.Setup(value => value.GetDefaultAsync()).ReturnsAsync(carrier.Object);

        var provider = new CarrierShippingRateProvider(resolver.Object, SiteService(new ShippingSettings { ShipFrom = _seattle }), new MemoryCache(new MemoryCacheOptions()), NullLogger<CarrierShippingRateProvider>.Instance, new PassThroughStringLocalizer<CarrierShippingRateProvider>());
        var method = new ShippingMethod { MethodId = "carrier", Name = "Carrier", ProviderName = ShippingConstants.Providers.Carrier };
        method.Put(new CarrierRateSettings { Carriers = ["UPS", "USPS"], MaxRates = 2, MarkupPercent = 10, MarkupAmount = 1, FallbackAmount = 9 });
        var zone = new ShippingZone { ItemId = "zone", Name = "US", Countries = ["US"], Methods = [method] };

        ShippingRateContext Context(string postalCode)
            => new(zone, method, new ShippingRateRequest { Destination = new Address { Country = "US", PostalCode = postalCode }, Currency = "USD", Lines = [new() { Quantity = 1, UnitPrice = 10, Weight = 1 }] });

        var rates = await provider.GetRatesAsync(Context("98101"), TestContext.Current.CancellationToken);
        var fallback = await provider.GetRatesAsync(Context("10001"), TestContext.Current.CancellationToken);

        // FedEx isn't offered; USPS 5 + 10% + 1 = 6.50, UPS 20 + 10% + 1 = 23.
        Assert.Equal([("USPS Ground Advantage", 6.50m), ("UPS 2nd Day Air", 23m)], rates.Select(rate => (rate.Name, rate.Amount)));
        Assert.Equal("2 business days", rates[1].Description);
        Assert.Equal([9m], fallback.Select(rate => rate.Amount));
    }

    [Fact]
    public async Task ASignedTrackingUpdate_RecordsWhereTheParcelIs_AndDeliveredDeliversIt()
    {
        var shipment = new Shipment { ItemId = "s1", TrackingNumber = "9400", Status = ShipmentStatus.Shipped };
        var store = new Mock<IShipmentStore>();
        store.Setup(value => value.FindByTrackingNumberAsync("9400", It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
        var shipments = new Mock<IShipmentService>();
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc));

        const string body = """
            {"description":"tracker.updated","result":{"tracking_code":"9400","status":"delivered","tracking_details":[
              {"datetime":"2026-10-18T09:00:00Z","message":"Out for delivery","status":"out_for_delivery","tracking_location":{"city":"Seattle","state":"WA"}},
              {"datetime":"2026-10-18T15:00:00Z","message":"Delivered","status":"delivered","tracking_location":{"city":"Seattle","state":"WA"}}]}}
            """;
        var signature = "hmac-sha256-hex=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes("whsec"), Encoding.UTF8.GetBytes(body)));

        Assert.True(EasyPostTrackingUpdater.IsSigned(body, signature, "whsec"));
        Assert.False(EasyPostTrackingUpdater.IsSigned(body, signature, "another"));
        Assert.False(EasyPostTrackingUpdater.IsSigned(body + " ", signature, "whsec"));

        Assert.True(await new EasyPostTrackingUpdater(store.Object, shipments.Object, clock.Object).ApplyAsync(JsonNode.Parse(body) as JsonObject, TestContext.Current.CancellationToken));

        Assert.Equal("Delivered", shipment.TrackingStatus);
        Assert.Equal(["Delivered", "Out for delivery"], shipment.TrackingEvents.Select(entry => entry.Message));
        Assert.Equal("Seattle, WA", shipment.TrackingEvents[0].Location);
        shipments.Verify(value => value.DeliverAsync(shipment, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task TheDefaultCarrierProvider_IsTheOneChosen_WhileItsEnabled()
    {
        var easyPost = Provider("EasyPost", enabled: true);
        var other = Provider("Other", enabled: false);

        DefaultCarrierProviderResolver Resolver(string chosen)
        {
            var site = new Mock<ISite>();
            site.Setup(value => value.GetOrCreate<CarrierProviderSettings>()).Returns(new CarrierProviderSettings { DefaultProviderName = chosen });
            var siteService = new Mock<ISiteService>();
            siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

            return new DefaultCarrierProviderResolver([easyPost, other], siteService.Object);
        }

        Assert.Same(easyPost, await Resolver("easypost").GetDefaultAsync());
        Assert.Null(await Resolver("Other").GetDefaultAsync());
        Assert.Null(await Resolver(null).GetDefaultAsync());
        Assert.Equal([easyPost], await Resolver(null).GetEnabledAsync());

        static ICarrierProvider Provider(string name, bool enabled)
        {
            var provider = new Mock<ICarrierProvider>();
            provider.SetupGet(value => value.Name).Returns(name);
            provider.Setup(value => value.IsEnabledAsync()).ReturnsAsync(enabled);

            return provider.Object;
        }
    }

    [Theory]
    [InlineData("USPS", "GroundAdvantage", "Ground Advantage")]
    [InlineData("UPS", "2ndDayAir", "2nd Day Air")]
    [InlineData("FedEx", "FEDEX_2_DAY_AM", "2 Day AM")]
    [InlineData("FedEx", "SMART_POST", "Smart Post")]
    [InlineData("FedEx", "FIRST_OVERNIGHT", "First Overnight")]
    public void ACarriersServiceCode_ReadsAsItsName(string carrier, string service, string expected)
        => Assert.Equal(expected, CarrierShippingRateProvider.DescribeService(carrier, service));

    [Theory]
    [InlineData("FedExDefault", "FedEx")]
    [InlineData("UPSDAP", "UPS")]
    [InlineData("USPS", "USPS")]
    public void EasyPostsOwnCarrierAccounts_ReadAsTheCarrier(string carrier, string expected)
        => Assert.Equal(expected, EasyPostCarrierProvider.CarrierName(carrier));

    [Fact]
    public void EasyPostsErrors_NameTheFieldsThatAreWrong()
        => Assert.Equal(
            "Missing required parameter. (shipment.to_address.zip: cannot be blank)",
            EasyPostCarrierProvider.DescribeError(JsonNode.Parse("""{"code":"PARAMETER.REQUIRED","message":"Missing required parameter.","errors":[{"field":"shipment.to_address.zip","message":"cannot be blank"}]}""") as JsonObject));

    [Theory]
    [InlineData("https://shop.example.com/store", "https://shop.example.com/store/shipping/easypost/webhook")]
    [InlineData("https://localhost:5390/shop", null)]
    [InlineData("http://shop.example.com", null)]
    [InlineData("https://shop.test", null)]
    [InlineData(null, null)]
    public void TrackingUpdates_AreRegisteredOnlyForAPublicHttpsAddress(string baseUrl, string expected)
        => Assert.Equal(expected, CrestApps.OrchardCore.Shipping.Drivers.EasyPostSettingsDisplayDriver.WebhookUrlFor(baseUrl));

    private static EasyPostCarrierProvider EasyPost(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var protection = new EphemeralDataProtectionProvider();
        var settings = new EasyPostSettings { IsEnabled = true, ApiKey = protection.CreateProtector(EasyPostCarrierProvider.ProtectionPurpose).Protect("EZTK_test") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient(EasyPostCarrierProvider.HttpClientName))
            .Returns(() => new HttpClient(new ReplyingHandler(respond)) { BaseAddress = new Uri("https://api.easypost.test/v2/") });

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<EasyPostSettings>()).Returns(settings);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new EasyPostCarrierProvider(factory.Object, siteService.Object, protection, NullLogger<EasyPostCarrierProvider>.Instance, new PassThroughStringLocalizer<EasyPostCarrierProvider>());
    }

    private static ISiteService SiteService(ShippingSettings settings)
    {
        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<ShippingSettings>()).Returns(settings);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class ReplyingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public ReplyingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_respond(request));
    }
}
