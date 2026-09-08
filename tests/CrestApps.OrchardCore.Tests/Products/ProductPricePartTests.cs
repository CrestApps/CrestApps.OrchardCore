using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A product can be offered on several sets of terms at once — monthly, annual, pay-what-you-want — and the
/// buyer picks one. These tests pin the rules that decide what they are actually charged, because every one
/// of them is a rule the browser can be made to break: the amount, the quantity, and whether the price is
/// still on offer all arrive from a form.
/// </summary>
public sealed class ProductPricePartTests
{
    private static readonly DateTime _now = new(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// A link straight to "buy" names no price, so the product's default is what gets charged.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WithoutAChoice_UsesTheDefaultPrice()
    {
        var product = CreateProduct(
            Price("monthly", 10m, isDefault: false),
            Price("annual", 100m, isDefault: true));

        var price = await Resolve(product, new ProductSnapshotContext(product));

        Assert.Equal("annual", price.PriceId);
        Assert.Equal(100m, price.UnitPrice);
    }

    /// <summary>
    /// The price the buyer chose is the price they pay.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WithAChoice_UsesIt()
    {
        var product = CreateProduct(
            Price("monthly", 10m, isDefault: true),
            Price("annual", 100m));

        var price = await Resolve(product, new ProductSnapshotContext(product) { PriceId = "annual" });

        Assert.Equal("annual", price.PriceId);
        Assert.Equal(100m, price.UnitPrice);
        Assert.True(price.IsRecurring);
    }

    /// <summary>
    /// An old link must not keep selling something the merchant withdrew.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_ForAWithdrawnPrice_RefusesToPrice()
    {
        var withdrawn = Price("legacy", 5m);
        withdrawn.IsActive = false;

        var product = CreateProduct(withdrawn, Price("current", 10m, isDefault: true));

        Assert.Null(await Resolve(product, new ProductSnapshotContext(product) { PriceId = "legacy" }));
    }

    /// <summary>
    /// A price that has not started being offered yet is not on offer.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_BeforeAPriceIsOffered_RefusesToPrice()
    {
        var future = Price("earlybird", 5m);
        future.EffectiveFromUtc = _now.AddDays(1);

        var product = CreateProduct(future);

        Assert.Null(await Resolve(product, new ProductSnapshotContext(product) { PriceId = "earlybird" }));
    }

    /// <summary>
    /// Pay-what-you-want means what the buyer names, within the bounds the merchant set.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WithACustomAmount_ChargesWhatTheBuyerNamed()
    {
        var product = CreateProduct(Custom("supporter", suggested: 10m, min: 5m, max: 500m));

        var price = await Resolve(product, new ProductSnapshotContext(product) { PriceId = "supporter", CustomAmount = 25m });

        Assert.Equal(25m, price.UnitPrice);
    }

    /// <summary>
    /// The bounds are enforced on the server, because the amount comes from a form anybody can edit.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10000)]
    public async Task ResolveAsync_WithAnAmountOutsideTheBounds_RefusesToPrice(decimal amount)
    {
        var product = CreateProduct(Custom("supporter", suggested: 10m, min: 5m, max: 500m));

        Assert.Null(await Resolve(product, new ProductSnapshotContext(product) { PriceId = "supporter", CustomAmount = amount }));
    }

    /// <summary>
    /// A price that does not invite an amount ignores one, rather than letting a crafted request set it.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WithACustomAmountOnAFixedPrice_ChargesTheFixedAmount()
    {
        var product = CreateProduct(Price("monthly", 10m, isDefault: true));

        var price = await Resolve(product, new ProductSnapshotContext(product) { CustomAmount = 1m });

        Assert.Equal(10m, price.UnitPrice);
    }

    /// <summary>
    /// A quantity multiplies the line, and the subtotal is what gets billed.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WithAQuantity_MultipliesTheSubtotal()
    {
        var seats = Price("seats", 10m, isDefault: true);
        seats.AllowQuantity = true;
        seats.MaximumQuantity = 20;

        var product = CreateProduct(seats);

        var price = await Resolve(product, new ProductSnapshotContext(product) { Quantity = 5 });

        Assert.Equal(5, price.Quantity);
        Assert.Equal(50m, price.Subtotal);
    }

    /// <summary>
    /// More than the merchant allows is refused rather than quietly reduced.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WithMoreThanTheMaximumQuantity_RefusesToPrice()
    {
        var seats = Price("seats", 10m, isDefault: true);
        seats.AllowQuantity = true;
        seats.MaximumQuantity = 3;

        var product = CreateProduct(seats);

        Assert.Null(await Resolve(product, new ProductSnapshotContext(product) { Quantity = 4 }));
    }

    /// <summary>
    /// A price that is sold one at a time cannot be multiplied by a crafted request.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WithAQuantityOnASinglePrice_RefusesToPrice()
    {
        var product = CreateProduct(Price("monthly", 10m, isDefault: true));

        Assert.Null(await Resolve(product, new ProductSnapshotContext(product) { Quantity = 3 }));
    }

    /// <summary>
    /// A price is never converted into the currency the caller asked for.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_InAnotherCurrency_RefusesToPrice()
    {
        var product = CreateProduct(Price("monthly", 10m, isDefault: true));

        Assert.Null(await Resolve(product, new ProductSnapshotContext(product) { Currency = "EUR" }));
    }

    /// <summary>
    /// A product with no prices of its own is still sold at the single price on its product part, so
    /// adding the part is opt-in rather than a migration every site has to survive.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WithoutThePricePart_FallsBackToTheProductPart()
    {
        var product = new ContentItem { ContentType = "Product", ContentItemId = "product-1" };
        product.Apply(nameof(ProductPart), new ProductPart { Price = 42m, Currency = "USD" });

        var price = await Resolve(product, new ProductSnapshotContext(product));

        Assert.Equal(42m, price.UnitPrice);
        Assert.Null(price.Price);
        Assert.False(price.IsRecurring);
    }

    private static async Task<PriceResult> Resolve(ContentItem product, ProductSnapshotContext context)
    {
        var resolver = new DefaultPriceResolver(
            new DefaultProductSnapshotResolver(CreateContentDefinitionManager()),
            new TestClock(_now),
            NullLogger<DefaultPriceResolver>.Instance);

        return await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);
    }

    private static ProductPrice Price(string id, decimal amount, bool isDefault = false)
        => new()
        {
            PriceId = id,
            Name = id,
            Currency = "USD",
            Amount = amount,
            Kind = PriceKind.Recurring,
            BillingDuration = 1,
            Interval = BillingInterval.Month,
            IsDefault = isDefault,
            IsActive = true,
        };

    private static ProductPrice Custom(string id, decimal suggested, decimal min, decimal max)
    {
        var price = Price(id, suggested, isDefault: true);

        price.AllowCustomAmount = true;
        price.MinimumAmount = min;
        price.MaximumAmount = max;

        return price;
    }

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
