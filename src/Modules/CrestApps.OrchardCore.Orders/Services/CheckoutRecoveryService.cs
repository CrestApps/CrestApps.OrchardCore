using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Emails buyers who left a checkout without paying, once per order, with what they chose and a link back.
/// </summary>
public sealed class CheckoutRecoveryService
{
    private const int PageSize = 200;

    // A buyer who left more than a week ago has moved on; a recovery email then is noise.
    private static readonly TimeSpan _window = TimeSpan.FromDays(7);

    private static readonly HtmlEncoder _encoder = HtmlEncoder.Default;

    private static readonly HashSet<OrderCancelReason> _leftReasons =
    [
        OrderCancelReason.CheckoutCanceled,
        OrderCancelReason.CheckoutExpired,
        OrderCancelReason.Abandoned,
        OrderCancelReason.PaymentFailed,
    ];

    private readonly IOrderStore _orderStore;
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly CheckoutRecoveryLinks _links;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IOrderEmailTemplates _emailTemplates;

    internal readonly IStringLocalizer S;

    public CheckoutRecoveryService(
        IOrderStore orderStore,
        ICheckoutSessionStore sessionStore,
        CheckoutRecoveryLinks links,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<CheckoutRecoveryService> logger,
        IStringLocalizer<CheckoutRecoveryService> stringLocalizer,
        IOrderEmailTemplates emailTemplates)
    {
        _orderStore = orderStore;
        _sessionStore = sessionStore;
        _links = links;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
        _emailTemplates = emailTemplates;
        S = stringLocalizer;
    }

    /// <summary>
    /// Sends the recovery emails that are due, and returns how many were sent.
    /// </summary>
    public async Task<int> SendDueAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<OrderSettings>();

        if (!settings.SendRecoveryEmails || _serviceProvider.GetService<IEmailService>() is not { } emailService)
        {
            return 0;
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var now = _clock.UtcNow;
        var leftBefore = now - TimeSpan.FromHours(Math.Max(1, settings.RecoveryDelayHours));
        var sent = 0;

        var (_, orders) = await _orderStore.PageAsync(new OrderQuery
        {
            Statuses = [OrderStatus.AwaitingPayment, OrderStatus.Canceled],
            CreatedOnOrAfter = now - _window,
            CreatedBefore = leftBefore,
        }, 0, PageSize, cancellationToken);

        foreach (var order in orders)
        {
            var (email, name) = await GetContactAsync(order);

            if (string.IsNullOrWhiteSpace(email) || !await IsDueAsync(order, email, leftBefore, cancellationToken))
            {
                continue;
            }

            var url = await _links.CreateUrlAsync(order.ItemId);

            if (string.IsNullOrEmpty(url))
            {
                _logger.LogWarning("Recovery emails need the site's base URL to link back to the checkout; none was sent.");

                return sent;
            }

            // Written in the language the buyer shopped in.
            using var language = OrderCulture.Use(order.Culture);

            var (builtSubject, builtText, builtHtml) = Build(order, name, url, site.SiteName);
            var message = await _emailTemplates.ApplyAsync(OrderEmailKinds.Recovery, order, builtSubject, builtText, builtHtml, new Dictionary<string, string>
            {
                ["RecoveryUrl"] = url,
            }, cancellationToken);

            try
            {
                await emailService.SendAsync(new MailMessage
                {
                    To = email,
                    Subject = message.Subject,
                    TextBody = message.Text,
                    HtmlBody = message.Html,
                }, cancellationToken: cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "The recovery email for order {OrderNumber} couldn't be sent.", order.OrderNumber);

                continue;
            }

            // Stamped after sending: a failed send is tried again on the next run.
            order.RecoveryEmailSentUtc = now;
            await _orderStore.UpdateAsync(order, cancellationToken);
            sent++;
        }

        return sent;
    }

