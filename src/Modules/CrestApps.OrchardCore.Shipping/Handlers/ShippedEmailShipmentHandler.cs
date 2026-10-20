using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Handlers;

/// <summary>
/// Emails the buyer when a shipment leaves, once per shipment.
/// </summary>
internal sealed class ShippedEmailShipmentHandler : ShipmentEventHandlerBase
{
    private readonly ISiteService _siteService;
    private readonly ShipmentEmailBuilder _emailBuilder;
    private readonly IOrderLinkService _orderLinks;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public ShippedEmailShipmentHandler(
        ISiteService siteService,
        ShipmentEmailBuilder emailBuilder,
        IOrderLinkService orderLinks,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<ShippedEmailShipmentHandler> logger)
    {
        _siteService = siteService;
        _emailBuilder = emailBuilder;
        _orderLinks = orderLinks;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
    }

    public override async Task ShippedAsync(ShipmentContext context)
    {
        var order = context.Order;
        var shipment = context.Shipment;

        if (shipment.ShippedEmailSentUtc is not null || string.IsNullOrWhiteSpace(order.CustomerEmail) ||
            !(await _siteService.GetSettingsAsync<ShippingSettings>()).SendShippedEmails ||
            _serviceProvider.GetService<IEmailService>() is not { } emailService)
        {
            return;
        }

        // Written in the language the buyer shopped in.
        using var language = OrderCulture.Use(order.Culture);

        // A guest follows the order by its link; a signed-in buyer finds it under My Orders.
        var orderUrl = order.UserId is null ? await _orderLinks.CreateUrlAsync(order) : null;
        var built = _emailBuilder.Build(order, shipment, orderUrl);
        var email = await _serviceProvider.ApplyOrderEmailTemplateAsync(OrderEmailKinds.Shipped, order, built.Subject, built.Text, built.Html, new Dictionary<string, string>
        {
            ["TrackingUrl"] = shipment.TrackingUrl,
            ["Carrier"] = shipment.Carrier,
            ["ShipmentNumber"] = shipment.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["OrderUrl"] = orderUrl,
        });

        try
        {
            var result = await emailService.SendAsync(new MailMessage
            {
                To = order.CustomerEmail,
                Subject = email.Subject,
                TextBody = email.Text,
                HtmlBody = email.Html,
            });

            if (result.Succeeded)
            {
                shipment.ShippedEmailSentUtc = _clock.UtcNow;
            }
            else
            {
                _logger.LogWarning("The email that shipment {Number} of order {OrderNumber} left couldn't be sent.", shipment.Number, order.OrderNumber);
            }
        }
        catch (Exception ex)
        {
            // A mail server that's down doesn't stop the shipment.
            _logger.LogError(ex, "The email that shipment {Number} of order {OrderNumber} left failed.", shipment.Number, order.OrderNumber);
        }
    }
}
