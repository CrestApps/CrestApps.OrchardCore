using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Handlers;

/// <summary>
/// Emails a paid order's confirmation to the customer, once.
/// </summary>
/// <remarks>
/// It runs after the completion is committed, so a mail server that is down never undoes a paid order; the order is
/// simply not marked as confirmed. A site without an email service sends nothing.
/// </remarks>
public sealed class OrderConfirmationCheckoutHandler : CheckoutHandlerBase
{
    private readonly IOrderStore _orderStore;
    private readonly ISiteService _siteService;
    private readonly OrderConfirmationEmailBuilder _emailBuilder;
    private readonly IOrderLinkService _orderLinks;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public OrderConfirmationCheckoutHandler(
        IOrderStore orderStore,
        ISiteService siteService,
        OrderConfirmationEmailBuilder emailBuilder,
        IOrderLinkService orderLinks,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<OrderConfirmationCheckoutHandler> logger)
    {
        _orderStore = orderStore;
        _siteService = siteService;
        _emailBuilder = emailBuilder;
        _orderLinks = orderLinks;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
    }

    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        if (context.Flow.Session is not CheckoutSession session ||
            !string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        if (!(await _siteService.GetSettingsAsync<OrderSettings>()).SendConfirmations ||
            _serviceProvider.GetService<IEmailService>() is not { } emailService)
        {
            return;
        }

        var order = await _orderStore.FindByIdAsync(session.ReferenceId);

        if (order is null || order.Status != OrderStatus.Paid || order.ConfirmationSentUtc is not null || string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            return;
        }

        // A guest opens the order from the email; a signed-in customer finds it under My Orders.
        var orderUrl = order.UserId is null ? await _orderLinks.CreateUrlAsync(order) : null;
        var email = await _emailBuilder.BuildAsync(order, orderUrl);

        var result = await emailService.SendAsync(new MailMessage
        {
            To = order.CustomerEmail,
            Subject = email.Subject,
            TextBody = email.Text,
            HtmlBody = email.Html,
        });

        if (!result.Succeeded)
        {
            _logger.LogWarning("The confirmation of order '{OrderNumber}' could not be emailed.", order.OrderNumber);

            return;
        }

        order.ConfirmationSentUtc = _clock.UtcNow;

        await _orderStore.UpdateAsync(order);
    }
}
