using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Consent.Unsubscribe;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Records;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.MailingLists.Core.Emails;

/// <summary>
/// Builds the mailing lists e-mails and hands them to <see cref="ITemplatedEmailOutbox"/>, which renders the site's
/// template of each kind around them and retries a send the e-mail service did not take. Dates are written in the
/// site's time zone with <see cref="ILocalClock"/>; the subscriber has no time zone of their own.
/// </summary>
public sealed class MailingListEmails : IMailingListEmails
{
    private readonly ITemplatedEmailOutbox _outbox;
    private readonly ITemplatedEmailOutboxStore _outboxStore;
    private readonly IUnsubscribeLinks _unsubscribeLinks;
    private readonly SiteUrlGenerator _urls;
    private readonly SignedTokenService _tokens;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListEmails"/> class.
    /// </summary>
    /// <param name="outbox">The durable templated e-mail outbox.</param>
    /// <param name="outboxStore">The stored e-mails of the outbox, for an erasure.</param>
    /// <param name="unsubscribeLinks">The unsubscribe links of the e-mail preferences page.</param>
    /// <param name="urls">The site URL generator, for the verification links.</param>
    /// <param name="tokens">The signed token service, for the verification links.</param>
    /// <param name="localClock">The local clock, for the coupon's end date.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public MailingListEmails(
        ITemplatedEmailOutbox outbox,
        ITemplatedEmailOutboxStore outboxStore,
        IUnsubscribeLinks unsubscribeLinks,
        SiteUrlGenerator urls,
        SignedTokenService tokens,
        ILocalClock localClock,
        IClock clock,
        ILogger<MailingListEmails> logger,
        IStringLocalizer<MailingListEmails> stringLocalizer)
    {
        _outbox = outbox;
        _outboxStore = outboxStore;
        _unsubscribeLinks = unsubscribeLinks;
        _urls = urls;
        _tokens = tokens;
        _localClock = localClock;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<bool> SendConfirmAsync(MailingList list, MailingListMember member, MailingListIssuedCoupon coupon, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(member);

        var verifyUrl = await VerifyUrlAsync(list, member);

        if (verifyUrl is null)
        {
            return false;
        }

        return await SendAsync(MailingListsConstants.EmailKinds.Confirm, list, member, coupon, verifyUrl, (links, couponModel) =>
        {
            var html = new StringBuilder();

            Greeting(html, member);
            Paragraph(html, S["Please confirm that you want to receive {0} at this address.", list.DisplayText]);
            Button(html, links.Verify, S["Confirm my subscription"]);

            if (couponModel is not null)
            {
                CouponBlock(html, couponModel, S["Your welcome code is below. Confirm your subscription to stay on the list."]);
            }

            Paragraph(html, S["If you did not ask for this, ignore this e-mail; you will not be subscribed."]);
            Footer(html, links);

            return new TemplatedEmailContent
            {
                Subject = S["Confirm your subscription to {0}", list.DisplayText],
                Html = html.ToString(),
                Values = Values(links, couponModel),
            };
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> SendWelcomeAsync(MailingList list, MailingListMember member, MailingListIssuedCoupon coupon, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(member);

        return SendAsync(MailingListsConstants.EmailKinds.Welcome, list, member, coupon, verifyUrl: null, (links, couponModel) =>
        {
            var html = new StringBuilder();

            Greeting(html, member);
            Paragraph(html, S["Welcome! You are now subscribed to {0}.", list.DisplayText]);

            if (!string.IsNullOrWhiteSpace(list.Description))
            {
                Paragraph(html, list.Description);
            }

            if (couponModel is not null)
            {
                CouponBlock(html, couponModel, S["Here is your welcome code."]);
            }

            Footer(html, links);

            return new TemplatedEmailContent
            {
                Subject = couponModel is null ? S["Welcome to {0}", list.DisplayText] : S["Welcome to {0}: your code is inside", list.DisplayText],
                Html = html.ToString(),
                Values = Values(links, couponModel),
            };
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SendClaimAsync(MailingList list, MailingListMember member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(member);

        var verifyUrl = await VerifyUrlAsync(list, member);

        if (verifyUrl is null)
        {
            return false;
        }

        return await SendAsync(MailingListsConstants.EmailKinds.CouponClaim, list, member, coupon: null, verifyUrl, (links, couponModel) =>
        {
            var html = new StringBuilder();

            Greeting(html, member);
            Paragraph(html, S["Welcome! You are now subscribed to {0}.", list.DisplayText]);
            Paragraph(html, S["A welcome gift waits for you. Confirm this is your address to get it."]);
            Button(html, links.Verify, S["Confirm and get my gift"]);
            Footer(html, links);

            return new TemplatedEmailContent
            {
                Subject = S["Welcome to {0}: claim your gift", list.DisplayText],
                Html = html.ToString(),
                Values = Values(links, couponModel),
            };
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> ForgetAsync(MailingListMember member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        return string.IsNullOrEmpty(member.ItemId)
            ? 0
            : await _outboxStore.DeleteBySourceAsync(new RecordRef(MailingListsConstants.MemberRecordKind, member.ItemId), cancellationToken);
    }

    private async Task<bool> SendAsync(
        string kind,
        MailingList list,
        MailingListMember member,
        MailingListIssuedCoupon coupon,
        string verifyUrl,
        Func<MailingListEmailLinks, MailingListEmailCoupon, TemplatedEmailContent> build,
        CancellationToken cancellationToken)
    {
        var to = member.Contact?.Email?.Trim();

        if (string.IsNullOrEmpty(to))
        {
            return false;
        }

        var links = new MailingListEmailLinks
        {
            Verify = verifyUrl,
            Unsubscribe = await _unsubscribeLinks.CreateUrlAsync(to, new UnsubscribeScope { Kind = MailingListsConstants.UnsubscribeScopeKind, Id = list.ItemId }, cancellationToken),
        };

        var couponModel = await CouponAsync(coupon);
        var models = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["List"] = new MailingListEmailList { Name = list.DisplayText, Description = list.Description },
            ["Member"] = new MailingListEmailMember { Name = member.Contact?.Name, Email = to },
            ["Links"] = links,
        };

        if (couponModel is not null)
        {
            models["Coupon"] = couponModel;
        }

        try
        {
            var result = await _outbox.SendAsync(
                new TemplatedEmail
                {
                    Kind = kind,
                    To = to,
                    Models = models,
                    Culture = TemplatedEmailCulture.Current(),
                    Reference = $"{MailingListsConstants.MemberRecordKind}:{member.ItemId}",
                    Source = new RecordRef(MailingListsConstants.MemberRecordKind, member.ItemId),
                },
                _ => Task.FromResult(build(links, couponModel)),
                cancellationToken);

            return result?.Status is TemplatedEmailOutboxStatus.Sent or TemplatedEmailOutboxStatus.Queued;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The {Kind} e-mail of mailing list member {MemberId} could not be handed to the outbox.", kind, member.ItemId.SanitizeLogValue());

            return false;
        }
    }

    private async Task<string> VerifyUrlAsync(MailingList list, MailingListMember member)
    {
        if (string.IsNullOrEmpty(member.VerificationNonce))
        {
            return null;
        }

        var token = MailingListVerificationTokens.Create(_tokens, member.ItemId, member.VerificationNonce, _clock.UtcNow, list.ConfirmationLinkLifetime);
        var url = await _urls.GetUrlAsync(MailingListsConstants.RouteNames.Verify, new { token });

        if (url is null)
        {
            _logger.LogWarning("The verification link of mailing list member {MemberId} could not be built: the site has no base URL and no request.", member.ItemId.SanitizeLogValue());
        }

        return url;
    }

    private async Task<MailingListEmailCoupon> CouponAsync(MailingListIssuedCoupon coupon)
    {
        if (coupon is null || string.IsNullOrEmpty(coupon.Code))
        {
            return null;
        }

        string expires = null;

        if (coupon.ExpiresUtc is { } expiresUtc)
        {
            var local = await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(expiresUtc, DateTimeKind.Utc));

            expires = local.ToString("g", CultureInfo.CurrentCulture);
        }

        return new MailingListEmailCoupon
        {
            Code = coupon.Code,
            Url = coupon.Url,
            Description = coupon.Description,
            Expires = expires,
        };
    }

    private static Dictionary<string, string> Values(MailingListEmailLinks links, MailingListEmailCoupon coupon)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VerifyUrl"] = links.Verify,
            ["UnsubscribeUrl"] = links.Unsubscribe,
        };

        if (coupon is not null)
        {
            values["CouponCode"] = coupon.Code;
            values["CouponUrl"] = coupon.Url;
        }

        return values;
    }

    private void Greeting(StringBuilder html, MailingListMember member)
        => Paragraph(html, string.IsNullOrWhiteSpace(member.Contact?.Name) ? S["Hello,"] : S["Hello {0},", member.Contact.Name.Trim()]);

    private void CouponBlock(StringBuilder html, MailingListEmailCoupon coupon, string introduction)
    {
        Paragraph(html, introduction);
        html.Append("<p style=\"font-size:1.25em\"><strong>").Append(HtmlEncoder.Default.Encode(coupon.Code)).Append("</strong></p>");

        if (!string.IsNullOrWhiteSpace(coupon.Description))
        {
            Paragraph(html, coupon.Description);
        }

        if (!string.IsNullOrEmpty(coupon.Expires))
        {
            Paragraph(html, S["The code works until {0}.", coupon.Expires]);
        }

        if (!string.IsNullOrEmpty(coupon.Url))
        {
            Button(html, coupon.Url, S["Use my code"]);
        }
    }

    private void Footer(StringBuilder html, MailingListEmailLinks links)
    {
        if (string.IsNullOrEmpty(links.Unsubscribe))
        {
            return;
        }

        html.Append("<p style=\"font-size:0.85em\"><a href=\"")
            .Append(HtmlEncoder.Default.Encode(links.Unsubscribe))
            .Append("\">")
            .Append(HtmlEncoder.Default.Encode(S["Unsubscribe or change your e-mail preferences"]))
            .Append("</a></p>");
    }

    private static void Paragraph(StringBuilder html, string text)
        => html.Append("<p>").Append(HtmlEncoder.Default.Encode(text ?? string.Empty)).Append("</p>");

    private static void Button(StringBuilder html, string url, string text)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        html.Append("<p><a href=\"")
            .Append(HtmlEncoder.Default.Encode(url))
            .Append("\" style=\"display:inline-block;padding:10px 18px;background:#0d6efd;color:#ffffff;text-decoration:none;border-radius:4px\">")
            .Append(HtmlEncoder.Default.Encode(text))
            .Append("</a></p>");
    }
}
