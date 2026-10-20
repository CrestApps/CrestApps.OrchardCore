using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Gives a signed-in buyer points for an order once it's placed, and takes them back when it's canceled or refunded.
/// </summary>
internal sealed class LoyaltyOrderEventHandler : OrderEventHandlerBase
{
    private readonly ICustomerStore _customerStore;
    private readonly ILoyaltyStore _store;
    private readonly LoyaltyService _loyalty;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    // The order service raises the events this handles, so it's resolved when an event comes, not injected.
    public LoyaltyOrderEventHandler(
        ICustomerStore customerStore,
        ILoyaltyStore store,
        LoyaltyService loyalty,
        IServiceProvider serviceProvider,
        IStringLocalizer<LoyaltyOrderEventHandler> stringLocalizer)
    {
        _customerStore = customerStore;
        _store = store;
        _loyalty = loyalty;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    public override Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
    {
        var orderId = context.Order.ItemId;

        // Points are given once the order is saved, in a scope of their own: a balance changes under its own lock and
        // commits, which inside a checkout's completion would commit the checkout halfway.
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider
            .GetServices<IOrderEventHandler>()
            .OfType<LoyaltyOrderEventHandler>()
            .First()
            .AwardAsync(orderId));

        return Task.CompletedTask;
    }

    internal async Task AwardAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (await _serviceProvider.GetRequiredService<IOrderStore>().FindByIdAsync(orderId, cancellationToken) is not { Status: OrderStatus.Paid } order)
        {
            return;
        }

        if (await FindCustomerIdAsync(order, cancellationToken) is not { } customerId)
        {
            return;
        }

        // Paid is raised once per order, but points are never given twice for one.
        if ((await _store.FindByCustomerAsync(customerId, cancellationToken))?.Activity?.Any(activity => activity.OrderId == order.ItemId && activity.Kind == LoyaltyActivityKind.Earned) == true)
        {
            return;
        }

        var settings = await _loyalty.GetSettingsAsync();
        var points = LoyaltyService.GetPointsFor(order, settings);

        if (points <= 0 || await _loyalty.ChangeAsync(customerId, points, LoyaltyActivityKind.Earned, S["Earned with order {0}", order.OrderNumber], orderId: order.ItemId, cancellationToken: cancellationToken) is null)
        {
            return;
        }

        await _serviceProvider.GetRequiredService<IOrderService>().AddNoteAsync(order, S["The buyer earned {0} {1} points.", points, settings.ProgramName], OrderNoteKind.Event, null, cancellationToken);
    }

    public override Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
        => TakeBackAsync(context.Order, fraction: 1m, cancellationToken);

    public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
    {
        var order = context.Order;
        var total = order.Total ?? order.Subtotal;

        // What's taken back follows how much of the order was refunded.
        return total <= 0m || context.Refund is null
            ? Task.CompletedTask
            : TakeBackAsync(order, Math.Min(1m, context.Refund.Amount / total), cancellationToken);
    }

    private async Task TakeBackAsync(Order order, decimal fraction, CancellationToken cancellationToken)
    {
        if (await FindCustomerIdAsync(order, cancellationToken) is not { } customerId ||
            await _store.FindByCustomerAsync(customerId, cancellationToken) is not { } account)
        {
            return;
        }

        var earned = account.GetEarnedFor(order.ItemId);
        var originally = (account.Activity ?? []).Where(activity => activity.OrderId == order.ItemId && activity.Kind == LoyaltyActivityKind.Earned).Sum(activity => activity.Points);
        var points = Math.Min(earned, fraction >= 1m ? earned : (int)Math.Round(originally * fraction, MidpointRounding.AwayFromZero));

        if (points <= 0)
        {
            return;
        }

        await _loyalty.ChangeAsync(customerId, -points, LoyaltyActivityKind.Reversed, S["Taken back with order {0}", order.OrderNumber], orderId: order.ItemId, cancellationToken: cancellationToken);
    }

    private async Task<string> FindCustomerIdAsync(Order order, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(order?.UserId)
            ? null
            : (await _customerStore.FindByUserIdAsync(order.UserId, cancellationToken))?.ItemId;
}
