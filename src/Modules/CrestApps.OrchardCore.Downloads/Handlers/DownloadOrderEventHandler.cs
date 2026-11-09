using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Downloads.Handlers;

/// <summary>
/// Gives a paid order its downloads, the files of the product version it bought, and takes them back when it is
/// canceled or refunded in full.
/// </summary>
internal sealed class DownloadOrderEventHandler : OrderLineGrantHandlerBase<DigitalFilesPart>
{
    private readonly DownloadService _downloads;

    internal readonly IStringLocalizer S;

    public DownloadOrderEventHandler(
        DownloadService downloads,
        IContentManager contentManager,
        IStringLocalizer<DownloadOrderEventHandler> stringLocalizer)
        : base(contentManager)
    {
        _downloads = downloads;
        S = stringLocalizer;
    }

    protected override Task<ISet<string>> GetGrantedLineIdsAsync(Order order, CancellationToken cancellationToken)
        => _downloads.GetGrantedLineIdsAsync(order.ItemId, cancellationToken);

    // In the checkout's unit of work, so the confirmation email sent after it can carry the links.
    protected override async Task<bool> GrantLineAsync(OrderLineGrantContext<DigitalFilesPart> context, CancellationToken cancellationToken)
        => await _downloads.GrantLineAsync(context.Order, context.Line, context.Part, cancellationToken) > 0;

    protected override Task RevokeLineAsync(Order order, OrderLine line, OrderRevokeReason reason, CancellationToken cancellationToken)
        => _downloads.RevokeLineAsync(
            order.ItemId,
            line.LineId,
            reason == OrderRevokeReason.Canceled
                ? S["The order was canceled."]
                : S["The order was refunded."],
            cancellationToken);
}
