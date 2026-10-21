using System.Security.Claims;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using Xunit;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// A cart holds what a visitor chose and is priced again whenever it is shown. These tests pin what may go in a
/// cart, how a guest's cart is found again, and what happens to it when the guest signs in.
/// </summary>
public sealed class CartServiceTests
{
    private readonly Harness _harness = new();

    [Fact]
    public async Task AGuestsCart_IsFoundAgainOnTheirNextVisit()
    {
        _harness.AddProduct("mug", unitPrice: 12m);

        var result = await _harness.NewRequest().AddAsync("mug", priceId: null, quantity: 2, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);

        var cart = await _harness.NextRequest().GetCurrentAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(cart);
        Assert.Null(cart.UserId);
        Assert.NotNull(cart.GuestTokenHash);
        var line = Assert.Single(cart.Lines);
        Assert.Equal(2, line.Quantity);
        Assert.Equal("USD", cart.Currency);
    }

    [Fact]
    public async Task ACountedProduct_CantBeAddedBeyondWhatsLeft()
    {
        _harness.AddProduct("mug", unitPrice: 12m);
        _harness.Stock["mug"] = new ProductAvailability(3, AllowBackorder: false, LowStockThreshold: 5);

        var tooMany = await _harness.NewRequest().AddAsync("mug", null, 4, TestContext.Current.CancellationToken);
        Assert.False(tooMany.Succeeded);
        Assert.Equal("Only 3 of mug left.", tooMany.Message.Value);

        var three = await _harness.NewRequest().AddAsync("mug", null, 3, TestContext.Current.CancellationToken);
        Assert.True(three.Succeeded);
    }

