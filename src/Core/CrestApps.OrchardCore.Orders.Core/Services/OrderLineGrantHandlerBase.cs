using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// An <see cref="OrderGrantHandlerBase"/> that grants line by line: each line whose purchased product has
/// <typeparamref name="TPart"/>. It reads the product through <see cref="OrderLine.ContentItemVersionId"/>, so a grant is
/// what was bought even after the product is edited, and it skips the lines already granted, so a payment confirmed
/// twice grants each line once.
/// </summary>
/// <typeparam name="TPart">The part that makes a product grant something, such as digital files or a membership.</typeparam>
public abstract class OrderLineGrantHandlerBase<TPart> : OrderGrantHandlerBase
    where TPart : ContentPart
{
    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderLineGrantHandlerBase{TPart}"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager that reads the purchased products.</param>
    protected OrderLineGrantHandlerBase(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    /// <inheritdoc />
    protected sealed override async Task GrantAsync(Order order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.Lines is not { Count: > 0 } || !await CanGrantAsync(order, cancellationToken))
        {
            return;
        }

        var granted = await GetGrantedLineIdsAsync(order, cancellationToken) ?? new HashSet<string>(StringComparer.Ordinal);
        var count = 0;

        foreach (var line in order.Lines)
        {
            if (line is null || line.Quantity <= 0 || (line.LineId is not null && granted.Contains(line.LineId)))
            {
                continue;
            }

            var item = await _contentManager.GetPurchasedAsync(line);

            if (item is null || !item.TryGet<TPart>(out var part))
            {
                continue;
            }

            // Paid by a promise (Pay Later, a bank transfer): what delivers instantly waits for the money, unless the
            // product is set to be delivered before payment.
            if (!order.IsPaymentCollected && !(item.TryGet<ProductPart>(out var product) && product.DeliverBeforePayment))
            {
                continue;
            }

            if (await GrantLineAsync(new OrderLineGrantContext<TPart>(order, line, item, part), cancellationToken))
            {
                count++;
            }
        }

        await GrantedAsync(order, count, cancellationToken);
    }

    /// <inheritdoc />
    protected sealed override async Task RevokeAsync(Order order, OrderRevokeReason reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        foreach (var line in order.Lines ?? [])
        {
            if (line is not null)
            {
                await RevokeLineAsync(order, line, reason, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Returns whether the order can be granted at all, such as only when it has a signed-in buyer. Defaults to
    /// <see langword="true"/>.
    /// </summary>
    /// <param name="order">The paid order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected virtual Task<bool> CanGrantAsync(Order order, CancellationToken cancellationToken)
        => Task.FromResult(true);

    /// <summary>
    /// Returns the IDs of the order's lines that were granted already, so they are not granted again.
    /// </summary>
    /// <param name="order">The paid order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task<ISet<string>> GetGrantedLineIdsAsync(Order order, CancellationToken cancellationToken);

    /// <summary>
    /// Grants one line, from the purchased version of its product.
    /// </summary>
    /// <param name="context">The order, the line and the purchased product with its part.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when something was granted.</returns>
    protected abstract Task<bool> GrantLineAsync(OrderLineGrantContext<TPart> context, CancellationToken cancellationToken);

    /// <summary>
    /// Takes back what one line granted, when its order is canceled or refunded in full. Called for every line; a line
    /// that granted nothing has nothing to take back.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="line">The line.</param>
    /// <param name="reason">Why it is taken back.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task RevokeLineAsync(Order order, OrderLine line, OrderRevokeReason reason, CancellationToken cancellationToken);

    /// <summary>
    /// Runs after the lines were granted, such as to refresh the buyer's sign-in. Does nothing by default.
    /// </summary>
    /// <param name="order">The paid order.</param>
    /// <param name="granted">How many lines were granted now.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected virtual Task GrantedAsync(Order order, int granted, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
