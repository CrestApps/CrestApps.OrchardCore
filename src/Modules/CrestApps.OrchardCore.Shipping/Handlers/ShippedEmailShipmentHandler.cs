using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
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
    private readonly ICommerceEmailSender _emailSender;
    private readonly IClock _clock;

    public ShippedEmailShipmentHandler(
        ISiteService siteService,
        ShipmentEmailBuilder emailBuilder,
        IOrderLinkService orderLinks,
        ICommerceEmailSender emailSender,
        IClock clock)
    {
        _siteService = siteService;
        _emailBuilder = emailBuilder;
        _orderLinks = orderLinks;
        _emailSender = emailSender;
        _clock = clock;
    }

    public override async Task ShippedAsync(ShipmentContext context)
    {
        var order = context.Order;
        var shipment = context.Shipment;

        if (shipment.ShippedEmailSentUtc is not null || string.IsNullOrWhiteSpace(order.CustomerEmail) ||
            !(await _siteService.GetSettingsAsync<ShippingSettings>()).SendShippedEmails ||
            !_emailSender.CanSend)
        {
            return;
        }

        // Written in the language the buyer shopped in. A mail server that's down doesn't stop the shipment.
        var sent = await _emailSender.SendAsync(new CommerceEmail
        {
            Kind = OrderEmailKinds.Shipped,
            To = order.CustomerEmail,
            Order = order,
            Culture = order.Culture,
            Reference = $"{order.OrderNumber}/{shipment.Number}",
        }, async _ =>
        {
            // A guest follows the order by its link; a signed-in buyer finds it under My Orders.
            var orderUrl = order.UserId is null ? await _orderLinks.CreateUrlAsync(order) : null;
            var built = _emailBuilder.Build(order, shipment, orderUrl);

            return new CommerceEmailContent
            {
                Subject = built.Subject,
                Text = built.Text,
                Html = built.Html,
                Values = new Dictionary<string, string>
                {
                    ["TrackingUrl"] = shipment.TrackingUrl,
                    ["Carrier"] = shipment.Carrier,
                    ["ShipmentNumber"] = shipment.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["OrderUrl"] = orderUrl,
                },
            };
        });

        if (sent)
        {
            shipment.ShippedEmailSentUtc = _clock.UtcNow;
        }
    }
}
