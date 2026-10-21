using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Downloads.Handlers;

/// <summary>
/// Gives a paid order its downloads, and takes them back when it is canceled or refunded in full.
/// </summary>
internal sealed class DownloadOrderEventHandler : OrderGrantHandlerBase
{
    private readonly DownloadService _downloads;

    internal readonly IStringLocalizer S;

    public DownloadOrderEventHandler(
        DownloadService downloads,
        IStringLocalizer<DownloadOrderEventHandler> stringLocalizer)
    {
        _downloads = downloads;
        S = stringLocalizer;
    }

    // In the checkout's unit of work, so the confirmation email sent after it can carry the links.
    protected override Task GrantAsync(Order order, CancellationToken cancellationToken)
        => _downloads.GrantAsync(order, cancellationToken);

    protected override Task RevokeAsync(Order order, OrderRevokeReason reason, CancellationToken cancellationToken)
        => _downloads.RevokeOrderAsync(
            order.ItemId,
            reason == OrderRevokeReason.Canceled
                ? S["The order was canceled."]
                : S["The order was refunded."],
            cancellationToken);
}
