using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Core.Handlers;

/// <summary>
/// Bills an order through the checkout, and marks it paid when the checkout completes.
/// </summary>
/// <remarks>
/// The lines billed are the order's frozen lines, not the cart's, so the customer pays exactly what they saw when
/// they checked out. Each line carries its quantity and its product's tax details.
/// </remarks>
public sealed class OrderCheckoutHandler : CheckoutHandlerBase
{
    private readonly IOrderStore _orderStore;
    private readonly IOrderService _orderService;
    private readonly IEnumerable<IOrderEventHandler> _orderHandlers;
    private readonly ICartStore _cartStore;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public OrderCheckoutHandler(
        IOrderStore orderStore,
        IOrderService orderService,
        IEnumerable<IOrderEventHandler> orderHandlers,
        ICartStore cartStore,
        IClock clock,
        ILogger<OrderCheckoutHandler> logger,
        IStringLocalizer<OrderCheckoutHandler> stringLocalizer)
    {
        _orderStore = orderStore;
        _orderService = orderService;
        _orderHandlers = orderHandlers;
        _cartStore = cartStore;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        var session = context.Session;

        if (!IsOrder(session) || string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var order = await _orderStore.FindByIdAsync(session.ReferenceId);

        if (order is null || order.Status == OrderStatus.Paid)
        {
            // Nothing is billed, and completing refuses the checkout.
            _logger.LogWarning("Checkout session '{SessionId}' was started for order '{OrderId}', which is missing or already paid.", session.SessionId, session.ReferenceId);

            return;
        }

        session.Currency = order.Currency;

        // What the buyer knows the order by, for a client that can't read the order itself.
        global::OrchardCore.Entities.EntityExtensions.Put(session, new Checkout.Models.CheckoutReferenceNumber { Number = order.OrderNumber });

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = OrderConstants.CheckoutStepKey,
            Title = S["Order"],
            Order = 0,
            Conceal = true,
            BillingItems = [.. order.Lines.Select(ToBillingItem)],
        });

        order.Status = OrderStatus.AwaitingPayment;
        order.CheckoutSessionId = session.SessionId;
        order.ModifiedUtc = _clock.UtcNow;

        // An order the store made for a buyer already knows who they are, so they needn't type it again.
        if (order.Invoice is not null && !string.IsNullOrEmpty(order.CustomerEmail) &&
            !global::OrchardCore.Entities.EntityExtensions.TryGet<CheckoutContactInfo>(session, out _))
        {
            global::OrchardCore.Entities.EntityExtensions.Put(session, new CheckoutContactInfo
            {
                DisplayName = order.CustomerName,
                Email = order.CustomerEmail,
                Phone = order.CustomerPhone,
            });
        }

