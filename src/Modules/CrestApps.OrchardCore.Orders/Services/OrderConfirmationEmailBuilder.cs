using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// What a paid order's confirmation email says: the order number, what was bought, what it came to, and where it is
/// billed. Every value taken from the order is HTML-encoded.
/// </summary>
public sealed class OrderConfirmationEmailBuilder
{
    private static readonly HtmlEncoder _encoder = HtmlEncoder.Default;

    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public OrderConfirmationEmailBuilder(IServiceProvider serviceProvider, IStringLocalizer<OrderConfirmationEmailBuilder> stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <summary>
    /// The confirmation of <paramref name="order"/>, with a link to it when <paramref name="orderUrl"/> is given.
    /// </summary>
    public async Task<OrderConfirmationEmail> BuildAsync(Order order, string orderUrl = null)
    {
        ArgumentNullException.ThrowIfNull(order);

        string Money(decimal amount) => ProductPriceFormatter.Format(amount, order.Currency);

        var total = order.Total ?? order.Subtotal;
        var greeting = string.IsNullOrWhiteSpace(order.CustomerName)
            ? S["Thank you for your order."].Value
            : S["Thank you for your order, {0}.", order.CustomerName].Value;
        var billedTo = await GetBillingLinesAsync(order);

        var text = new StringBuilder()
            .AppendLine(greeting)
            .AppendLine(S["Your order number is {0}.", order.OrderNumber].Value)
            .AppendLine();

        var html = new StringBuilder()
            .Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#212529;max-width:600px\">")
            .Append("<p>").Append(_encoder.Encode(greeting)).Append("</p>")
            .Append("<p>").Append(_encoder.Encode(S["Your order number is {0}.", order.OrderNumber].Value)).Append("</p>")
            .Append("<table style=\"width:100%;border-collapse:collapse\">");

        foreach (var line in order.Lines)
        {
            var title = string.IsNullOrEmpty(line.PriceName) ? line.Title : $"{line.Title} ({line.PriceName})";

            text.AppendLine($"{line.Quantity} × {title}  {Money(line.LineTotal)}");
            html.Append("<tr>")
                .Append("<td style=\"padding:4px 0\">").Append(line.Quantity).Append(" × ").Append(_encoder.Encode(title ?? string.Empty)).Append("</td>")
                .Append("<td style=\"padding:4px 0;text-align:right\">").Append(_encoder.Encode(Money(line.LineTotal))).Append("</td>")
                .Append("</tr>");
        }

        if (order.TaxAmount is > 0)
        {
            text.AppendLine($"{S["Tax"]}  {Money(order.TaxAmount.Value)}");
            html.Append("<tr><td style=\"padding:4px 0\">").Append(_encoder.Encode(S["Tax"].Value)).Append("</td>")
                .Append("<td style=\"padding:4px 0;text-align:right\">").Append(_encoder.Encode(Money(order.TaxAmount.Value))).Append("</td></tr>");
        }

        text.AppendLine($"{S["Total"]}  {Money(total)}");
        html.Append("<tr><td style=\"padding:8px 0;border-top:1px solid #dee2e6;font-weight:bold\">").Append(_encoder.Encode(S["Total"].Value)).Append("</td>")
            .Append("<td style=\"padding:8px 0;border-top:1px solid #dee2e6;font-weight:bold;text-align:right\">").Append(_encoder.Encode(Money(total))).Append("</td></tr>")
            .Append("</table>");

        if (billedTo.Count > 0)
        {
            text.AppendLine().AppendLine(S["Billed to:"].Value);
            html.Append("<p style=\"margin-top:16px\"><strong>").Append(_encoder.Encode(S["Billed to"].Value)).Append("</strong><br>");

            for (var i = 0; i < billedTo.Count; i++)
            {
                text.AppendLine(billedTo[i]);
                html.Append(_encoder.Encode(billedTo[i]));

                if (i < billedTo.Count - 1)
                {
                    html.Append("<br>");
                }
            }

            html.Append("</p>");
        }

        if (!string.IsNullOrEmpty(orderUrl))
        {
            var view = S["View your order"].Value;

            text.AppendLine().AppendLine($"{view}: {orderUrl}");
            html.Append("<p style=\"margin-top:16px\"><a href=\"").Append(_encoder.Encode(orderUrl)).Append("\">").Append(_encoder.Encode(view)).Append("</a></p>");
        }

        html.Append("</div>");

        return new OrderConfirmationEmail(S["Your order {0}", order.OrderNumber].Value, text.ToString(), html.ToString());
    }

    // Printed the way the address's country writes it when the Addresses feature is on, plainly otherwise.
    private async Task<IReadOnlyList<string>> GetBillingLinesAsync(Order order)
    {
        var address = order.BillingAddress;

        if (address is null)
        {
            return [];
        }

        if (_serviceProvider.GetService<IAddressFormatter>() is { } formatter)
        {
            return await formatter.GetLinesAsync(address);
        }

        return new[]
        {
            address.Name,
            address.Company,
            address.AddressLine1,
            address.AddressLine2,
            string.Join(' ', new[] { address.City, address.Region, address.PostalCode }.Where(part => !string.IsNullOrWhiteSpace(part))),
            address.Country,
        }
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .ToArray();
    }
}

/// <summary>
/// A built order confirmation email.
/// </summary>
public sealed record OrderConfirmationEmail(string Subject, string Text, string Html);
