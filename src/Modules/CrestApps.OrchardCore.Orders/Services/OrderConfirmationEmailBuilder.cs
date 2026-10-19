using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
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
        var billedTo = await GetAddressLinesAsync(order.BillingAddress);
        var shipsTo = await GetAddressLinesAsync(order.ShippingAddress);

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
            var title = OrderLineText.Describe(line);

            text.AppendLine($"{line.Quantity} × {title}  {Money(line.LineTotal)}");
            html.Append("<tr>")
                .Append("<td style=\"padding:4px 0\">").Append(line.Quantity).Append(" × ").Append(_encoder.Encode(title ?? string.Empty)).Append("</td>")
                .Append("<td style=\"padding:4px 0;text-align:right\">").Append(_encoder.Encode(Money(line.LineTotal))).Append("</td>")
                .Append("</tr>");
        }

        foreach (var discount in order.Discounts ?? [])
        {
            var label = string.IsNullOrEmpty(discount.Code) ? S["Discount: {0}", discount.Description].Value : S["Discount ({0})", discount.Code].Value;
            var amount = "-" + Money(discount.Amount);

            text.AppendLine($"{label}  {amount}");
            html.Append("<tr><td style=\"padding:4px 0\">").Append(_encoder.Encode(label)).Append("</td>")
                .Append("<td style=\"padding:4px 0;text-align:right\">").Append(_encoder.Encode(amount)).Append("</td></tr>");
        }

        if (order.Shipping is not null)
        {
            var label = S["Shipping ({0})", order.Shipping.Name].Value;
            var amount = order.Shipping.Amount == 0 ? S["Free"].Value : Money(order.Shipping.Amount);

            text.AppendLine($"{label}  {amount}");
            html.Append("<tr><td style=\"padding:4px 0\">").Append(_encoder.Encode(label)).Append("</td>")
                .Append("<td style=\"padding:4px 0;text-align:right\">").Append(_encoder.Encode(amount)).Append("</td></tr>");
        }

        if (order.TaxAmount is > 0)
        {
            text.AppendLine($"{S["Tax"]}  {Money(order.TaxAmount.Value)}");
            html.Append("<tr><td style=\"padding:4px 0\">").Append(_encoder.Encode(S["Tax"].Value)).Append("</td>")
                .Append("<td style=\"padding:4px 0;text-align:right\">").Append(_encoder.Encode(Money(order.TaxAmount.Value))).Append("</td></tr>");
        }

        text.AppendLine($"{S["Total"]}  {Money(total)}");
        html.Append("<tr><td style=\"padding:8px 0;border-top:1px solid #dee2e6;font-weight:bold\">").Append(_encoder.Encode(S["Total"].Value)).Append("</td>")
            .Append("<td style=\"padding:8px 0;border-top:1px solid #dee2e6;font-weight:bold;text-align:right\">").Append(_encoder.Encode(Money(total))).Append("</td></tr>");

        foreach (var tender in order.Tenders ?? [])
        {
            var label = S["Paid with {0}", tender.Description].Value;

            text.AppendLine($"{label}  {Money(tender.Amount)}");
            html.Append("<tr><td style=\"padding:4px 0\">").Append(_encoder.Encode(label)).Append("</td>")
                .Append("<td style=\"padding:4px 0;text-align:right\">").Append(_encoder.Encode(Money(tender.Amount))).Append("</td></tr>");
        }

        html.Append("</table>");

        foreach (var provider in _serviceProvider.GetServices<IOrderEmailSectionProvider>())
        {
            if (await provider.GetSectionAsync(order) is { Links.Count: > 0 } section)
            {
                AppendSection(text, html, section);
            }
        }

        AppendAddress(text, html, S["Ships to:"].Value, S["Ships to"].Value, shipsTo);
        AppendAddress(text, html, S["Billed to:"].Value, S["Billed to"].Value, billedTo);

        if (!string.IsNullOrEmpty(orderUrl))
        {
            var view = S["View your order"].Value;

            text.AppendLine().AppendLine($"{view}: {orderUrl}");
            html.Append("<p style=\"margin-top:16px\"><a href=\"").Append(_encoder.Encode(orderUrl)).Append("\">").Append(_encoder.Encode(view)).Append("</a></p>");
        }

        html.Append("</div>");

        return new OrderConfirmationEmail(S["Your order {0}", order.OrderNumber].Value, text.ToString(), html.ToString());
    }

    private static void AppendSection(StringBuilder text, StringBuilder html, OrderEmailSection section)
    {
        text.AppendLine().AppendLine(section.Title + ":");
        html.Append("<p style=\"margin-top:16px\"><strong>").Append(_encoder.Encode(section.Title ?? string.Empty)).Append("</strong></p>");

        if (!string.IsNullOrEmpty(section.Intro))
        {
            text.AppendLine(section.Intro);
            html.Append("<p>").Append(_encoder.Encode(section.Intro)).Append("</p>");
        }

        html.Append("<ul style=\"padding-left:18px\">");

        foreach (var link in section.Links)
        {
            text.AppendLine(string.IsNullOrEmpty(link.Note) ? $"{link.Text}: {link.Url}" : $"{link.Text}: {link.Url} ({link.Note})");
            html.Append("<li><a href=\"").Append(_encoder.Encode(link.Url ?? string.Empty)).Append("\">").Append(_encoder.Encode(link.Text ?? string.Empty)).Append("</a>");

            if (!string.IsNullOrEmpty(link.Note))
            {
                html.Append(" <span style=\"color:#6c757d\">").Append(_encoder.Encode(link.Note)).Append("</span>");
            }

            html.Append("</li>");
        }

        html.Append("</ul>");
    }

    private static void AppendAddress(StringBuilder text, StringBuilder html, string textLabel, string htmlLabel, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        text.AppendLine().AppendLine(textLabel);
        html.Append("<p style=\"margin-top:16px\"><strong>").Append(_encoder.Encode(htmlLabel)).Append("</strong><br>");

        for (var i = 0; i < lines.Count; i++)
        {
            text.AppendLine(lines[i]);
            html.Append(_encoder.Encode(lines[i]));

            if (i < lines.Count - 1)
            {
                html.Append("<br>");
            }
        }

        html.Append("</p>");
    }

    // Printed the way the address's country writes it when the Addresses feature is on, plainly otherwise.
    private async Task<IReadOnlyList<string>> GetAddressLinesAsync(Address address)
    {
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
