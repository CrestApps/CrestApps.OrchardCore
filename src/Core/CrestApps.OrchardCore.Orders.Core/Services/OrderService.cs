using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Turns a cart into an order.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Freezes a priced cart into a draft order. Orders made from the same cart earlier and not paid are canceled,
    /// so the customer pays for what the cart holds now. Every <see cref="IOrderEventHandler"/> then hears of the new
    /// order; when one refuses it, such as for want of stock, the order is canceled and the result says why.
    /// </summary>
    /// <param name="pricing">The cart, priced now. Every line must be available.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<OrderCreationResult> CreateFromCartAsync(CartPricing pricing, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an order from a priced cart, letting the caller set it up (its channel and location, say) before its
    /// handlers hear of it, so stock is held where it's sold.
    /// </summary>
    Task<OrderCreationResult> CreateFromCartAsync(CartPricing pricing, Action<Order> configure, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an order that was not paid, records why, and tells every <see cref="IOrderEventHandler"/>. A paid or
    /// already canceled order is left as it is.
    /// </summary>
    /// <returns><see langword="true"/> when the order was canceled now.</returns>
    Task<bool> CancelAsync(Order order, OrderCancelReason reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the order. Only <see cref="OrderCancelReason.CanceledByStore"/> cancels a paid order; with
    /// <paramref name="restock"/>, what it sold goes back into stock.
    /// </summary>
    Task<bool> CancelAsync(Order order, OrderCancelReason reason, bool restock, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records money given back for the order, once per refund, and tells the order handlers.
    /// </summary>
    /// <returns><see langword="false"/> when the refund was recorded already.</returns>
    Task<bool> RecordRefundAsync(Order order, OrderRefund refund, bool restock, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a note to the order.
    /// </summary>
    Task AddNoteAsync(Order order, string text, OrderNoteKind kind, string authorName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the lines of an order not paid or checked out yet with the cart's, such as a store's draft being
    /// changed. The orders' handlers hear of it, and one that can't provide the new lines, such as stock running short,
    /// refuses them: nothing changes and the problems are returned.
    /// </summary>
    Task<IReadOnlyList<string>> ReplaceLinesAsync(Order order, CartPricing pricing, CancellationToken cancellationToken = default);
}

public sealed class OrderService : IOrderService
{
    // Crockford's base 32, without the letters that read as digits.
    private const string NumberAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private readonly IOrderStore _store;
    private readonly IEnumerable<IOrderEventHandler> _handlers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public OrderService(IOrderStore store, IEnumerable<IOrderEventHandler> handlers, IClock clock, ILogger<OrderService> logger)
    {
        _store = store;
        _handlers = handlers;
        _clock = clock;
        _logger = logger;
    }

    public Task<OrderCreationResult> CreateFromCartAsync(CartPricing pricing, CancellationToken cancellationToken = default)
        => CreateFromCartAsync(pricing, configure: null, cancellationToken);

    public async Task<OrderCreationResult> CreateFromCartAsync(CartPricing pricing, Action<Order> configure, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        if (pricing.Cart is null || !pricing.CanCheckout)
        {
            throw new InvalidOperationException("Only a cart whose every line can be bought becomes an order.");
        }

        var now = _clock.UtcNow;

        foreach (var earlier in await _store.ListUnpaidByCartAsync(pricing.Cart.ItemId, cancellationToken))
        {
            await CancelAsync(earlier, OrderCancelReason.Replaced, cancellationToken);
        }

        var order = new Order
        {
            ItemId = UniqueId.GenerateId(),
            OrderNumber = await NewOrderNumberAsync(now, cancellationToken),
            UserId = pricing.Cart.UserId,
            CartId = pricing.Cart.ItemId,
            Currency = pricing.Currency,
            Status = OrderStatus.Draft,
            CreatedUtc = now,
            ModifiedUtc = now,
            Lines = OrderLines.FromPricing(pricing),
        };

        order.Subtotal = order.Lines.Sum(line => line.LineTotal);
        order.Channel = OrderChannels.Online;

        configure?.Invoke(order);

        await _store.CreateAsync(order, cancellationToken);

        var created = new OrderCreatedContext(order);

        foreach (var handler in _handlers)
        {
            await handler.CreatedAsync(created, cancellationToken);
        }

        if (created.Problems.Count > 0)
        {
            await CancelAsync(order, OrderCancelReason.Unavailable, cancellationToken);
        }

        return new OrderCreationResult(order, created.Problems);
    }

    public async Task<IReadOnlyList<string>> ReplaceLinesAsync(Order order, CartPricing pricing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(pricing);

        if (order.Status != OrderStatus.Draft)
        {
            throw new InvalidOperationException("Only an order not checked out yet can have its lines replaced.");
        }

        if (!pricing.CanCheckout)
        {
            throw new InvalidOperationException("Only a cart whose every line can be bought replaces an order's lines.");
        }

        var previous = order.Lines;

        order.Lines = OrderLines.FromPricing(pricing);
        order.Currency = pricing.Currency;
        order.Subtotal = order.Lines.Sum(line => line.LineTotal);

        var changed = new OrderLinesChangedContext(order, previous);

        foreach (var handler in _handlers)
        {
            await handler.LinesChangedAsync(changed, cancellationToken);
        }

        if (changed.Problems.Count > 0)
        {
            order.Lines = previous;
            order.Subtotal = previous.Sum(line => line.LineTotal);

            return changed.Problems;
        }

        order.ModifiedUtc = _clock.UtcNow;
        await _store.UpdateAsync(order, cancellationToken);

        return [];
    }

    public async Task<bool> CancelAsync(Order order, OrderCancelReason reason, CancellationToken cancellationToken = default)
        => await CancelAsync(order, reason, restock: false, cancellationToken);

    public async Task<bool> CancelAsync(Order order, OrderCancelReason reason, bool restock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        // Only the store cancels a paid order: a checkout that ends never touches an order it already paid for.
        if (order.Status == OrderStatus.Canceled ||
            (order.Status == OrderStatus.Paid && reason != OrderCancelReason.CanceledByStore))
        {
            return false;
        }

        var now = _clock.UtcNow;

        order.Status = OrderStatus.Canceled;
        order.CancelReason = reason;
        order.CanceledUtc = now;
        order.ModifiedUtc = now;

        await _store.UpdateAsync(order, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Order '{OrderNumber}' was canceled: {Reason}.", order.OrderNumber, reason);
        }

        var context = new OrderCanceledContext(order, reason) { Restock = restock };

        foreach (var handler in _handlers)
        {
            await handler.CanceledAsync(context, cancellationToken);
        }

        return true;
    }

    public async Task<bool> RecordRefundAsync(Order order, OrderRefund refund, bool restock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(refund);

        order.Refunds ??= [];

        // A refund is reported again when it is reconciled; it counts once.
        if (!string.IsNullOrEmpty(refund.RefundId) && order.Refunds.Any(existing => string.Equals(existing.RefundId, refund.RefundId, StringComparison.Ordinal)))
        {
            return false;
        }

        order.Refunds.Add(refund);
        order.ModifiedUtc = _clock.UtcNow;

        await _store.UpdateAsync(order, cancellationToken);

        var context = new OrderRefundedContext(order, refund)
        {
            // Stock goes back only once everything was given back: after a partial refund the customer keeps the goods.
            Restock = restock && order.RefundStatus == OrderRefundStatus.Refunded,
        };

        foreach (var handler in _handlers)
        {
            await handler.RefundedAsync(context, cancellationToken);
        }

        return true;
    }

    public async Task AddNoteAsync(Order order, string text, OrderNoteKind kind, string authorName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var now = _clock.UtcNow;

        order.Notes ??= [];
        order.Notes.Add(new OrderNote
        {
            Id = UniqueId.GenerateId(),
            Text = text.Trim(),
            Kind = kind,
            AuthorName = string.IsNullOrWhiteSpace(authorName) ? null : authorName.Trim(),
            CreatedUtc = now,
        });
        order.ModifiedUtc = now;

        await _store.UpdateAsync(order, cancellationToken);
    }

    /// <summary>
    /// A number a customer can read out on the phone: the date, then six random characters, such as
    /// <c>261013-7F3K2Q</c>.
    /// </summary>
    internal static string FormatOrderNumber(DateTime utc, ReadOnlySpan<byte> random)
    {
        Span<char> suffix = stackalloc char[6];

        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = NumberAlphabet[random[i] % NumberAlphabet.Length];
        }

        return $"{utc:yyMMdd}-{suffix}";
    }

    private async Task<string> NewOrderNumberAsync(DateTime now, CancellationToken cancellationToken)
    {
        // A billion numbers a day makes a clash unlikely; checking makes it impossible in practice.
        for (var attempt = 0; ; attempt++)
        {
            var number = FormatOrderNumber(now, RandomNumberGenerator.GetBytes(6));

            if (attempt >= 4 || await _store.FindByNumberAsync(number, cancellationToken) is null)
            {
                return number;
            }
        }
    }
}
