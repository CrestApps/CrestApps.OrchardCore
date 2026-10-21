using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Services;

public enum OrderReturnEmailKind
{
    Requested,
    Approved,
    Rejected,
    Refunded,
    Exchanged,
}

/// <summary>
/// Tells the buyer where their return is: received as a request, approved with how to send the items, declined, and
/// refunded or exchanged. A failed send is logged; it never undoes the step.
/// </summary>
public sealed class OrderReturnEmailSender
{
    private readonly ICommerceEmailSender _emailSender;
    private readonly IOrderStore _orders;
    private readonly IOrderLinkService _links;
    private readonly ISiteService _siteService;
    private readonly HtmlEncoder _encoder;

    internal readonly IStringLocalizer S;

    public OrderReturnEmailSender(
        ICommerceEmailSender emailSender,
        IOrderStore orders,
        IOrderLinkService links,
        ISiteService siteService,
        HtmlEncoder encoder,
        IStringLocalizer<OrderReturnEmailSender> stringLocalizer)
    {
        _emailSender = emailSender;
        _orders = orders;
        _links = links;
        _siteService = siteService;
        _encoder = encoder;
        S = stringLocalizer;
    }

    public async Task<bool> SendAsync(OrderReturn orderReturn, OrderReturnEmailKind kind, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderReturn);

        if (!_emailSender.CanSend || string.IsNullOrWhiteSpace(orderReturn.CustomerEmail))
        {
            return false;
        }

        var order = await _orders.FindByIdAsync(orderReturn.OrderId, cancellationToken);

        // Written in the language the buyer shopped in.
        return await _emailSender.SendAsync(new CommerceEmail
        {
            Kind = OrderEmailKinds.Return,
            To = orderReturn.CustomerEmail,
            Order = order,
            Culture = order?.Culture,
            Reference = orderReturn.ItemId,
        }, async _ =>
        {
            var orderUrl = order is null ? null : await _links.CreateUrlAsync(order);
            var built = Build(orderReturn, kind, (await _siteService.GetSettingsAsync<ReturnSettings>()).Instructions, orderUrl);

            return new CommerceEmailContent
            {
                Subject = built.Subject,
                Text = built.Text,
                Html = built.Html,
                Values = new Dictionary<string, string>
                {
                    ["ReturnNumber"] = orderReturn.Number,
                    ["ReturnStatus"] = kind.ToString(),
                    ["OrderUrl"] = orderUrl,
                },
            };
        }, cancellationToken);
    }

    internal (string Subject, string Text, string Html) Build(OrderReturn orderReturn, OrderReturnEmailKind kind, string instructions, string orderUrl)
    {
        var (subject, lead) = kind switch
        {
            OrderReturnEmailKind.Approved => (S["Your return {0} is approved", orderReturn.Number].Value, S["Your return {0} for order {1} is approved. Here's how to send the items back.", orderReturn.Number, orderReturn.OrderNumber].Value),
            OrderReturnEmailKind.Rejected => (S["About your return {0}", orderReturn.Number].Value, S["We can't accept return {0} for order {1}.", orderReturn.Number, orderReturn.OrderNumber].Value),
            OrderReturnEmailKind.Refunded => (S["Your return {0} is refunded", orderReturn.Number].Value, S["We've refunded {0} for return {1}. It can take a few days to show on your statement.", ProductPriceFormatter.Format(orderReturn.RefundedAmount, orderReturn.Currency), orderReturn.Number].Value),
            OrderReturnEmailKind.Exchanged => (S["Your exchange {0} is on its way", orderReturn.Number].Value, S["Your exchange for return {0} is on its way.", orderReturn.Number].Value),
            _ => (S["We've got your return request {0}", orderReturn.Number].Value, S["We've got your request to return items of order {0}. We'll let you know once we've looked at it.", orderReturn.OrderNumber].Value),
        };

        var paragraphs = new List<string> { lead };

        if (kind == OrderReturnEmailKind.Approved && !string.IsNullOrWhiteSpace(instructions))
        {
            paragraphs.Add(instructions.Trim());
        }

        if (kind is OrderReturnEmailKind.Approved or OrderReturnEmailKind.Rejected or OrderReturnEmailKind.Exchanged && !string.IsNullOrWhiteSpace(orderReturn.StoreMessage))
        {
            paragraphs.Add(orderReturn.StoreMessage);
        }

        if (kind == OrderReturnEmailKind.Approved)
        {
            paragraphs.Add(S["Put the return number {0} in the parcel so we know it's yours.", orderReturn.Number].Value);
        }

        var text = new StringBuilder();
        var html = new StringBuilder("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#212529;max-width:600px\">");

        foreach (var paragraph in paragraphs)
        {
            text.AppendLine(paragraph).AppendLine();
            html.Append("<p style=\"white-space:pre-line\">").Append(_encoder.Encode(paragraph)).Append("</p>");
        }

        html.Append("<ul>");

        foreach (var line in orderReturn.Lines)
        {
            text.AppendLine($"{line.Quantity} × {line.Title}");
            html.Append("<li>").Append(line.Quantity).Append(" × ").Append(_encoder.Encode(line.Title ?? string.Empty)).Append("</li>");
        }

        html.Append("</ul>");

        if (!string.IsNullOrEmpty(orderUrl))
        {
            var view = S["View your order"].Value;

            text.AppendLine().AppendLine($"{view}: {orderUrl}");
            html.Append("<p><a href=\"").Append(_encoder.Encode(orderUrl)).Append("\">").Append(_encoder.Encode(view)).Append("</a></p>");
        }

        html.Append("</div>");

        return (subject, text.ToString(), html.ToString());
    }
}
