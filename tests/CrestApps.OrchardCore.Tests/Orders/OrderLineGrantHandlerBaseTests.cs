using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// A line grant is made from the product version that was bought, once per order and line, and taken back line by line
/// when the order is refunded in full.
/// </summary>
public sealed class OrderLineGrantHandlerBaseTests
{
    [Fact]
    public async Task AGrant_UsesThePurchasedVersion_AfterTheProductIsEdited()
    {
        // Arrange: the product was edited (and has a draft) after the order bought version v1.
        var handler = new RecordingHandler(ContentManager());
        var order = Order(new OrderLine { LineId = "l1", ContentItemId = "p1", ContentItemVersionId = "v1", Quantity = 1 });

        // Act
        await handler.PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["l1:bought"], handler.Granted);
    }

    [Fact]
    public async Task ALineWithoutAVersion_UsesThePublishedProduct()
    {
        var handler = new RecordingHandler(ContentManager());
        var order = Order(new OrderLine { LineId = "l1", ContentItemId = "p1", Quantity = 1 });

        await handler.PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken);

        Assert.Equal(["l1:published"], handler.Granted);
    }

    [Fact]
    public async Task APaymentConfirmedTwice_GrantsEachLineOnce_AndSkipsLinesWithoutThePart()
    {
        var handler = new RecordingHandler(ContentManager());
        var order = Order(
            new OrderLine { LineId = "l1", ContentItemId = "p1", ContentItemVersionId = "v1", Quantity = 1 },
            new OrderLine { LineId = "l2", ContentItemId = "plain", ContentItemVersionId = "plain-v1", Quantity = 1 });

        await handler.PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken);
        await handler.PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken);

        Assert.Equal(["l1:bought"], handler.Granted);
        Assert.Equal([1, 0], handler.GrantedCounts);
    }

    [Fact]
    public async Task AFullRefund_RevokesEachLine_AndAPartialOneRevokesNothing()
    {
        var handler = new RecordingHandler(ContentManager());
        var order = Order(new OrderLine { LineId = "l1", ContentItemId = "p1", ContentItemVersionId = "v1", Quantity = 1 });

        order.Refunds.Add(new OrderRefund { RefundId = "r1", Amount = 4m });
        await handler.RefundedAsync(new OrderRefundedContext(order, order.Refunds[0]), TestContext.Current.CancellationToken);

        Assert.Empty(handler.Revoked);

        order.Refunds.Add(new OrderRefund { RefundId = "r2", Amount = 6m });
        await handler.RefundedAsync(new OrderRefundedContext(order, order.Refunds[1]), TestContext.Current.CancellationToken);

        Assert.Equal(["l1:Refunded"], handler.Revoked);
    }

    /// <summary>
    /// SEC-C5: an order placed with a payment method that defers payment (Pay Later, bank transfer) used to be granted at
    /// once, so a buyer could take a download or a membership and never pay. Its grants are held until the payment
    /// settles, unless the product is set to be delivered before payment.
    /// </summary>
    [Fact]
    public async Task ADeferredPayment_GrantsNothingUntilItSettles()
    {
        var handler = new RecordingHandler(ContentManager());
        var order = Order(new OrderLine { LineId = "l1", ContentItemId = "p1", ContentItemVersionId = "v1", Quantity = 1 });
        order.PaymentDeferred = true;

        await handler.PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken);

        Assert.Empty(handler.Granted);

        order.PaymentSettledUtc = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
        await handler.PaymentSettledAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken);

        Assert.Equal(["l1:bought"], handler.Granted);
    }

    [Fact]
    public async Task ADeferredPayment_StillGrantsAProductDeliveredBeforePayment()
    {
        var handler = new RecordingHandler(ContentManager(deliverBeforePayment: true));
        var order = Order(new OrderLine { LineId = "l1", ContentItemId = "p1", ContentItemVersionId = "v1", Quantity = 1 });
        order.PaymentDeferred = true;

        await handler.PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken);

        Assert.Equal(["l1:bought"], handler.Granted);
    }

    private static Order Order(params OrderLine[] lines)
        => new()
        {
            ItemId = "o1",
            Status = OrderStatus.Paid,
            Currency = "USD",
            Total = 10m,
            Lines = [.. lines],
        };

    private static IContentManager ContentManager(bool deliverBeforePayment = false)
    {
        var bought = Item("p1", "v1", "bought");

        if (deliverBeforePayment)
        {
            bought.Weld(new CrestApps.OrchardCore.Products.Core.Models.ProductPart { DeliverBeforePayment = true });
        }

        var edited = Item("p1", "v2", "edited");
        var published = Item("p1", "v2", "published");

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetVersionAsync("v1")).ReturnsAsync(bought);
        contentManager.Setup(manager => manager.GetVersionAsync("plain-v1")).ReturnsAsync(new ContentItem { ContentItemId = "plain", ContentItemVersionId = "plain-v1" });
        contentManager.Setup(manager => manager.GetAsync("p1", It.Is<VersionOptions>(options => options.IsLatest))).ReturnsAsync(edited);
        contentManager.Setup(manager => manager.GetAsync("p1", It.Is<VersionOptions>(options => options.IsPublished))).ReturnsAsync(published);

        return contentManager.Object;
    }

    private static ContentItem Item(string id, string versionId, string label)
    {
        var item = new ContentItem { ContentItemId = id, ContentItemVersionId = versionId };

        item.Weld(new GrantPart { Label = label });

        return item;
    }

    private sealed class GrantPart : ContentPart
    {
        public string Label { get; set; }
    }

    private sealed class RecordingHandler : OrderLineGrantHandlerBase<GrantPart>
    {
        public RecordingHandler(IContentManager contentManager)
            : base(contentManager)
        {
        }

        public List<string> Granted { get; } = [];

        public List<int> GrantedCounts { get; } = [];

        public List<string> Revoked { get; } = [];

        private readonly HashSet<string> _grantedLines = new(StringComparer.Ordinal);

        protected override Task<ISet<string>> GetGrantedLineIdsAsync(Order order, CancellationToken cancellationToken)
            => Task.FromResult<ISet<string>>(new HashSet<string>(_grantedLines, StringComparer.Ordinal));

        protected override Task<bool> GrantLineAsync(OrderLineGrantContext<GrantPart> context, CancellationToken cancellationToken)
        {
            Granted.Add($"{context.Line.LineId}:{context.Part.Label}");
            _grantedLines.Add(context.Line.LineId);

            return Task.FromResult(true);
        }

        protected override Task RevokeLineAsync(Order order, OrderLine line, OrderRevokeReason reason, CancellationToken cancellationToken)
        {
            Revoked.Add($"{line.LineId}:{reason}");

            return Task.CompletedTask;
        }

        protected override Task GrantedAsync(Order order, int granted, CancellationToken cancellationToken)
        {
            GrantedCounts.Add(granted);

            return Task.CompletedTask;
        }
    }
}
