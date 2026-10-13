using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Handlers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Modules;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// A cart becomes an order, the order is billed through the checkout line by line, and it is marked paid only
/// when the checkout completes, taking what was bought out of the cart.
/// </summary>
public sealed class OrderCheckoutTests
{
    private static readonly DateTime _now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeOrderStore _orders = new();
    private readonly FakeCartStore _carts = new();

    [Fact]
    public async Task ACart_BecomesADraftOrder_WithItsLinesFrozen()
    {
        var pricing = CreatePricing(CreateCart(("mug", 2, 12m), ("lamp", 1, 40m)));

        var order = await CreateService().CreateFromCartAsync(pricing, TestContext.Current.CancellationToken);

        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal("cart-1", order.CartId);
        Assert.Equal("USD", order.Currency);
        Assert.Equal(64m, order.Subtotal);
        Assert.Matches(@"^261013-[0-9A-HJKMNP-TV-Z]{6}$", order.OrderNumber);

        var mug = order.Lines.Single(line => line.ContentItemId == "mug");

        Assert.Equal(2, mug.Quantity);
        Assert.Equal(12m, mug.UnitPrice);
        Assert.Equal(24m, mug.LineTotal);
        Assert.Equal("SKU-mug", mug.Sku);
        Assert.Equal("books", mug.TaxCategoryCode);
    }

    [Fact]
    public async Task CheckingOutAgain_CancelsTheEarlierUnpaidOrder()
    {
        var cart = CreateCart(("mug", 1, 12m));
        var service = CreateService();

        var first = await service.CreateFromCartAsync(CreatePricing(cart), TestContext.Current.CancellationToken);
        var second = await service.CreateFromCartAsync(CreatePricing(cart), TestContext.Current.CancellationToken);

        Assert.Equal(OrderStatus.Canceled, first.Status);
        Assert.Equal(OrderStatus.Draft, second.Status);
    }

