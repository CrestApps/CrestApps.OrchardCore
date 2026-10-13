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
    private readonly ICartStore _cartStore;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public OrderCheckoutHandler(
        IOrderStore orderStore,
        ICartStore cartStore,
        IClock clock,
        ILogger<OrderCheckoutHandler> logger,
        IStringLocalizer<OrderCheckoutHandler> stringLocalizer)
    {
        _orderStore = orderStore;
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

        // An order replaced by a newer one from the same cart is still what this payment bought.
        order.Status = OrderStatus.Paid;
        order.PaidUtc = now;
        order.ModifiedUtc = now;
        order.CheckoutSessionId = session.SessionId;
        order.Total = invoice.GrandTotal;
        order.TaxAmount = invoice.TaxAmount;

        if (global::OrchardCore.Entities.EntityExtensions.TryGet<CheckoutContactInfo>(session, out var contact))
        {
            order.CustomerName ??= contact.DisplayName;
            order.CustomerEmail ??= contact.Email;
        }

        await _orderStore.UpdateAsync(order);
        await RemoveBoughtLinesAsync(order);
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
            var line = cart.Lines.FirstOrDefault(candidate =>
                candidate.ContentItemId == bought.ContentItemId &&
                string.Equals(candidate.PriceId, bought.PriceId, StringComparison.Ordinal));

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

    private static bool IsOrder(CheckoutSession session)
        => string.Equals(session?.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal);

    private static BillingItem ToBillingItem(OrderLine line)
        => new()
        {
            ItemId = line.LineId,
            Description = string.IsNullOrEmpty(line.PriceName) ? line.Title : $"{line.Title} ({line.PriceName})",
            Amount = line.UnitPrice,
            Quantity = line.Quantity,
            PriceId = line.PriceId,
            Sku = line.Sku,
            TaxCategoryCode = line.TaxCategoryCode,
            TaxClassificationCode = line.TaxClassificationCode,
            TaxableKind = line.ProductType switch
            {
                ProductType.Service => TaxableItemKind.Service,
                ProductType.Digital => TaxableItemKind.Digital,
                ProductType.Good => TaxableItemKind.Physical,
                _ => null,
            },
        };
}
