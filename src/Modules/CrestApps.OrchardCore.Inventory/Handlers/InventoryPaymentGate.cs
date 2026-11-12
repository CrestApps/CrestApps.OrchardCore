using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Inventory.Handlers;

/// <summary>
/// Makes sure an order still holds its stock just before it is paid. A hold that ran out and was let go is placed again
/// when the stock is still there; when it was sold to someone else meanwhile, the payment is refused, instead of the
/// order being paid and its stock sold twice when it completes.
/// </summary>
public sealed class InventoryPaymentGate : ICheckoutPaymentGate
{
    private readonly IInventoryService _inventory;
    private readonly IOrderStore _orders;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryPaymentGate"/> class.
    /// </summary>
    /// <param name="inventory">The inventory service.</param>
    /// <param name="orders">The order store.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InventoryPaymentGate(
        IInventoryService inventory,
        IOrderStore orders,
        ISiteService siteService,
        IClock clock,
        IStringLocalizer<InventoryPaymentGate> stringLocalizer)
    {
        _inventory = inventory;
        _orders = orders;
        _siteService = siteService;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task BeginningAsync(CheckoutPaymentBeginningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Flow.Session is not CheckoutSession session ||
            !string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            await _orders.FindByIdAsync(session.ReferenceId, cancellationToken) is not { } order ||
            order.Status == OrderStatus.Paid ||
            order.Lines is not { Count: > 0 })
        {
            return;
        }

        var settings = await _siteService.GetSettingsAsync<InventorySettings>();
        var expires = _clock.UtcNow.AddMinutes(Math.Max(1, settings.HoldMinutes));

        // What the order still holds is left as it is; only lines whose hold was let go are held again.
        var result = await _inventory.ReserveAsync(order.ItemId, InventoryOrderEventHandler.ToRequests(order), expires, cancellationToken);

        if (result.Shortages is { Count: > 0 } shortages && shortages[0] is { } shortage)
        {
            context.Refuse(shortage.Available > 0
                ? S["Only {0} of {1} is left now: your hold on it ran out. Lower the quantity to check out.", shortage.Available, shortage.Item.Title]
                : S["{0} sold out while your hold on it ran out. Remove it to check out.", shortage.Item.Title]);
        }
    }
}