    [Fact]
    public async Task AnOutOfStockProduct_IsRefused_UnlessItTakesBackorders()
    {
        _harness.AddProduct("mug", unitPrice: 12m);
        _harness.AddProduct("lamp", unitPrice: 40m);
        _harness.Stock["mug"] = new ProductAvailability(0, AllowBackorder: false, LowStockThreshold: 5);
        _harness.Stock["lamp"] = new ProductAvailability(0, AllowBackorder: true, LowStockThreshold: 5);

        var mug = await _harness.NewRequest().AddAsync("mug", null, 1, TestContext.Current.CancellationToken);
        Assert.Equal("mug is out of stock.", mug.Message.Value);

        Assert.True((await _harness.NewRequest().AddAsync("lamp", null, 5, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task ALineThatSoldOutSinceItWasAdded_StopsTheCheckout()
    {
        _harness.AddProduct("mug", unitPrice: 12m);
        await _harness.NewRequest().AddAsync("mug", null, 2, TestContext.Current.CancellationToken);

        _harness.Stock["mug"] = new ProductAvailability(1, AllowBackorder: false, LowStockThreshold: 5);

        var service = _harness.NextRequest();
        var pricing = await service.PriceAsync(await service.GetCurrentAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        var line = Assert.Single(pricing.Lines);

        Assert.False(pricing.CanCheckout);
        Assert.Equal(1, line.MaxQuantity);
        Assert.Equal("Only 1 left. Lower the quantity to check out.", line.Problem.Value);
    }

    [Fact]
    public async Task AnotherBrowser_DoesNotSeeTheCart()
    {
        _harness.AddProduct("mug", unitPrice: 12m);

        await _harness.NewRequest().AddAsync("mug", null, 1, TestContext.Current.CancellationToken);

        Assert.Null(await _harness.NewRequest().GetCurrentAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddingTheSameProductAgain_AddsToItsLine()
    {
        _harness.AddProduct("mug", unitPrice: 12m);

        await _harness.NewRequest().AddAsync("mug", null, 1, TestContext.Current.CancellationToken);
        var result = await _harness.NextRequest().AddAsync("mug", null, 3, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(4, Assert.Single(result.Cart.Lines).Quantity);
    }

    [Fact]
    public async Task AnAddOn_AddsItsPriceToEachUnit_AndKeepsWhatWasTyped()
    {
        WithAddOns(_harness.AddProduct("mug", unitPrice: 12m));

        var service = _harness.NewRequest();
        var result = await service.AddAsync("mug", null, null, 2, new Dictionary<string, string> { ["name"] = "  Sam  ", ["wrap"] = "true", ["unknown"] = "x" }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var line = Assert.Single(result.Cart.Lines);
        Assert.Equal(15.5m, line.AddedUnitPrice);
        Assert.Equal(new Dictionary<string, string> { ["name"] = "Sam", ["wrap"] = "true" }, line.AddOns);

        var next = _harness.NextRequest();
        var pricing = await next.PriceAsync(await next.GetCurrentAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        var priced = Assert.Single(pricing.Lines);
        Assert.Equal(15.5m, priced.UnitPrice);
        Assert.Equal(31m, priced.LineTotal);
        Assert.False(priced.PriceChanged);
        Assert.Collection(priced.AddOns,
            name => Assert.Equal(("Name", "Sam", 1m), (name.Label, name.Value, name.Price)),
            wrap => Assert.Equal(("Gift wrap", (string)null, 2.5m), (wrap.Label, wrap.Value, wrap.Price)));
    }

    [Fact]
    public async Task ARequiredAddOn_LeftOut_IsRefused()
    {
        WithAddOns(_harness.AddProduct("mug", unitPrice: 12m));

        var result = await _harness.NewRequest().AddAsync("mug", null, null, 1, new Dictionary<string, string> { ["wrap"] = "true", ["name"] = "   " }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("Name", result.Message.Value);
    }

    [Fact]
    public async Task TheSameProductWithOtherAddOns_IsAnotherLine()
    {
        WithAddOns(_harness.AddProduct("mug", unitPrice: 12m));

        await _harness.NewRequest().AddAsync("mug", null, null, 1, new Dictionary<string, string> { ["name"] = "Sam" }, TestContext.Current.CancellationToken);
        await _harness.NextRequest().AddAsync("mug", null, null, 1, new Dictionary<string, string> { ["name"] = "Alex" }, TestContext.Current.CancellationToken);
        var result = await _harness.NextRequest().AddAsync("mug", null, null, 2, new Dictionary<string, string> { ["name"] = "Sam" }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Cart.Lines.Count);
        Assert.Equal(3, result.Cart.Lines.Single(line => line.AddOns["name"] == "Sam").Quantity);
    }

    private static void WithAddOns(ContentItem item)
        => item.Alter<ProductPart>(part => part.AddOns =
        [
            new ProductAddOn { AddOnId = "name", Label = "Name", Kind = ProductAddOnKind.Text, Price = 1m, Required = true },
            new ProductAddOn { AddOnId = "wrap", Label = "Gift wrap", Kind = ProductAddOnKind.Checkbox, Price = 2.5m },
        ]);

    [Fact]
    public async Task APriceBoughtOnlyOnce_CannotBeAddedTwice()
    {
        _harness.AddProduct("poster", unitPrice: 30m, price: new ProductPrice { PriceId = "p1", Amount = 30m, Currency = "USD", Kind = PriceKind.OneTime, AllowQuantity = false });

        Assert.True((await _harness.NewRequest().AddAsync("poster", "p1", 1, TestContext.Current.CancellationToken)).Succeeded);

        var second = await _harness.NextRequest().AddAsync("poster", "p1", 1, TestContext.Current.CancellationToken);

        Assert.False(second.Succeeded);
        Assert.Equal(1, Assert.Single(second.Cart.Lines).Quantity);
    }

    [Fact]
    public async Task ARecurringPrice_IsRefused()
    {
        _harness.AddProduct("box", unitPrice: 20m, price: new ProductPrice { PriceId = "monthly", Amount = 20m, Currency = "USD", Kind = PriceKind.Recurring });

        var result = await _harness.NewRequest().AddAsync("box", "monthly", 1, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(await _harness.NextRequest().GetCurrentAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ASubscriptionPlan_IsRefused_EvenWhenPricedOnItsProductPart()
    {
        var plan = _harness.AddProduct("plan", unitPrice: 10m);
        plan.Weld("SubscriptionPart", new ContentPart());

        var result = await _harness.NewRequest().AddAsync("plan", null, 1, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AProductThatCannotBeBoughtOnline_IsRefused()
    {
        _harness.AddProduct("house", unitPrice: 450_000m, sellable: false);

        var result = await _harness.NewRequest().AddAsync("house", null, 1, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AProductInAnotherCurrency_IsRefused()
    {
        _harness.AddProduct("mug", unitPrice: 12m);
        _harness.AddProduct("tea", unitPrice: 8m, currency: "EUR");

        await _harness.NewRequest().AddAsync("mug", null, 1, TestContext.Current.CancellationToken);
        var result = await _harness.NextRequest().AddAsync("tea", null, 1, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Single(result.Cart.Lines);
    }

    [Fact]
    public async Task SettingAQuantityBelowOne_RemovesTheLine()
    {
        _harness.AddProduct("mug", unitPrice: 12m);

        var added = await _harness.NewRequest().AddAsync("mug", null, 2, TestContext.Current.CancellationToken);
        var result = await _harness.NextRequest().UpdateQuantityAsync(added.Cart.Lines[0].LineId, 0, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Cart.Lines);
        Assert.Null(result.Cart.Currency);
    }

    [Fact]
    public async Task Pricing_UsesTodaysPrice_AndPointsOutWhatChanged()
    {
        _harness.AddProduct("mug", unitPrice: 12m);
        _harness.AddProduct("lamp", unitPrice: 40m);

        await _harness.NewRequest().AddAsync("mug", null, 2, TestContext.Current.CancellationToken);
        await _harness.NextRequest().AddAsync("lamp", null, 1, TestContext.Current.CancellationToken);

        _harness.AddProduct("mug", unitPrice: 15m);
        _harness.Remove("lamp");

        var service = _harness.NextRequest();
        var pricing = await service.PriceAsync(await service.GetCurrentAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        var mug = pricing.Lines.Single(line => line.Line.ContentItemId == "mug");
        var lamp = pricing.Lines.Single(line => line.Line.ContentItemId == "lamp");

        Assert.Equal(15m, mug.UnitPrice);
        Assert.Equal(30m, mug.LineTotal);
        Assert.True(mug.PriceChanged);
        Assert.False(lamp.IsAvailable);
        Assert.Equal(30m, pricing.Subtotal);
        Assert.False(pricing.CanCheckout);
    }

    [Fact]
    public async Task SigningIn_MakesTheGuestCartTheUsers_WhenTheyHaveNone()
    {
        _harness.AddProduct("mug", unitPrice: 12m);

        await _harness.NewRequest().AddAsync("mug", null, 2, TestContext.Current.CancellationToken);
        var guestCartId = _harness.Store.Carts.Single().ItemId;

        await _harness.NextRequest().MergeGuestCartAsync("user-1", TestContext.Current.CancellationToken);

        var cart = Assert.Single(_harness.Store.Carts);
        Assert.Equal(guestCartId, cart.ItemId);
        Assert.Equal("user-1", cart.UserId);
        Assert.Null(cart.GuestTokenHash);
    }

    [Fact]
    public async Task SigningIn_AddsTheGuestCartToTheUsersCart()
    {
        _harness.AddProduct("mug", unitPrice: 12m);
        _harness.AddProduct("lamp", unitPrice: 40m);

        await _harness.NewRequest(userId: "user-1").AddAsync("mug", null, 1, TestContext.Current.CancellationToken);
        await _harness.NewRequest().AddAsync("mug", null, 2, TestContext.Current.CancellationToken);
        await _harness.NextRequest().AddAsync("lamp", null, 1, TestContext.Current.CancellationToken);

        await _harness.NextRequest().MergeGuestCartAsync("user-1", TestContext.Current.CancellationToken);

        var cart = Assert.Single(_harness.Store.Carts);
        Assert.Equal("user-1", cart.UserId);
        Assert.Equal(3, cart.Lines.Single(line => line.ContentItemId == "mug").Quantity);
        Assert.Equal(1, cart.Lines.Single(line => line.ContentItemId == "lamp").Quantity);
    }

    [Fact]
    public async Task AnExpiredCart_IsNoLongerTheVisitors()
    {
        _harness.AddProduct("mug", unitPrice: 12m);

        await _harness.NewRequest().AddAsync("mug", null, 1, TestContext.Current.CancellationToken);

        _harness.Now = _harness.Now.AddDays(31);

        Assert.Null(await _harness.NextRequest().GetCurrentAsync(TestContext.Current.CancellationToken));
    }

    private sealed class Harness
    {
        private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();
        private readonly Dictionary<string, (ContentItem Item, ISellableProduct Product, ProductPrice Price)> _products = [];
        private readonly HttpContextAccessor _accessor = new();

        public FakeCartStore Store { get; } = new();

        /// <summary>
        /// Gets what's available of each counted product, by content item id. A product missing here isn't counted.
        /// </summary>
        public Dictionary<string, ProductAvailability> Stock { get; } = [];

        public IEnumerable<IProductAvailabilityProvider> Availability
            => [new StockProvider(Stock)];

        public DateTime Now { get; set; } = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

        public ContentItem AddProduct(string id, decimal unitPrice, string currency = "USD", bool sellable = true, ProductPrice price = null)
        {
            var item = new ContentItem { ContentItemId = id, ContentItemVersionId = id + "-v1", ContentType = "Product", DisplayText = id };
            var product = sellable
                ? new SellableProduct { ContentItemId = id, ContentItemVersionId = id + "-v1", Title = id, UnitPrice = unitPrice, Currency = currency }
                : null;

            _products[id] = (item, product, price);

            return item;
        }

        public void Remove(string id)
            => _products.Remove(id);

        /// <summary>
        /// A request from a new browser, optionally signed in.
        /// </summary>
        public CartService NewRequest(string userId = null)
        {
            var context = new DefaultHttpContext();

            if (userId is not null)
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));
            }

            _accessor.HttpContext = context;

            return CreateService();
        }

        /// <summary>
        /// The next request from the same browser, carrying the cookies the last response set.
        /// </summary>
        public CartService NextRequest()
        {
            var previous = (DefaultHttpContext)_accessor.HttpContext;
            var context = new DefaultHttpContext { User = previous.User };

            var cookies = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var pair in (previous.Request.Headers.Cookie.ToString() ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = pair.IndexOf('=');
                cookies[pair[..separator]] = pair[(separator + 1)..];
            }

            foreach (var setCookie in previous.Response.Headers.SetCookie)
            {
                var pair = setCookie.Split(';')[0];
                var separator = pair.IndexOf('=');
                cookies[pair[..separator]] = pair[(separator + 1)..];
            }

            context.Request.Headers.Cookie = string.Join("; ", cookies.Select(cookie => $"{cookie.Key}={cookie.Value}"));
            _accessor.HttpContext = context;

            return CreateService();
        }

        private CartService CreateService()
        {
            var contentManager = new Mock<IContentManager>();
            contentManager
                .Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
                .ReturnsAsync((string id, VersionOptions _) => _products.TryGetValue(id, out var entry) ? entry.Item : null);

            var snapshots = new Mock<IProductSnapshotResolver>();
            snapshots
                .Setup(resolver => resolver.ResolveAsync(It.IsAny<ProductSnapshotContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProductSnapshotContext context, CancellationToken _) => _products.TryGetValue(context.ContentItem.ContentItemId, out var entry) ? entry.Product : null);

            var prices = new Mock<IPriceResolver>();
            prices
                .Setup(resolver => resolver.ResolveAsync(It.IsAny<ProductSnapshotContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProductSnapshotContext context, CancellationToken _) => Price(context));

            var clock = new Mock<IClock>();
            clock.Setup(c => c.UtcNow).Returns(() => Now);

            return new CartService(
                Store,
                Mock.Of<ISession>(),
                new GuestSessionTokenManager(_accessor, _protection),
                _accessor,
                contentManager.Object,
                snapshots.Object,
                prices.Object,
                Availability,
                clock.Object,
                Options.Create(new CartOptions()),
                NullLogger<CartService>.Instance,
                new PassThroughStringLocalizer<CartService>());
        }

        // Mirrors the product price resolver: a listed price when there is one, the product part's price otherwise.
        private PriceResult Price(ProductSnapshotContext context)
        {
            if (!_products.TryGetValue(context.ContentItem.ContentItemId, out var entry) || entry.Product is null)
            {
                return null;
            }

            if (entry.Price is null)
            {
                return new PriceResult(entry.Product.UnitPrice, entry.Product.Currency, context.Quantity);
            }

            if (context.PriceId is not null && context.PriceId != entry.Price.PriceId)
            {
                return null;
            }

            var quantity = entry.Price.GetChargeableQuantity(context.Quantity);

            return quantity is null ? null : new PriceResult(entry.Price.Amount, entry.Price.Currency, quantity.Value, entry.Price);
        }
    }

    private sealed class StockProvider : IProductAvailabilityProvider
    {
        private readonly Dictionary<string, ProductAvailability> _stock;

        public StockProvider(Dictionary<string, ProductAvailability> stock)
            => _stock = stock;

        public Task<IReadOnlyDictionary<ProductStockKey, ProductAvailability>> GetAvailabilityAsync(ProductAvailabilityRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<ProductStockKey, ProductAvailability>>(request.Products
                .Where(key => _stock.ContainsKey(key.ContentItemId))
                .ToDictionary(key => key, key => _stock[key.ContentItemId]));
    }

    private sealed class FakeCartStore : ICartStore
    {
        public List<Cart> Carts { get; } = [];

        public ValueTask<Cart> FindByIdAsync(string cartId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Carts.FirstOrDefault(cart => cart.ItemId == cartId));

        public Task<Cart> FindByUserAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(userId is null ? null : Carts.FirstOrDefault(cart => cart.UserId == userId));

        public Task<Cart> FindByGuestTokenHashAsync(string guestTokenHash, CancellationToken cancellationToken = default)
            => Task.FromResult(guestTokenHash is null ? null : Carts.FirstOrDefault(cart => cart.GuestTokenHash == guestTokenHash));

        public Task<IReadOnlyList<Cart>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Cart>>(Carts.Where(cart => cart.ExpiresUtc < nowUtc).Take(take).ToArray());

        public ValueTask CreateAsync(Cart cart, CancellationToken cancellationToken = default)
        {
            Carts.Add(cart);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(Cart cart, CancellationToken cancellationToken = default)
        {
            if (!Carts.Contains(cart))
            {
                Carts.Add(cart);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> DeleteAsync(Cart cart, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Carts.Remove(cart));
    }
}
