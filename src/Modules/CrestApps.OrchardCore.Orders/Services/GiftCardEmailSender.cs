using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Emails a gift card to its recipient: what's on it, its code, and how to use it.
/// </summary>
public sealed class GiftCardEmailSender
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HtmlEncoder _encoder;
    private readonly ILogger _logger;
    private readonly IOrderEmailTemplates _emailTemplates;
    private readonly IOrderStore _orderStore;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public GiftCardEmailSender(
        IServiceProvider serviceProvider,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        HtmlEncoder encoder,
        ILogger<GiftCardEmailSender> logger,
        IStringLocalizer<GiftCardEmailSender> stringLocalizer,
        IOrderEmailTemplates emailTemplates,
        IOrderStore orderStore,
        ILocalClock localClock)
    {
        _serviceProvider = serviceProvider;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _encoder = encoder;
        _logger = logger;
        _emailTemplates = emailTemplates;
        _orderStore = orderStore;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Sends the card to <see cref="GiftCard.RecipientEmail"/>, with an optional personal message. Returns whether it
    /// went.
    /// </summary>
    public async Task<bool> SendAsync(GiftCard card, string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (string.IsNullOrWhiteSpace(card.RecipientEmail) || _serviceProvider.GetService<IEmailService>() is not { } emailService)
        {
            return false;
        }

        // Written in the language the card was bought in.
        var order = string.IsNullOrEmpty(card.OrderId) ? null : await _orderStore.FindByIdAsync(card.OrderId, cancellationToken);
        using var language = OrderCulture.Use(order?.Culture);

        var site = await _siteService.GetSiteSettingsAsync();
        var request = _httpContextAccessor.HttpContext?.Request;
        var storeUrl = request is null ? null : $"{request.Scheme}://{request.Host}{request.PathBase}/";
        var value = ProductPriceFormatter.Format(card.Balance, card.Currency);

        var subject = S["Your {0} gift card from {1}", value, site.SiteName].Value;
        var greeting = string.IsNullOrWhiteSpace(card.RecipientName) ? S["Hello,"].Value : S["Hello {0},", card.RecipientName].Value;
        var intro = S["You've been given a gift card worth {0} to spend at {1}.", value, site.SiteName].Value;
        var howTo = S["At checkout, enter the code under Gift card and click Apply. It pays toward your order, and what's left stays on the card for next time."].Value;
        var expires = card.ExpiresUtc.HasValue
            ? S["It can be used until {0:d}.", (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(card.ExpiresUtc.Value, DateTimeKind.Utc))).DateTime].Value
            : null;

        var text = new StringBuilder()
            .AppendLine(greeting)
            .AppendLine()
            .AppendLine(intro);

        if (!string.IsNullOrWhiteSpace(message))
        {
            text.AppendLine().AppendLine(message);
        }

        text.AppendLine()
            .AppendLine(S["Gift card code: {0}", card.Code])
            .AppendLine()
            .AppendLine(howTo);

        if (expires is not null)
        {
            text.AppendLine(expires);
        }

        if (storeUrl is not null)
        {
            text.AppendLine().AppendLine(storeUrl);
        }

        var html = new StringBuilder()
            .Append("<p>").Append(_encoder.Encode(greeting)).Append("</p>")
            .Append("<p>").Append(_encoder.Encode(intro)).Append("</p>");

        if (!string.IsNullOrWhiteSpace(message))
        {
            html.Append("<blockquote style=\"margin:16px 0;padding:8px 16px;border-left:4px solid #dee2e6\">").Append(_encoder.Encode(message)).Append("</blockquote>");
        }

        html.Append("<p style=\"margin:24px 0;padding:16px;border:2px dashed #6c757d;text-align:center\">")
            .Append("<span style=\"display:block;font-size:12px;color:#6c757d\">").Append(_encoder.Encode(S["Gift card code"].Value)).Append("</span>")
            .Append("<span style=\"display:block;font-size:22px;font-family:monospace;letter-spacing:2px\">").Append(_encoder.Encode(card.Code)).Append("</span>")
            .Append("<span style=\"display:block;margin-top:4px\">").Append(_encoder.Encode(value)).Append("</span>")
            .Append("</p>")
            .Append("<p>").Append(_encoder.Encode(howTo)).Append("</p>");

        if (expires is not null)
        {
            html.Append("<p>").Append(_encoder.Encode(expires)).Append("</p>");
        }

        if (storeUrl is not null)
        {
            html.Append("<p><a href=\"").Append(_encoder.Encode(storeUrl)).Append("\">").Append(_encoder.Encode(S["Shop at {0}", site.SiteName].Value)).Append("</a></p>");
        }

        var email = await _emailTemplates.ApplyAsync(OrderEmailKinds.GiftCard, order, subject, text.ToString(), html.ToString(), new Dictionary<string, string>
        {
            ["Code"] = card.Code,
            ["Amount"] = value,
            ["RecipientName"] = card.RecipientName,
            ["Message"] = message,
        }, cancellationToken);

        var result = await emailService.SendAsync(new MailMessage
        {
            To = card.RecipientEmail,
            Subject = email.Subject,
            TextBody = email.Text,
            HtmlBody = email.Html,
        }, cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Gift card '{GiftCardId}' couldn't be emailed.", card.ItemId);
        }

        return result.Succeeded;
    }
}
