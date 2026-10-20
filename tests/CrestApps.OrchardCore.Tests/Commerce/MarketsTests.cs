using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Markets.Controllers;
using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.Markets.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// A shopper in a market pays in its currency: a product's own price there wins, otherwise its price is converted at
/// the store's rate and rounded up by the market's rule. Subscriptions and amounts the buyer types are never converted,
/// and shipping is quoted in the market's currency.
/// </summary>
public sealed class MarketsTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(23.00, "EUR", MarketPriceRounding.EndIn99, 23.99)]
    [InlineData(27.60, "EUR", MarketPriceRounding.EndIn99, 27.99)]
    [InlineData(27.995, "EUR", MarketPriceRounding.EndIn99, 28.99)]
    [InlineData(23.96, "EUR", MarketPriceRounding.EndIn95, 24.95)]
    [InlineData(23.50, "EUR", MarketPriceRounding.EndIn95, 23.95)]
    [InlineData(23.01, "EUR", MarketPriceRounding.WholeUnits, 24.00)]
    [InlineData(23.456, "EUR", MarketPriceRounding.None, 23.46)]
    [InlineData(1234.2, "JPY", MarketPriceRounding.EndIn99, 1235)]
    public void AConvertedPrice_RoundsUpByTheMarketsRule(decimal amount, string currency, MarketPriceRounding rounding, decimal expected)
        => Assert.Equal(expected, MarketPricing.Round(amount, currency, rounding));

    [Fact]
    public void Converting_GoesThroughTheStoresCurrency()
    {
        Assert.Equal(23m, MarketPricing.Convert(25m, 1m, 0.92m));
        Assert.Equal(25m, MarketPricing.Convert(23m, 0.92m, 1m));
        Assert.Equal(0.79m * 100m / 0.92m, MarketPricing.Convert(100m, 0.92m, 0.79m));
    }

    [Fact]
    public void AMarket_IsFoundByItsId_OrACountryInIt_OrTheShoppersLanguage()
    {
        IReadOnlyList<Market> markets = [Germany(), new Market { ItemId = "uk", Name = "United Kingdom", Countries = ["GB"], Currency = "GBP", ExchangeRate = 0.79m }];

        Assert.Equal("de", ShopperMarketService.Find(markets, "de")?.ItemId);
        Assert.Equal("de", ShopperMarketService.Find(markets, "at")?.ItemId);
        Assert.Null(ShopperMarketService.Find(markets, "FR"));
        Assert.Null(ShopperMarketService.Find(markets, " "));
        Assert.Equal("uk", ShopperMarketService.FindByCulture(markets, CultureInfo.GetCultureInfo("en-GB"))?.ItemId);
        Assert.Null(ShopperMarketService.FindByCulture(markets, CultureInfo.GetCultureInfo("de")));
        Assert.Null(ShopperMarketService.FindByCulture(markets, CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void AMarketsCountries_AreTwoLetterCodes_WithoutRepeats()
        => Assert.Equal(["DE", "AT"], MarketsAdminController.ParseCountries(" de, AT;de\nat "));

    [Fact]
    public async Task AProduct_IsConvertedIntoTheShoppersCurrency()
    {
        var product = CreateProduct(OneTime("standard", 30m, "USD", isDefault: true));

        var price = await ResolveAsync(product, "EUR");

        Assert.Equal(27.99m, price.UnitPrice);
        Assert.Equal("EUR", price.Currency);
    }

    [Fact]
    public async Task AProductsOwnPriceInTheShoppersCurrency_Wins()
    {
        var product = CreateProduct(OneTime("standard", 30m, "USD", isDefault: true), OneTime("standard-eur", 25m, "EUR"));

        var price = await ResolveAsync(product, "EUR");

        Assert.Equal(25m, price.UnitPrice);
        Assert.Equal("standard-eur", price.PriceId);
    }

    [Fact]
    public async Task ASubscription_OrAnAmountTheBuyerTypes_IsNeverConverted()
    {
        var monthly = OneTime("monthly", 10m, "USD", isDefault: true);
        monthly.Kind = PriceKind.Recurring;
        monthly.Interval = BillingInterval.Month;
        monthly.BillingDuration = 1;

        var donation = OneTime("donation", 10m, "USD", isDefault: true);
        donation.AllowCustomAmount = true;
        donation.MinimumAmount = 1m;
        donation.MaximumAmount = 100m;

        Assert.Null(await ResolveAsync(CreateProduct(monthly), "EUR"));
        Assert.Null(await ResolveAsync(CreateProduct(donation), "EUR"));
    }

    [Fact]
    public async Task AProductWithASinglePrice_IsConvertedToo()
    {
        var product = new ContentItem { ContentType = "Product", ContentItemId = "product-1" };
        product.Apply(nameof(ProductPart), new ProductPart { Price = 25m, Currency = "USD" });

        var price = await ResolveAsync(product, "EUR");

        Assert.Equal(23.99m, price.UnitPrice);
        Assert.Equal("EUR", price.Currency);
    }

    [Fact]
    public async Task WithoutMarkets_APriceInAnotherCurrency_IsStillRefused()
    {
        var product = CreateProduct(OneTime("standard", 30m, "USD", isDefault: true));

        Assert.Null(await ResolveAsync(product, "EUR", new ProductOwnShopperCurrency()));
    }

    [Fact]
    public async Task Shipping_IsQuotedInTheMarketsCurrency_ConvertedWhenTheZoneHasNoneInIt()
    {
        var flat = Flat("International", 25m);
        var zone = new ShippingZone { ItemId = "world", Name = "World", RestOfWorld = true, Methods = [flat] };

        var rates = await QuoteAsync(zone, "EUR");

        Assert.Equal([("International", 23m, "EUR")], rates.Select(rate => (rate.Name, rate.Amount, rate.Currency)));
    }

    [Fact]
    public async Task Shipping_UsesTheZonesOwnRatesInTheMarketsCurrency_WhenItHasSome()
    {
        var dollars = Flat("International", 25m);
        var euros = Flat("Europe", 9m);
        euros.Currency = "EUR";
        var zone = new ShippingZone { ItemId = "world", Name = "World", RestOfWorld = true, Methods = [dollars, euros] };

        var rates = await QuoteAsync(zone, "EUR");

        Assert.Equal([("Europe", 9m)], rates.Select(rate => (rate.Name, rate.Amount)));
    }

    [Theory]
    [InlineData("DE", "EUR", true)]
    [InlineData("US", "EUR", false)]
    [InlineData("DE", "USD", false)]
    public async Task ACheckout_InAMarketWhosePricesIncludeTax_IsTaxedInclusively(string country, string currency, bool inclusive)
    {
        var store = new Mock<IMarketStore>();
        store.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Germany()]);
        var markets = new ShopperMarketService(store.Object, new HttpContextAccessor(), Mock.Of<ISiteService>());
        var profile = new CheckoutTaxProfile { Destination = new Address { Country = country } };

        await new MarketCheckoutTaxProfileHandler(markets).BuildingAsync(profile, new CheckoutSession { SessionId = "s", Currency = currency }, TestContext.Current.CancellationToken);

        Assert.Equal(inclusive ? TaxPriceType.Inclusive : TaxPriceType.Exclusive, profile.PriceType);
    }

    [Fact]
    public async Task AProductLeftOutOfTheShoppersMarket_IsntSoldThere()
    {
        var store = new Mock<IMarketStore>();
        store.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Germany()]);
        var german = new DefaultHttpContext();
        german.Request.Headers[CrestApps.OrchardCore.Markets.MarketsConstants.HeaderName] = "DE";
        var american = new DefaultHttpContext();
        var product = new ContentItem { ContentType = "Product", ContentItemId = "product-1" };
        product.Apply(nameof(ProductMarketsPart), new ProductMarketsPart { NotSoldIn = ["de"] });

        string RestrictionIn(HttpContext httpContext)
            => new MarketSaleRestriction(new ShopperMarketService(store.Object, new HttpContextAccessor { HttpContext = httpContext }, Mock.Of<ISiteService>()), new PassThroughStringLocalizer<MarketSaleRestriction>())
                .GetRestrictionAsync(product, TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult();

        Assert.Equal("Not sold in Germany.", RestrictionIn(german));
        Assert.Null(RestrictionIn(american));
        Assert.Null(await new MarketSaleRestriction(new ShopperMarketService(store.Object, new HttpContextAccessor { HttpContext = german }, Mock.Of<ISiteService>()), new PassThroughStringLocalizer<MarketSaleRestriction>())
            .GetRestrictionAsync(new ContentItem { ContentType = "Product" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AnOrdersEmails_AreWrittenInTheCultureTheBuyerShoppedIn()
    {
        var before = CultureInfo.CurrentUICulture.Name;

        using (CrestApps.OrchardCore.Orders.Core.Services.OrderCulture.Use("de-DE"))
        {
            Assert.Equal("de-DE", CultureInfo.CurrentUICulture.Name);
            Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name);
        }

        Assert.Equal(before, CultureInfo.CurrentUICulture.Name);
        Assert.Null(CrestApps.OrchardCore.Orders.Core.Services.OrderCulture.Use(null));
        Assert.Null(CrestApps.OrchardCore.Orders.Core.Services.OrderCulture.Use(" "));
    }

    private static Market Germany()
        => new() { ItemId = "de", Name = "Germany", Countries = ["DE", "AT"], Currency = "EUR", ExchangeRate = 0.92m, Rounding = MarketPriceRounding.EndIn99, PricesIncludeTax = true };

    // A shopper buying in euros at 0.92 per dollar, with prices rounded up to .99.
    private sealed class EuroShopper : IShopperCurrency
    {
        public ValueTask<string> GetCurrencyAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult("EUR");

        public ValueTask<string> GetStoreCurrencyAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult("USD");

        public ValueTask<decimal?> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, bool isPrice = true, CancellationToken cancellationToken = default)
        {
            if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult<decimal?>(amount);
            }

            if ((fromCurrency ?? "USD") != "USD" || toCurrency != "EUR")
            {
                return ValueTask.FromResult<decimal?>(null);
            }

            var converted = MarketPricing.Convert(amount, 1m, 0.92m);

            return ValueTask.FromResult<decimal?>(isPrice ? MarketPricing.Round(converted, "EUR", MarketPriceRounding.EndIn99) : Math.Round(converted, 2));
        }
    }

    private static async Task<PriceResult> ResolveAsync(ContentItem product, string currency, IShopperCurrency shopper = null)
    {
        var resolver = new DefaultPriceResolver(
            new DefaultProductSnapshotResolver(CreateContentDefinitionManager()),
            new TestClock(_now),
            NullLogger<DefaultPriceResolver>.Instance,
            new HttpContextAccessor(),
            shopper ?? new EuroShopper());

        return await resolver.ResolveAsync(new ProductSnapshotContext(product) { Currency = currency }, TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<ShippingRate>> QuoteAsync(ShippingZone zone, string currency)
    {
        var catalog = new Mock<INamedCatalog<ShippingZone>>();
        catalog.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([zone]);

        var service = new DefaultShippingRateService(
            catalog.Object,
            [new FlatRateShippingProvider(new PassThroughStringLocalizer<FlatRateShippingProvider>())],
            NullLogger<DefaultShippingRateService>.Instance,
            new EuroShopper());

        return await service.GetRatesAsync(new ShippingRateRequest
        {
            Destination = new Address { Country = "DE" },
            Currency = currency,
            Lines = [new() { ContentItemId = "item", Quantity = 1, UnitPrice = 20m }],
        }, TestContext.Current.CancellationToken);
    }

    private static ShippingMethod Flat(string name, decimal amount)
    {
        var method = new ShippingMethod { MethodId = name.ToLowerInvariant(), Name = name, ProviderName = ShippingConstants.Providers.Flat, Enabled = true };
        method.Put(new FlatRateSettings { Amount = amount });

        return method;
    }

    private static ProductPrice OneTime(string id, decimal amount, string currency, bool isDefault = false)
        => new() { PriceId = id, Name = id.Split('-')[0], Currency = currency, Amount = amount, Kind = PriceKind.OneTime, IsDefault = isDefault, IsActive = true };

    private static ContentItem CreateProduct(params ProductPrice[] prices)
    {
        var contentItem = new ContentItem { ContentType = "Product", ContentItemId = "product-1" };

        contentItem.Apply(nameof(ProductPart), new ProductPart { Price = 1m, Currency = "USD" });
        contentItem.Apply(nameof(ProductPricePart), new ProductPricePart { Prices = [.. prices] });

        return contentItem;
    }

    private static IContentDefinitionManager CreateContentDefinitionManager()
    {
        var settings = new JsonObject
        {
            ["ProductPartSettings"] = new JsonObject { ["DefaultCurrency"] = "USD" },
        };

        var definition = new ContentTypeDefinition(
            "Product",
            "Product",
            [new ContentTypePartDefinition(nameof(ProductPart), new ContentPartDefinition(nameof(ProductPart)), settings)],
            []);

        var manager = new Mock<IContentDefinitionManager>();
        manager.Setup(m => m.GetTypeDefinitionAsync("Product")).ReturnsAsync(definition);

        return manager.Object;
    }
}
