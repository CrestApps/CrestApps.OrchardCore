using System.Security.Claims;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.PublicForms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.MailingLists.SignupWidget;

/// <summary>
/// Signs a visitor up from a sign-up widget, with or without a login. It answers JSON to the widget's script and sends a
/// visitor without script back to the page with the outcome in the query string. The visitor reads the same message
/// whether the address was new, already subscribed or refused, so the form can't tell who is on a list.
/// </summary>
[Feature(MailingListsConstants.Features.SignupWidget)]
public sealed class MailingListSignupController : Controller
{
    internal const string FormQueryName = "mailingListSignup";
    internal const string ResultQueryName = "mailingListResult";
    internal const string SucceededCode = "ok";

    private const string InvalidCode = "invalid";
    private const string ChallengeCode = "challenge";
    private const string ChooseCode = "choose";
    private const int MaxNameLength = 200;

    // A sign-up carries a few short fields.
    private const long MaxRequestBytes = 16 * 1024;

    private readonly IContentManager _contentManager;
    private readonly IMailingListManager _lists;
    private readonly IMailingListMemberService _members;
    private readonly IEnumerable<IPublicFormChallenge> _challenges;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListSignupController"/> class.
    /// </summary>
    public MailingListSignupController(
        IContentManager contentManager,
        IMailingListManager lists,
        IMailingListMemberService members,
        IEnumerable<IPublicFormChallenge> challenges,
        IAuthorizationService authorizationService,
        ILogger<MailingListSignupController> logger,
        IStringLocalizer<MailingListSignupController> stringLocalizer)
    {
        _contentManager = contentManager;
        _lists = lists;
        _members = members;
        _challenges = challenges;
        _authorizationService = authorizationService;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Signs the visitor up to the lists the widget offers and the visitor chose.
    /// </summary>
    /// <param name="widgetId">The widget.</param>
    /// <param name="email">The address.</param>
    /// <param name="name">The person's name, when the form asks for it.</param>
    /// <param name="listIds">The lists the visitor chose, when the widget offers several.</param>
    /// <param name="returnUrl">The page to go back to.</param>
    /// <returns>JSON for the widget's script, or a redirect back to the page.</returns>
    [AllowAnonymous]
    [HttpPost("mailing-lists/subscribe/{widgetId}", Name = MailingListsConstants.RouteNames.Subscribe)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RateLimitGroup(MailingListsConstants.RateLimitGroups.Signup)]
    public async Task<IActionResult> Subscribe(string widgetId, string email, string name, string[] listIds, string returnUrl)
    {
        var back = Url.IsLocalUrl(returnUrl) ? returnUrl : "~/";

        if (string.IsNullOrEmpty(widgetId)
            || await _contentManager.GetAsync(widgetId, VersionOptions.Published) is not { } widget
            || !widget.TryGet<MailingListSignupPart>(out var part)
            || !await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, widget))
        {
            // A form the visitor can't see, or one no longer published, takes no sign-ups.
            return NotFound();
        }

        // A filled trap is answered as if it worked, so a robot learns nothing.
        if (PublicFormTrap.IsTrapped(Request.Form[PublicFormTrap.FieldName]))
        {
            return Answer(part, back, widgetId, SucceededCode, coupon: null);
        }

        foreach (var challenge in _challenges.Where(challenge => challenge.IsConfigured))
        {
            if (!await challenge.VerifyAsync())
            {
                return Answer(part, back, widgetId, ChallengeCode, coupon: null);
            }
        }

        if (!EmailAddresses.IsValid(email))
        {
            return Answer(part, back, widgetId, InvalidCode, coupon: null);
        }

        var offered = part.ListIds ?? [];
        var chosen = offered.Length == 1
            ? offered
            : offered.Intersect(listIds ?? [], StringComparer.Ordinal).ToArray();

        if (chosen.Length == 0)
        {
            return Answer(part, back, widgetId, ChooseCode, coupon: null);
        }

        var userId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        var verifiedByAccount = userId is not null && await IsAccountEmailAsync(email);
        var trimmedName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

        if (trimmedName?.Length > MaxNameLength)
        {
            trimmedName = trimmedName[..MaxNameLength];
        }

        MailingListIssuedCoupon coupon = null;

        foreach (var listId in chosen)
        {
            if (await _lists.FindByIdAsync(listId, HttpContext.RequestAborted) is not { IsEnabled: true })
            {
                continue;
            }

            try
            {
                var result = await _members.SubscribeAsync(new MailingListSubscribeRequest
                {
                    ListId = listId,
                    Email = email,
                    Name = part.AskForName ? trimmedName : null,
                    Source = MailingListMemberSources.Widget,
                    SourceId = widgetId,
                    UserId = userId,
                    EmailVerifiedByAccount = verifiedByAccount,
                    ClientAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ConsentForm = Url.Content(back),
                }, HttpContext.RequestAborted);

                coupon ??= result.Coupon;

                if (!result.Succeeded && _logger.IsEnabled(LogLevel.Information))
                {
                    // Never shown to the visitor: the answer is the same as for a sign-up that went through.
                    _logger.LogInformation("A sign-up from widget {WidgetId} to mailing list {ListId} was refused: {Error}", widgetId.SanitizeLogValue(), listId.SanitizeLogValue(), result.Error.SanitizeLogValue());
                }
            }
            catch (TimeoutException)
            {
                // Another post for the same address is running; it will answer the visitor.
            }
        }

        return Answer(part, back, widgetId, SucceededCode, coupon);
    }

