using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Downloads.Handlers;

/// <summary>
/// Gives a paid order its downloads, and takes them back when it is canceled or refunded in full.
/// </summary>
internal sealed class DownloadOrderEventHandler : OrderEventHandlerBase
{
    private readonly DownloadService _downloads;

    public DownloadOrderEventHandler(DownloadService downloads)
    {
        _downloads = downloads;
    }

    // In the checkout's unit of work, so the confirmation email sent after it can carry the links.
    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => _downloads.GrantAsync(context.Order, cancellationToken);

    public override Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
        => _downloads.RevokeOrderAsync(context.Order.ItemId, "The order was canceled.", cancellationToken);

    // After a partial refund the buyer keeps what they bought, files included.
    public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => context.Order.RefundStatus == OrderRefundStatus.Refunded
            ? _downloads.RevokeOrderAsync(context.Order.ItemId, "The order was refunded.", cancellationToken)
            : Task.CompletedTask;
}
