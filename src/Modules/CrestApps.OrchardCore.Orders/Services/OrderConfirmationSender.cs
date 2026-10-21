using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Emails a paid order's confirmation to the customer, the first time when the checkout completes, or again when the
/// store resends it.
/// </summary>
public sealed class OrderConfirmationSender
{
    private readonly IOrderStore _orderStore;
    private readonly OrderConfirmationEmailBuilder _emailBuilder;
    private readonly IOrderLinkService _orderLinks;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IOrderEmailTemplates _emailTemplates;

    public OrderConfirmationSender(
        IOrderStore orderStore,
        OrderConfirmationEmailBuilder emailBuilder,
        IOrderLinkService orderLinks,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<OrderConfirmationSender> logger,
        IOrderEmailTemplates emailTemplates)
    {
        _orderStore = orderStore;
        _emailBuilder = emailBuilder;
        _orderLinks = orderLinks;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
        _emailTemplates = emailTemplates;
    }

    /// <summary>
    /// Gets a value indicating whether the site can send email.
    /// </summary>
    public bool CanSend
        => _serviceProvider.GetService<IEmailService>() is not null;

    /// <summary>
    /// Sends the confirmation. A failed send is logged and returns <see langword="false"/>; it never undoes the order.
    /// </summary>
    public async Task<bool> SendAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (_serviceProvider.GetService<IEmailService>() is not { } emailService || string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            return false;
        }

        // Written in the language the buyer shopped in.
        using var language = OrderCulture.Use(order.Culture);

        // A guest opens the order from the email; a signed-in customer finds it under My Orders.
        var orderUrl = order.UserId is null ? await _orderLinks.CreateUrlAsync(order) : null;
        var built = await _emailBuilder.BuildAsync(order, orderUrl);
        var email = await _emailTemplates.ApplyAsync(OrderEmailKinds.Confirmation, order, built.Subject, built.Text, built.Html, new Dictionary<string, string>
        {
            ["OrderUrl"] = orderUrl,
        }, cancellationToken);

        var result = await emailService.SendAsync(new MailMessage
        {
            To = order.CustomerEmail,
            Subject = email.Subject,
            TextBody = email.Text,
            HtmlBody = email.Html,
        }, cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("The confirmation of order '{OrderNumber}' could not be emailed.", order.OrderNumber);

            return false;
        }

        order.ConfirmationSentUtc = _clock.UtcNow;

        await _orderStore.UpdateAsync(order, cancellationToken);

        return true;
    }
}