    /// <summary>
    /// Returns the message a visitor reads after a sign-up that went through (or looked like it did).
    /// </summary>
    /// <param name="part">The widget.</param>
    /// <param name="S">The localizer.</param>
    /// <returns>The message.</returns>
    internal static string SuccessMessage(MailingListSignupPart part, IStringLocalizer S)
        => string.IsNullOrWhiteSpace(part?.SuccessMessage)
            ? S["Thank you! If your address needs to be confirmed, we sent you an e-mail with a link."].Value
            : part.SuccessMessage;

    /// <summary>
    /// Describes an outcome code for the visitor.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="S">The localizer.</param>
    /// <returns>The message, or <see langword="null"/>.</returns>
    internal static string Describe(string code, IStringLocalizer S)
        => code switch
        {
            InvalidCode => S["Please enter a valid e-mail address."],
            ChallengeCode => S["Please confirm you're not a robot and send it again."],
            ChooseCode => S["Please choose at least one list."],
            _ => null,
        };

    private IActionResult Answer(MailingListSignupPart part, string back, string widgetId, string code, MailingListIssuedCoupon coupon)
    {
        var succeeded = string.Equals(code, SucceededCode, StringComparison.Ordinal);
        var message = succeeded ? SuccessMessage(part, S) : Describe(code, S);

        if (Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        {
            return Json(new
            {
                succeeded,
                message,
                coupon = coupon is null ? null : new { code = coupon.Code, url = coupon.Url, description = coupon.Description },
            });
        }

        var target = Url.Content(back);
        var separator = target.Contains('?') ? '&' : '?';

        return Redirect($"{target}{separator}{FormQueryName}={Uri.EscapeDataString(widgetId)}&{ResultQueryName}={Uri.EscapeDataString(code)}#mailing-list-signup-{widgetId}");
    }

    private async Task<bool> IsAccountEmailAsync(string email)
    {
        // Users may be off: then no account can prove the address.
        if (HttpContext.RequestServices.GetService<UserManager<IUser>>() is not { } userManager
            || await userManager.GetUserAsync(User) is not { } user)
        {
            return false;
        }

        return await userManager.IsEmailConfirmedAsync(user)
            && string.Equals(EmailAddresses.Normalize(await userManager.GetEmailAsync(user)), EmailAddresses.Normalize(email), StringComparison.Ordinal);
    }
}
