using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Handlers;

/// <summary>
/// Emails the confirmation of an order once its checkout completes and is committed, when the site has an email
/// service and the settings ask for it. It is sent once: a checkout completed again sends nothing.
/// </summary>
public sealed class OrderConfirmationCheckoutHandler : CheckoutHandlerBase
{
    private readonly IOrderStore _orderStore;
    private readonly ISiteService _siteService;
    private readonly OrderConfirmationSender _sender;

    public OrderConfirmationCheckoutHandler(
        IOrderStore orderStore,
        ISiteService siteService,
        OrderConfirmationSender sender)
    {
        _orderStore = orderStore;
        _siteService = siteService;
        _sender = sender;
    }

    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        if (context.Flow.Session is not CheckoutSession session ||
            !string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        if (!(await _siteService.GetSettingsAsync<OrderSettings>()).SendConfirmations || !_sender.CanSend)
        {
            return;
        }

        var order = await _orderStore.FindByIdAsync(session.ReferenceId);

        if (order is null || order.Status != OrderStatus.Paid || order.ConfirmationSentUtc is not null)
        {
            return;
        }

        await _sender.SendAsync(order);
    }
}
