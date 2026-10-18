using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// The email that tells the buyer a shipment left: what's in it, the carrier, and where to follow it.
/// </summary>
public sealed class ShipmentEmailBuilder
{
    private readonly HtmlEncoder _encoder;

    internal readonly IStringLocalizer S;

    public ShipmentEmailBuilder(HtmlEncoder encoder, IStringLocalizer<ShipmentEmailBuilder> stringLocalizer)
    {
        _encoder = encoder;
        S = stringLocalizer;
    }

    public ShipmentEmail Build(Order order, Shipment shipment, string orderUrl = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(shipment);

        var greeting = string.IsNullOrWhiteSpace(order.CustomerName)
            ? S["Good news: a shipment from your order {0} is on its way.", order.OrderNumber].Value
            : S["Good news, {0}: a shipment from your order {1} is on its way.", order.CustomerName, order.OrderNumber].Value;

        var text = new StringBuilder().AppendLine(greeting).AppendLine();
        var html = new StringBuilder()
            .Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#212529;max-width:600px\">")
            .Append("<p>").Append(_encoder.Encode(greeting)).Append("</p>")
            .Append("<ul>");

        foreach (var line in shipment.Lines)
        {
            text.AppendLine($"{line.Quantity} × {line.Title}");
            html.Append("<li>").Append(line.Quantity).Append(" × ").Append(_encoder.Encode(line.Title ?? string.Empty)).Append("</li>");
        }

        html.Append("</ul>");

        if (!string.IsNullOrEmpty(shipment.Carrier) || !string.IsNullOrEmpty(shipment.TrackingNumber))
        {
            var sentWith = string.IsNullOrEmpty(shipment.TrackingNumber)
                ? S["Sent with {0}.", shipment.Carrier].Value
                : string.IsNullOrEmpty(shipment.Carrier)
                    ? S["Tracking number: {0}.", shipment.TrackingNumber].Value
                    : S["Sent with {0}, tracking number {1}.", shipment.Carrier, shipment.TrackingNumber].Value;

            text.AppendLine().AppendLine(sentWith);
            html.Append("<p>").Append(_encoder.Encode(sentWith)).Append("</p>");
        }

        if (!string.IsNullOrEmpty(shipment.TrackingUrl))
        {
            var track = S["Track your parcel"].Value;

            text.AppendLine($"{track}: {shipment.TrackingUrl}");
            html.Append("<p><a href=\"").Append(_encoder.Encode(shipment.TrackingUrl)).Append("\">").Append(_encoder.Encode(track)).Append("</a></p>");
        }

        if (!string.IsNullOrEmpty(orderUrl))
        {
            var view = S["View your order"].Value;

            text.AppendLine().AppendLine($"{view}: {orderUrl}");
            html.Append("<p><a href=\"").Append(_encoder.Encode(orderUrl)).Append("\">").Append(_encoder.Encode(view)).Append("</a></p>");
        }

        html.Append("</div>");

        return new ShipmentEmail(S["Your order {0} has shipped", order.OrderNumber].Value, text.ToString(), html.ToString());
    }
}

public sealed record ShipmentEmail(string Subject, string Text, string Html);
