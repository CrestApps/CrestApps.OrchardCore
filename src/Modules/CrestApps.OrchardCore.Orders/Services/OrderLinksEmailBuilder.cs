using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// The Find my order email: a link to each recent order placed with the address. Every value is HTML-encoded.
/// </summary>
public sealed class OrderLinksEmailBuilder
{
    private static readonly HtmlEncoder _encoder = HtmlEncoder.Default;

    internal readonly IStringLocalizer S;

    public OrderLinksEmailBuilder(IStringLocalizer<OrderLinksEmailBuilder> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public OrderConfirmationEmail Build(IReadOnlyList<(Order Order, string Url)> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);

        var intro = S["Here are the links to your recent orders. Each one opens the order without signing in, for 60 days."].Value;
        var text = new StringBuilder().AppendLine(intro).AppendLine();
        var html = new StringBuilder()
            .Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#212529;max-width:600px\">")
            .Append("<p>").Append(_encoder.Encode(intro)).Append("</p><ul>");

        foreach (var (order, url) in orders)
        {
            var label = S["Order {0}, placed {1:d}, {2}", order.OrderNumber, order.CreatedUtc, ProductPriceFormatter.Format(order.Total ?? order.Subtotal, order.Currency)].Value;

            text.AppendLine(label).AppendLine(url).AppendLine();
            html.Append("<li style=\"margin-bottom:8px\"><a href=\"").Append(_encoder.Encode(url)).Append("\">").Append(_encoder.Encode(label)).Append("</a></li>");
        }

        var footer = S["If you didn't ask for these links, you can ignore this email."].Value;

        text.AppendLine(footer);
        html.Append("</ul><p style=\"color:#6c757d\">").Append(_encoder.Encode(footer)).Append("</p></div>");

        return new OrderConfirmationEmail(S["Your orders"].Value, text.ToString(), html.ToString());
    }
}
