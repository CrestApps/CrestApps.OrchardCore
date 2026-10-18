using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Inventory.Handlers;

/// <summary>
/// Holds stock for an order when it is made, sells it when the order is paid, and lets it go when the order is
/// canceled.
/// </summary>
public sealed class InventoryOrderEventHandler : OrderEventHandlerBase
{
    private readonly IInventoryService _inventory;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public InventoryOrderEventHandler(
        IInventoryService inventory,
        ISiteService siteService,
        IClock clock,
        IStringLocalizer<InventoryOrderEventHandler> stringLocalizer)
    {
        _inventory = inventory;
        _siteService = siteService;
        _clock = clock;
        S = stringLocalizer;
    }

    public override async Task CreatedAsync(OrderCreatedContext context, CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<InventorySettings>();
        var expires = _clock.UtcNow.AddMinutes(Math.Max(1, settings.HoldMinutes));

        var result = await _inventory.ReserveAsync(context.Order.ItemId, ToRequests(context.Order), expires, cancellationToken);

        foreach (var shortage in result.Shortages)
        {
            context.Refuse(shortage.Available > 0
                ? S["Only {0} of {1} left. Lower the quantity to check out.", shortage.Available, shortage.Item.Title]
                : S["{0} is out of stock. Remove it to check out.", shortage.Item.Title]);
        }
    }

    // In the checkout's unit of work: when another sale of the same item commits first, the completion rolls back and
    // the checkout retries it, and selling again changes nothing.
    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
        => _inventory.CommitAsync(context.Order.ItemId, ToRequests(context.Order), cancellationToken);

    public override Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
        => _inventory.ReleaseAsync(context.Order.ItemId, $"The order was canceled: {context.Reason}.", cancellationToken);

    private static IEnumerable<StockRequest> ToRequests(Order order)
        => order.Lines.Select(line => new StockRequest(line.ContentItemId, line.VariantId, line.Quantity));
}
