using System.Xml.Linq;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Products.Core.Feeds;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Feeds;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// The modules that own a product's weight, size, shipping and returns add them to its feed items, and the Merchant
/// Center feed carries the weight and size it now knows.
/// </summary>
public sealed class ProductFeedItemContributorTests
{
    private static readonly XNamespace _g = "http://base.google.com/ns/1.0";

    [Fact]
    public async Task Shipping_GivesAVariantItsOwnWeightAndSize_AndTheCheapestLocalRateToTheStoresCountry()
    {
        // Arrange
        var variant = new ProductVariant { VariantId = "V1" };
        var product = Product();

        product.Alter<ShippingPart>(part =>
        {
            part.Weight = 2m;
            part.Length = 30m;
            part.Width = 20m;
            part.Height = 10m;
            part.VariantWeights["V1"] = 3m;
            part.VariantDimensions["V1"] = new ShippingDimensions { Length = 40m, Width = 25m, Height = 12m };
        });

        var zone = new ShippingZone
        {
            ItemId = "ZONE",
            Name = "Domestic",
            Countries = ["US"],
            Methods =
            [
                new ShippingMethod { MethodId = "standard", Name = "Standard", ProviderName = ShippingConstants.Providers.Flat },
                new ShippingMethod { MethodId = "free", Name = "Free over 100", ProviderName = ShippingConstants.Providers.Free },
                new ShippingMethod { MethodId = "live", Name = "Live", ProviderName = ShippingConstants.Providers.Carrier },
            ],
        };

        var contributor = ShippingContributor(zone, new ShippingSettings { WeightUnit = WeightUnit.Pound, ShipFrom = new Address { Country = "us" } });
        var item = new ProductFeedItem { Price = 50m, Currency = "USD" };

        // Act
        await contributor.ContributeAsync(new ProductFeedItemContext(product, variant, ProductType.Good, item), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3m, item.Weight);
        Assert.Equal("lb", item.WeightUnit);
        Assert.Equal(40m, item.Length);
        Assert.Equal(12m, item.Height);
        Assert.Equal("in", item.DimensionsUnit);

        // The flat rate is 7.50, free shipping doesn't apply to 50, and the carrier's live rate isn't asked for.
        Assert.Equal(7.50m, item.ShippingPrice);
    }