    // An unpaid order takes its customer's email only when it is paid; until then it is on the checkout's contact step.
    private async Task<(string Email, string Name)> GetContactAsync(Order order)
    {
        if (!string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            return (order.CustomerEmail, order.CustomerName);
        }

        if (string.IsNullOrEmpty(order.CheckoutSessionId) ||
            await _sessionStore.GetAsync(order.CheckoutSessionId) is not { } session ||
            !session.TryGet<CheckoutContactInfo>(out var contact) ||
            contact is null)
        {
            return (null, null);
        }

        return (contact.Email, contact.DisplayName);
    }

    private async Task<bool> IsDueAsync(Order order, string email, DateTime leftBefore, CancellationToken cancellationToken)
    {
        if (order.RecoveryEmailSentUtc.HasValue || (order.Lines?.Count ?? 0) == 0)
        {
            return false;
        }

        // Canceled only because the buyer left, failed to pay, or let the checkout expire; and left a while ago.
        if (order.Status == OrderStatus.Canceled && (order.CancelReason is not { } reason || !_leftReasons.Contains(reason) || order.CanceledUtc > leftBefore))
        {
            return false;
        }

        if (order.Status == OrderStatus.AwaitingPayment && order.ModifiedUtc > leftBefore)
        {
            return false;
        }

        // A buyer who came back and paid since needs no reminder.
        var (paidSince, _) = await _orderStore.PageAsync(new OrderQuery
        {
            CustomerEmail = email,
            Statuses = [OrderStatus.Paid],
            CreatedOnOrAfter = order.CreatedUtc,
        }, 0, 1, cancellationToken);

        return paidSince == 0;
    }

    internal (string Subject, string Text, string Html) Build(Order order, string customerName, string url, string siteName)
    {
        string Money(decimal amount) => ProductPriceFormatter.Format(amount, order.Currency);

        var store = string.IsNullOrWhiteSpace(siteName) ? S["our store"].Value : siteName;
        var greeting = string.IsNullOrWhiteSpace(customerName)
            ? S["You left something behind."].Value
            : S["{0}, you left something behind.", customerName].Value;
        var lead = S["Your checkout at {0} wasn't finished. Here's what you chose; it's waiting for you.", store].Value;
        var action = S["Return to your cart"].Value;
        var footer = S["You're getting this one email because you started a checkout. If you've changed your mind, there's nothing to do."].Value;

        var text = new StringBuilder().AppendLine(greeting).AppendLine(lead).AppendLine();
        var html = new StringBuilder()
            .Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#212529;max-width:600px\">")
            .Append("<p style=\"font-size:16px;font-weight:bold\">").Append(_encoder.Encode(greeting)).Append("</p>")
            .Append("<p>").Append(_encoder.Encode(lead)).Append("</p>")
            .Append("<table style=\"width:100%;border-collapse:collapse\">");

        foreach (var line in order.Lines)
        {
            var title = string.IsNullOrEmpty(line.VariantTitle) ? line.Title : $"{line.Title} ({line.VariantTitle})";

            if (line.AddOns is { Count: > 0 })
            {
                title += " · " + string.Join(" · ", line.AddOns.Select(OrderLineText.DescribeAddOn));
            }

            text.AppendLine($"{line.Quantity} × {title}  {Money(line.LineTotal)}");
            html.Append("<tr><td style=\"padding:6px 0;border-bottom:1px solid #f1f3f5\">").Append(line.Quantity).Append(" × ").Append(_encoder.Encode(title ?? string.Empty)).Append("</td>")
                .Append("<td style=\"padding:6px 0;border-bottom:1px solid #f1f3f5;text-align:right\">").Append(_encoder.Encode(Money(line.LineTotal))).Append("</td></tr>");
        }

        html.Append("</table>")
            .Append("<p style=\"margin:24px 0\"><a href=\"").Append(_encoder.Encode(url)).Append("\" style=\"background:#0d6efd;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:6px;display:inline-block\">")
            .Append(_encoder.Encode(action)).Append("</a></p>")
            .Append("<p style=\"color:#6c757d;font-size:12px\">").Append(_encoder.Encode(footer)).Append("</p>")
            .Append("</div>");

        text.AppendLine().AppendLine($"{action}: {url}").AppendLine().AppendLine(footer);

        return (S["You left something in your cart at {0}", store].Value, text.ToString(), html.ToString());
    }
}