        await _orderStore.UpdateAsync(order);
    }

    public override Task InitializingAsync(CheckoutFlowInitializingContext context)
    {
        // Concealment is decided per request, so the order step is hidden again every time the session loads. It
        // has nothing to ask; it carries the order's charges.
        foreach (var step in context.Flow.Session.Steps)
        {
            if (string.Equals(step.Key, OrderConstants.CheckoutStepKey, StringComparison.Ordinal))
            {
                step.Conceal = true;
            }
        }

        return Task.CompletedTask;
    }

    public override async Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        if (context.Flow.Session is not CheckoutSession session || !IsOrder(session))
        {
            return;
        }

        // Refusing rolls the completion back, so the payment is checked again rather than recorded against nothing.
        var order = await _orderStore.FindByIdAsync(session.ReferenceId)
            ?? throw new InvalidOperationException($"Checkout '{session.SessionId}' pays for order '{session.ReferenceId}', which does not exist.");

        if (order.Status == OrderStatus.Paid)
        {
            return;
        }

        if (!global::OrchardCore.Entities.EntityExtensions.TryGet<CheckoutInvoice>(session, out var invoice) || invoice.LineItems is not { Length: > 0 })
        {
            throw new InvalidOperationException($"Checkout '{session.SessionId}' for order '{order.OrderNumber}' bills nothing, so it cannot be completed.");
        }

        var now = _clock.UtcNow;

        // An order bump the buyer added on the payment step, and paid for, becomes a line of the order, so it's
        // sold from stock, shipped and reported like the rest.
        if (global::OrchardCore.Entities.EntityExtensions.TryGet<CheckoutOrderBump>(session, out var bump) &&
            invoice.LineItems.Any(item => item.ItemId == CheckoutOrderBump.BillingItemId) &&
            !order.Lines.Exists(line => line.LineId == CheckoutOrderBump.BillingItemId))
        {
            order.Lines.Add(new OrderLine
            {
                LineId = CheckoutOrderBump.BillingItemId,
                ContentItemId = bump.ContentItemId,
                ContentItemVersionId = bump.ContentItemVersionId,
                ContentType = bump.ContentType,
                Title = bump.Title,
                Sku = bump.Sku,
                Quantity = 1,
                UnitPrice = bump.UnitPrice,
                LineTotal = bump.UnitPrice,
                ProductType = bump.ProductType,
                TaxCategoryCode = bump.TaxCategoryCode,
                TaxClassificationCode = bump.TaxClassificationCode,
                CategoryIds = bump.CategoryIds ?? [],
            });
            order.Subtotal += bump.UnitPrice;
        }

        // An order replaced by a newer one from the same cart is still what this payment bought.
        order.Status = OrderStatus.Paid;
        order.PaidUtc = now;
        order.ModifiedUtc = now;
        order.CheckoutSessionId = session.SessionId;
        order.Total = invoice.GrandTotal;
        order.TaxAmount = invoice.TaxAmount;
        order.TaxLines = [.. invoice.TaxLines ?? []];

        // What the discounts took off, so the order's lines, shipping, discounts and tax add up to what was paid.
        order.Discounts = [.. (invoice.Discounts ?? [])
            .Where(discount => discount.Amount > 0m)
            .Select(discount => new OrderDiscount
            {
                Code = discount.IsAutomatic ? null : discount.Code,
                Description = discount.Description,
                Amount = discount.Amount,
                ProviderKey = discount.ProviderKey,
                Target = discount.Target,
            })];

        // What a gift card or store credit paid toward the total, so the order shows how it was paid.
        order.Tenders = [.. (invoice.Tenders ?? [])
            .Where(tender => tender.Amount > 0m)
            .Select(tender => new OrderTender
            {
                ProviderKey = tender.ProviderKey,
                Reference = tender.Reference,
                Description = tender.Description,
                Amount = tender.Amount,
            })];

        // What the buyer entered at checkout is who this order is for, even when they changed a signed-in account's
        // name or email for it.
        if (global::OrchardCore.Entities.EntityExtensions.TryGet<CheckoutContactInfo>(session, out var contact))
        {
            order.CustomerName = Prefer(contact.DisplayName, order.CustomerName);
            order.CustomerEmail = Prefer(contact.Email, order.CustomerEmail);
            order.CustomerPhone = Prefer(contact.Phone, order.CustomerPhone);
            order.BillingAddress = contact.BillingAddress?.Clone() ?? order.BillingAddress;
        }

        if (global::OrchardCore.Entities.EntityExtensions.TryGet<CheckoutShippingInfo>(session, out var shipping) && shipping.Address is not null)
        {
            order.ShippingAddress = shipping.Address.Clone();
            order.Shipping = new OrderShipping
            {
                MethodId = shipping.MethodId,
                RateId = shipping.RateId,
                ZoneId = shipping.ZoneId,
                ProviderName = shipping.ProviderName,
                Name = shipping.Name,
                Description = shipping.Description,
                Amount = shipping.Amount,
                LocationId = shipping.LocationId,
            };
        }

        await _orderStore.UpdateAsync(order);

        // In the same unit of work: a handler that can't record the sale, such as one that can't sell the stock just
        // now, rolls the completion back and the checkout retries it.
        var paid = new OrderPaidContext(order);

        foreach (var handler in _orderHandlers)
        {
            await handler.PaidAsync(paid);
        }

        await RemoveBoughtLinesAsync(order);
    }

    public override Task FailedAsync(CheckoutFlowFailedContext context)
        => CancelOrderAsync(context.Flow.Session as CheckoutSession, OrderCancelReason.PaymentFailed);

    public override Task CanceledAsync(CheckoutFlowCanceledContext context)
        => CancelOrderAsync(context.Flow.Session as CheckoutSession, OrderCancelReason.CheckoutCanceled);

    public override Task ExpiredAsync(CheckoutFlowExpiredContext context)
        => CancelOrderAsync(context.Flow.Session as CheckoutSession, OrderCancelReason.CheckoutExpired);

    // A checkout that can no longer be paid ends its order, so what the order held is let go. An order since paid
    // through another checkout, or moved to one, is not this checkout's to cancel.
    private async Task CancelOrderAsync(CheckoutSession session, OrderCancelReason reason)
    {
        if (!IsOrder(session) || string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var order = await _orderStore.FindByIdAsync(session.ReferenceId);

        if (order is null || !string.Equals(order.CheckoutSessionId, session.SessionId, StringComparison.Ordinal))
        {
            return;
        }

        // An order the store sent the buyer to pay stays open: the buyer can pay it again from its link.
        if (order.Invoice is not null && order.Status == OrderStatus.AwaitingPayment)
        {
            order.Status = OrderStatus.Draft;
            order.CheckoutSessionId = null;
            await _orderStore.UpdateAsync(order);

            return;
        }

        await _orderService.CancelAsync(order, reason);
    }

    /// <summary>
    /// Takes what was bought out of the cart. Anything added to the cart after checking out stays in it.
    /// </summary>
    private async Task RemoveBoughtLinesAsync(Order order)
    {
        var cart = string.IsNullOrEmpty(order.CartId) ? null : await _cartStore.FindByIdAsync(order.CartId);

        if (cart is null)
        {
            return;
        }

        foreach (var bought in order.Lines)
        {
            // The variant matters: buying the blue mug leaves the red one in the cart.
            var line = cart.Lines.FirstOrDefault(candidate =>
                candidate.ContentItemId == bought.ContentItemId &&
                string.Equals(candidate.PriceId, bought.PriceId, StringComparison.Ordinal) &&
                string.Equals(candidate.VariantId, bought.VariantId, StringComparison.Ordinal));

            if (line is null)
            {
                continue;
            }

            line.Quantity -= bought.Quantity;

            if (line.Quantity <= 0)
            {
                cart.Lines.Remove(line);
            }
        }

        if (cart.Lines.Count == 0)
        {
            await _cartStore.DeleteAsync(cart);

            return;
        }

        cart.ModifiedUtc = _clock.UtcNow;

        await _cartStore.UpdateAsync(cart);
    }

    private static string Prefer(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static bool IsOrder(CheckoutSession session)
        => string.Equals(session?.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal);

    /// <summary>
    /// The billing line of an order bump, as the order's lines are billed.
    /// </summary>
    public static BillingItem ToBillingItem(CheckoutOrderBump bump)
        => ToBillingItem(new OrderLine
        {
            LineId = CheckoutOrderBump.BillingItemId,
            ContentItemId = bump.ContentItemId,
            Title = bump.Title,
            Sku = bump.Sku,
            Quantity = 1,
            UnitPrice = bump.UnitPrice,
            LineTotal = bump.UnitPrice,
            ProductType = bump.ProductType,
            TaxCategoryCode = bump.TaxCategoryCode,
            TaxClassificationCode = bump.TaxClassificationCode,
            CategoryIds = bump.CategoryIds ?? [],
        });

    private static BillingItem ToBillingItem(OrderLine line)
        => new()
        {
            ItemId = line.LineId,
            Description = OrderLineText.Describe(line),
            Amount = line.UnitPrice,
            Quantity = line.Quantity,
            PriceId = line.PriceId,
            Sku = line.Sku,
            TaxCategoryCode = line.TaxCategoryCode,
            TaxClassificationCode = line.TaxClassificationCode,
            ProductId = line.ContentItemId,
            CategoryIds = line.CategoryIds,
            TaxableKind = line.ProductType switch
            {
                ProductType.Service => TaxableItemKind.Service,
                ProductType.Digital => TaxableItemKind.Digital,
                ProductType.Good => TaxableItemKind.Physical,
                _ => null,
            },
        };
}