    [Fact]
    public async Task Shipping_GivesADownloadNoShippingPrice_AndAProductWithoutAWeightNone()
    {
        // Arrange
        var zone = new ShippingZone
        {
            ItemId = "ZONE",
            Countries = ["US"],
            Methods = [new ShippingMethod { MethodId = "standard", ProviderName = ShippingConstants.Providers.Flat }],
        };

        var contributor = ShippingContributor(zone, new ShippingSettings { ShipFrom = new Address { Country = "US" } });
        var item = new ProductFeedItem { Price = 50m, Currency = "USD" };

        // Act
        await contributor.ContributeAsync(new ProductFeedItemContext(Product(), null, ProductType.Digital, item), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(item.Weight);
        Assert.Null(item.Length);
        Assert.Null(item.ShippingPrice);
    }

    [Theory]
    [InlineData(ProductType.Good, false, 30, true, true, 30, true)]
    [InlineData(ProductType.Good, false, 30, false, true, 30, false)]
    [InlineData(ProductType.Good, false, 0, true, false, null, false)]
    [InlineData(ProductType.Digital, true, 30, true, false, null, false)]
    [InlineData(ProductType.Service, false, 30, true, false, null, false)]
    public void Returns_ComeFromTheStoresReturnSettings_ForGoodsOnly(ProductType type, bool digital, int windowDays, bool exchanges, bool accepts, int? days, bool exchangesAccepted)
    {
        // Arrange
        var item = new ProductFeedItem { IsDigital = digital };

        // Act
        ReturnsProductFeedContributor.Apply(item, type, new ReturnSettings { WindowDays = windowDays, AllowExchanges = exchanges });

        // Assert
        Assert.Equal(accepts, item.AcceptsReturns);
        Assert.Equal(days, item.ReturnWindowDays);
        Assert.Equal(exchangesAccepted, item.AcceptsExchanges);
    }

    [Theory]
    [InlineData("/terms", "https://store.example.com/shop", "https://store.example.com/shop/terms")]
    [InlineData("privacy", "https://store.example.com", "https://store.example.com/privacy")]
    [InlineData("https://legal.example.com/tos", "https://store.example.com", "https://legal.example.com/tos")]
    [InlineData("/terms", null, null)]
    [InlineData("javascript:alert(1)", "https://store.example.com", null)]
    [InlineData(" ", "https://store.example.com", null)]
    public void APolicyLink_IsAbsolute_AfterTheSitesBaseUrl(string link, string baseUrl, string expected)
        => Assert.Equal(expected, ProductFeedBuilder.ToSiteUrl(link, baseUrl));

    [Fact]
    public void TheMerchantCenterFeed_CarriesTheWeightAndSize_OnlyWhenKnown()
    {
        // Arrange
        var sized = Valid("SKU-1");

        sized.Weight = 1.25m;
        sized.WeightUnit = "kg";
        sized.Length = 30m;
        sized.Width = 20m;
        sized.Height = 10m;
        sized.DimensionsUnit = "cm";

        // Act
        var xml = XDocument.Parse(ProductFeedWriter.ToXml(new ProductFeed([sized, Valid("SKU-2")], [], DateTime.UtcNow), "Contoso", "https://store.example.com"));
        var items = xml.Root.Element("channel").Elements("item").ToList();

        // Assert
        Assert.Equal("1.25 kg", items[0].Element(_g + "shipping_weight").Value);
        Assert.Equal("30 cm", items[0].Element(_g + "shipping_length").Value);
        Assert.Equal("10 cm", items[0].Element(_g + "shipping_height").Value);
        Assert.Null(items[1].Element(_g + "shipping_weight"));
        Assert.Null(items[1].Element(_g + "shipping_length"));
    }

    private static ShippingProductFeedContributor ShippingContributor(ShippingZone zone, ShippingSettings settings)
    {
        var site = new SiteSettings { SiteName = "Contoso" };

        site.Put(settings);

        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        var rates = new Mock<IShippingRateService>();

        rates.Setup(service => service.FindZoneAsync(It.Is<Address>(address => address.Country == "US"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(zone);

        var currency = new Mock<IShopperCurrency>();

        currency.Setup(service => service.GetStoreCurrencyAsync(It.IsAny<CancellationToken>())).ReturnsAsync("USD");

        IShippingRateProvider[] providers =
        [
            new FixedRateProvider(ShippingConstants.Providers.Flat, context => [context.CreateRate(7.50m)]),
            new FixedRateProvider(ShippingConstants.Providers.Free, context => context.Request.Subtotal >= 100m ? [context.CreateRate(0m)] : []),
            new FixedRateProvider(ShippingConstants.Providers.Carrier, context => [context.CreateRate(1m)]),
        ];

        return new ShippingProductFeedContributor(siteService.Object, rates.Object, providers, currency.Object);
    }

    private static ContentItem Product()
    {
        var item = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentType = "Product",
            DisplayText = "Shoe",
        };

        item.Alter<ProductPart>(part => part.Price = 50m);

        return item;
    }

    private static ProductFeedItem Valid(string id)
        => new()
        {
            Id = id,
            Title = "Hat",
            Description = "A hat.",
            Link = "https://store.example.com/hat",
            ImageLink = "https://store.example.com/media/hat.jpg",
            Availability = "in_stock",
            Price = 20m,
            Currency = "USD",
        };

    private sealed class FixedRateProvider : IShippingRateProvider
    {
        private readonly Func<ShippingRateContext, IReadOnlyList<ShippingRate>> _rates;

        public FixedRateProvider(string name, Func<ShippingRateContext, IReadOnlyList<ShippingRate>> rates)
        {
            Name = name;
            _rates = rates;
        }

        public string Name { get; }

        public LocalizedString DisplayName => new(Name, Name);

        public LocalizedString Description => new(Name, Name);

        public Task<IReadOnlyList<ShippingRate>> GetRatesAsync(ShippingRateContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(_rates(context));
    }
}
