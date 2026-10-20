using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Companies.Controllers;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Companies.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// A company's buyers pay its price list: a SKU's own price, or a percentage off the rest, and never more than the
/// store's price.
/// </summary>
public sealed class PriceListTests
{
    [Fact]
    public void Prices_ArePastedAsASkuAndAPriceOnEachLine()
    {
        var (entries, unread) = PriceListsAdminController.ParseEntries("HAT-1, 24.50\r\n\"CAP-2\",10\nMUG-3;7,25\nBOWL-4\t3.5\nno price here\nHAT-1, 22\n,5");

        Assert.Equal([("CAP-2", 10m), ("MUG-3", 7.25m), ("BOWL-4", 3.5m), ("HAT-1", 22m)], entries.Select(entry => (entry.Sku, entry.UnitPrice)));
        Assert.Equal(["no price here", ",5"], unread);
        Assert.Equal("CAP-2, 10\nMUG-3, 7.25\nBOWL-4, 3.5\nHAT-1, 22\n", PriceListsAdminController.FormatEntries(entries));
    }

    [Fact]
    public async Task AListPricesASkuItHas_AndTakesItsPercentageOffTheRest()
    {
        var list = new PriceList
        {
            Currency = "USD",
            PercentOff = 12.5m,
            Entries = [new PriceListEntry { Sku = "HAT-1", UnitPrice = 20m }],
        };

        Assert.Equal(20m, await CompanyPriceProvider.PriceAsync(list, Context("hat-1", 30m, "USD"), shopperCurrency: null, TestContext.Current.CancellationToken));
        Assert.Equal(7m, await CompanyPriceProvider.PriceAsync(list, Context("SOCK-1", 8m, "USD"), shopperCurrency: null, TestContext.Current.CancellationToken));

        list.PercentOff = null;

        Assert.Null(await CompanyPriceProvider.PriceAsync(list, Context("SOCK-1", 8m, "USD"), shopperCurrency: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AListInTheStoresCurrency_IsConvertedForABuyerInAMarket()
    {
        var list = new PriceList { Currency = "USD", Entries = [new PriceListEntry { Sku = "HAT-1", UnitPrice = 20m }] };
        var shopper = new Mock<IShopperCurrency>();
        shopper.Setup(value => value.ConvertAsync(20m, "USD", "EUR", true, It.IsAny<CancellationToken>())).ReturnsAsync(18.99m);

        Assert.Equal(18.99m, await CompanyPriceProvider.PriceAsync(list, Context("HAT-1", 27.99m, "EUR"), shopper.Object, TestContext.Current.CancellationToken));

        // Without a way to convert, the SKU's price isn't charged in the wrong currency.
        Assert.Null(await CompanyPriceProvider.PriceAsync(list, Context("HAT-1", 27.99m, "EUR"), shopperCurrency: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheResolver_ChargesTheLowestBuyerPrice_ButNeverMoreThanTheStores()
    {
        var product = new ContentItem { ContentType = "Product", ContentItemId = "hat" };
        product.Apply(nameof(ProductPart), new ProductPart { Price = 30m, Currency = "USD" });

        Assert.Equal(20m, (await ResolveAsync(product, 25m, 20m)).UnitPrice);
        Assert.Equal(30m, (await ResolveAsync(product, 45m)).UnitPrice);
        Assert.Equal(30m, (await ResolveAsync(product)).UnitPrice);
    }

    private static BuyerPriceContext Context(string sku, decimal unitPrice, string currency)
        => new(new ContentItem(), null, sku, unitPrice, currency, 1);

    private static async Task<PriceResult> ResolveAsync(ContentItem product, params decimal[] buyerPrices)
    {
        var providers = buyerPrices.Select(price =>
        {
            var provider = new Mock<IBuyerPriceProvider>();
            provider.Setup(value => value.GetUnitPriceAsync(It.IsAny<BuyerPriceContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(price);

            return provider.Object;
        });

        var resolver = new DefaultPriceResolver(
            new DefaultProductSnapshotResolver(CreateContentDefinitionManager()),
            new TestClock(new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)),
            NullLogger<DefaultPriceResolver>.Instance,
            new HttpContextAccessor(),
            Mock.Of<IShopperCurrency>(),
            [.. providers]);

        return await resolver.ResolveAsync(new ProductSnapshotContext(product) { Currency = "USD" }, TestContext.Current.CancellationToken);
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