    [Fact]
    public async Task ACartWithALineThatCannotBeBought_DoesNotBecomeAnOrder()
    {
        var pricing = CreatePricing(CreateCart(("mug", 1, 12m)), unavailable: "mug");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateFromCartAsync(pricing, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheCheckout_BillsEveryLineWithItsQuantityAndTaxDetails()
    {
        var order = await CreateService().CreateFromCartAsync(CreatePricing(CreateCart(("mug", 3, 12m))), TestContext.Current.CancellationToken);
        var session = new CheckoutSession { SessionId = "session-1", ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = order.ItemId };

        await CreateHandler().ActivatingAsync(new CheckoutFlowActivatingContext(session));

        var item = Assert.Single(Assert.Single(session.Steps).BillingItems);

        Assert.Equal(12m, item.Amount);
        Assert.Equal(3, item.Quantity);
        Assert.Equal("SKU-mug", item.Sku);
        Assert.Equal("books", item.TaxCategoryCode);
        Assert.Equal(TaxableItemKind.Physical, item.TaxableKind);
        Assert.Equal("USD", session.Currency);
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        Assert.Equal("session-1", order.CheckoutSessionId);
    }

    [Fact]
    public async Task CompletingTheCheckout_MarksTheOrderPaid_AndEmptiesTheCart()
    {
        var cart = CreateCart(("mug", 2, 12m));
        _carts.Carts.Add(cart);

        var order = await CreateService().CreateFromCartAsync(CreatePricing(cart), TestContext.Current.CancellationToken);
        var session = await StartCheckoutAsync(order);

        session.Put(new CheckoutContactInfo { DisplayName = "Avery", Email = "avery@example.com" });
        session.Put(new CheckoutInvoice { Currency = "USD", GrandTotal = 25.98m, TaxAmount = 1.98m, LineItems = [new CheckoutLineItem { ItemId = "mug", Quantity = 2, UnitPrice = 12m }] });

        await CreateHandler().CompletingAsync(new CheckoutFlowCompletingContext(new CheckoutFlow(session)));

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(_now, order.PaidUtc);
        Assert.Equal(25.98m, order.Total);
        Assert.Equal(1.98m, order.TaxAmount);
        Assert.Equal("avery@example.com", order.CustomerEmail);
        Assert.Empty(_carts.Carts);
    }

    [Fact]
    public async Task CompletingTheCheckout_KeepsWhatWasAddedToTheCartAfterward()
    {
        var cart = CreateCart(("mug", 2, 12m));
        _carts.Carts.Add(cart);

        var order = await CreateService().CreateFromCartAsync(CreatePricing(cart), TestContext.Current.CancellationToken);
        var session = await StartCheckoutAsync(order);

        cart.Lines[0].Quantity = 3;
        cart.Lines.Add(new CartLine { LineId = "late", ContentItemId = "lamp", Quantity = 1 });

        session.Put(new CheckoutInvoice { Currency = "USD", GrandTotal = 24m, LineItems = [new CheckoutLineItem { ItemId = "mug", Quantity = 2, UnitPrice = 12m }] });

        await CreateHandler().CompletingAsync(new CheckoutFlowCompletingContext(new CheckoutFlow(session)));

        var remaining = Assert.Single(_carts.Carts);
        Assert.Equal(1, remaining.Lines.Single(line => line.ContentItemId == "mug").Quantity);
        Assert.Contains(remaining.Lines, line => line.ContentItemId == "lamp");
    }

    [Fact]
    public async Task ACheckoutThatBillsNothing_IsRefused()
    {
        var order = await CreateService().CreateFromCartAsync(CreatePricing(CreateCart(("mug", 1, 12m))), TestContext.Current.CancellationToken);
        var session = new CheckoutSession { SessionId = "session-1", ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = order.ItemId };

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().CompletingAsync(new CheckoutFlowCompletingContext(new CheckoutFlow(session))));
        Assert.NotEqual(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task AReplacedOrder_IsStillPaid_WhenItsCheckoutCompletes()
    {
        var cart = CreateCart(("mug", 1, 12m));
        var service = CreateService();

        var first = await service.CreateFromCartAsync(CreatePricing(cart), TestContext.Current.CancellationToken);
        var session = await StartCheckoutAsync(first);
        await service.CreateFromCartAsync(CreatePricing(cart), TestContext.Current.CancellationToken);

        session.Put(new CheckoutInvoice { Currency = "USD", GrandTotal = 12m, LineItems = [new CheckoutLineItem { ItemId = "mug", Quantity = 1, UnitPrice = 12m }] });

        await CreateHandler().CompletingAsync(new CheckoutFlowCompletingContext(new CheckoutFlow(session)));

        Assert.Equal(OrderStatus.Paid, first.Status);
    }

    private async Task<CheckoutSession> StartCheckoutAsync(Order order)
    {
        var session = new CheckoutSession { SessionId = "session-" + order.ItemId, ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = order.ItemId };

        await CreateHandler().ActivatingAsync(new CheckoutFlowActivatingContext(session));

        return session;
    }

    private static Cart CreateCart(params (string Id, int Quantity, decimal UnitPrice)[] lines)
        => new()
        {
            ItemId = "cart-1",
            Currency = "USD",
            Lines = [.. lines.Select(line => new CartLine { LineId = line.Id, ContentItemId = line.Id, Quantity = line.Quantity, AddedUnitPrice = line.UnitPrice })],
        };

    private static CartPricing CreatePricing(Cart cart, string unavailable = null)
        => new()
        {
            Cart = cart,
            Currency = cart.Currency,
            Lines = [.. cart.Lines.Select(line => new CartLinePricing
            {
                Line = line,
                ContentItem = new ContentItem { ContentItemId = line.ContentItemId, ContentItemVersionId = line.ContentItemId + "-v1", ContentType = "Product" },
                Product = new SellableProduct
                {
                    ContentItemId = line.ContentItemId,
                    ContentItemVersionId = line.ContentItemId + "-v1",
                    ContentType = "Product",
                    Title = line.ContentItemId,
                    Sku = "SKU-" + line.ContentItemId,
                    Currency = cart.Currency,
                    UnitPrice = line.AddedUnitPrice,
                    ProductType = ProductType.Good,
                    TaxCategoryCode = "books",
                },
                Price = new PriceResult(line.AddedUnitPrice, cart.Currency, line.Quantity),
                Title = line.ContentItemId,
                UnitPrice = line.AddedUnitPrice,
                LineTotal = line.AddedUnitPrice * line.Quantity,
                MaxQuantity = 99,
                Problem = line.ContentItemId == unavailable ? new Microsoft.Extensions.Localization.LocalizedString("gone", "gone") : null,
            })],
        };

    private OrderService CreateService()
        => new(_orders, Clock());

    private OrderCheckoutHandler CreateHandler()
        => new(_orders, _carts, Clock(), NullLogger<OrderCheckoutHandler>.Instance, new PassThroughStringLocalizer<OrderCheckoutHandler>());

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(_now);

        return clock.Object;
    }

    private sealed class FakeOrderStore : IOrderStore
    {
        public List<Order> Orders { get; } = [];

        public ValueTask<Order> FindByIdAsync(string orderId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Orders.FirstOrDefault(order => order.ItemId == orderId));

        public Task<Order> FindByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(Orders.FirstOrDefault(order => order.OrderNumber == orderNumber));

        public Task<IReadOnlyList<Order>> ListUnpaidByCartAsync(string cartId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Order>>(Orders.Where(order => order.CartId == cartId && order.Status is OrderStatus.Draft or OrderStatus.AwaitingPayment).ToArray());

        public Task<(int Count, IReadOnlyList<Order> Orders)> PageAsync(OrderQuery query, int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<(int, IReadOnlyList<Order>)>((Orders.Count, Orders.Skip(skip).Take(take).ToArray()));

        public ValueTask CreateAsync(Order order, CancellationToken cancellationToken = default)
        {
            Orders.Add(order);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(Order order, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class FakeCartStore : ICartStore
    {
        public List<Cart> Carts { get; } = [];

        public ValueTask<Cart> FindByIdAsync(string cartId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Carts.FirstOrDefault(cart => cart.ItemId == cartId));

        public Task<Cart> FindByUserAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<Cart>(null);

        public Task<Cart> FindByGuestTokenHashAsync(string guestTokenHash, CancellationToken cancellationToken = default)
            => Task.FromResult<Cart>(null);

        public Task<IReadOnlyList<Cart>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Cart>>([]);

        public ValueTask CreateAsync(Cart cart, CancellationToken cancellationToken = default)
        {
            Carts.Add(cart);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(Cart cart, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<bool> DeleteAsync(Cart cart, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Carts.Remove(cart));
    }
}
