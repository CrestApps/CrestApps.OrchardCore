using System.Globalization;
using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.MailingLists.Core.Coupons;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.MailingLists.WelcomeCoupon;

/// <summary>
/// Issues a list's welcome coupon, once per list and address, ever: at sign-up, or once the address is verified, as the
/// list says. The code is issued under an idempotency key made from the list and a keyed hash of the address's abuse key
/// (never the member id), so unsubscribing and signing up again, or an alias of the same mailbox, returns the code issued
/// before and no second code. A coupon is never given to members added by an admin or an import, or to members the
/// provider reported who never signed up on the site. On a list without confirmation whose coupon waits for
/// verification, it asks the member service to send a claim link instead.
/// </summary>
internal sealed class MailingListWelcomeCouponObserver : MailingListMemberObserverBase
{
    private readonly ICouponIssuer _issuer;
    private readonly MailingListCouponKeyStore _keys;
    private readonly IMailingListProviderResolver _providers;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public MailingListWelcomeCouponObserver(
        ICouponIssuer issuer,
        MailingListCouponKeyStore keys,
        IMailingListProviderResolver providers,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MailingListWelcomeCouponObserver> logger,
        IStringLocalizer<MailingListWelcomeCouponObserver> stringLocalizer)
    {
        _issuer = issuer;
        _keys = keys;
        _providers = providers;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        S = stringLocalizer;
    }

    public override async Task SignedUpAsync(MailingListMemberContext context)
    {
        if (Settings(context) is not { } settings)
        {
            return;
        }

        if (settings.Timing == MailingListCouponTiming.AtSignup && context.List.Confirmation != MailingListConfirmation.Provider)
        {
            await IssueAsync(context, settings, showIssuedBefore: false);

            return;
        }

        // A list without confirmation subscribes at once; the coupon waits for the address to be proven by a claim link.
        if (context.List.Confirmation == MailingListConfirmation.None
            && context.Member.Status == MailingListMemberStatus.Subscribed
            && context.Member.EmailVerifiedUtc is null)
        {
            context.RequestsVerification = true;
        }
    }

    public override async Task VerifiedAsync(MailingListMemberContext context)
    {
        if (Settings(context) is not { } settings)
        {
            return;
        }

        // A code issued at sign-up is shown once the address is proven; one waiting for verification is issued now.
        await IssueAsync(context, settings, showIssuedBefore: true);
    }

    private static MailingListWelcomeCoupon Settings(MailingListMemberContext context)
    {
        if (context.Silent || context.List is null || context.Member is null)
        {
            return null;
        }

        return context.List.TryGet<MailingListWelcomeCoupon>(out var settings) && settings is { IsEnabled: true } && !string.IsNullOrEmpty(settings.CouponId)
            ? settings
            : null;
    }

    private async Task IssueAsync(MailingListMemberContext context, MailingListWelcomeCoupon settings, bool showIssuedBefore)
    {
        var member = context.Member;
        var key = MailingListCouponKeys.Build(context.List.ItemId, member.NormalizedEmail, await _keys.GetKeyAsync());

        if (key is null)
        {
            return;
        }

        // The issuer commits the unit of work inside its lock; the member was saved before the observers ran.
        var result = await _issuer.IssueAsync(new CouponIssueRequest
        {
            CouponId = settings.CouponId,
            Prefix = settings.Prefix,
            CustomerEmail = string.IsNullOrWhiteSpace(member.Contact?.Email) ? member.NormalizedEmail : member.Contact.Email.Trim(),
            ValidFor = settings.ValidForDays is > 0 ? TimeSpan.FromDays(settings.ValidForDays.Value) : null,
            IdempotencyKey = key,
            Source = new RecordRef(MailingListsConstants.MemberRecordKind, member.ItemId),
        });

        if (!result.Succeeded || result.Code is null)
        {
            _logger.LogWarning("The welcome coupon of mailing list {ListId} could not be issued: {Error}", context.List.ItemId.SanitizeLogValue(), result.Error.SanitizeLogValue());

            return;
        }

        var codeId = string.IsNullOrEmpty(result.Code.ItemId) ? result.Code.Code : result.Code.ItemId;
        var mine = string.Equals(member.CouponCodeId, codeId, StringComparison.Ordinal);

        // A repeated key is a code issued before: to this member it is shown again once verified; to anyone else (an
        // alias, or the address signing up again) it is never sent a second time.
        if (result.AlreadyIssued && !(showIssuedBefore && mine))
        {
            return;
        }

        member.CouponCodeId = codeId;
        member.Put(new MailingListMemberCoupon
        {
            Code = result.Code.Code,
            ExpiresUtc = result.Code.ExpiresUtc,
        });

        // The list's provider sends the code itself (its own welcome automation) when the list asks for it and it can. The
        // code reaches the provider with the member's next push, which the member service queues when asked to.
        if (settings.Delivery == MailingListCouponDelivery.Provider
            && _providers.Resolve(context.List) is { } provider
            && provider.Capabilities.HasFlag(MailingListProviderCapabilities.DeliversWelcomeOffer))
        {
            context.RequestsProviderPush = true;

            return;
        }

        context.IssuedCoupon = new MailingListIssuedCoupon
        {
            Code = result.Code.Code,
            Url = await DiscountUrlAsync(result.Code.Code),
            Description = Describe(result.Coupon),
            ExpiresUtc = result.Code.ExpiresUtc,
        };
    }

    private string Describe(Coupon coupon)
    {
        if (coupon is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(coupon.Description))
        {
            return coupon.Description;
        }

        return coupon.Kind switch
        {
            CouponKind.Percentage => S["{0}% off", coupon.Percentage.ToString("0.##", CultureInfo.CurrentCulture)],
            CouponKind.FixedAmount => S["{0} {1} off", coupon.Amount.ToString("0.##", CultureInfo.CurrentCulture), coupon.Currency],
            CouponKind.FreeShipping => S["Free shipping"],
            _ => null,
        };
    }

    // The discount link (discount/{code}) keeps the code for the buyer's next checkout. It is built on the site's base
    // URL, or on the current request when the site has none, because the link is sent by e-mail.
    private async Task<string> DiscountUrlAsync(string code)
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var path = "/discount/" + Uri.EscapeDataString(code);

        if (!string.IsNullOrWhiteSpace(site.BaseUrl))
        {
            return site.BaseUrl.TrimEnd('/') + path;
        }

        var request = _httpContextAccessor.HttpContext?.Request;

        return request is null ? null : $"{request.Scheme}://{request.Host}{request.PathBase}{path}";
    }
}
