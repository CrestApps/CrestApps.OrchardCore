using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Tells a user, once at sign-in, that their account's address isn't proved and it has more than one way to sign in (a
/// password, external logins and passkeys, counted each), some of which somebody else may have added, with the link to
/// "Confirm it's you". That is every account with both a password and a login until its address is proved, which is
/// why it is said at sign-in and not on every page: only a session an e-mailed code began, and every session of an
/// account whose credentials changed in such a session, are told on every page (<see cref="Filters.EmailOwnershipNoticeFilter"/>),
/// and they are not told here too.
/// </summary>

public sealed class EmailOwnershipLoginFormEvent : LoginFormEventBase
{
    private const string CodeSignInItem = "CrestApps.EmailOwnership.CodeSignIn";

    private const string EmailedCodeSignInItem = "CrestApps.EmailOwnership.EmailedCodeSignIn";

    private readonly IEmailOwnershipConfirmationPage _confirmationPage;
    private readonly INotifier _notifier;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    internal readonly IHtmlLocalizer H;

    public EmailOwnershipLoginFormEvent(
        IEmailOwnershipConfirmationPage confirmationPage,
        INotifier notifier,
        IHttpContextAccessor httpContextAccessor,
        IHtmlLocalizer<EmailOwnershipLoginFormEvent> htmlLocalizer,
        ILogger<EmailOwnershipLoginFormEvent> logger)
    {
        _confirmationPage = confirmationPage;
        _notifier = notifier;
        _httpContextAccessor = httpContextAccessor;
        H = htmlLocalizer;
        _logger = logger;
    }

    /// <summary>
    /// Marks <paramref name="httpContext"/>'s request as a sign-in with an e-mailed code that leaves the account's
    /// credentials in place: its pages warn from now on, so the sign-in notice is not said too.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <summary>
    /// Refuses a sign-in with a password, an external login or a passkey while the account's unproved credentials are
    /// held (<see cref="UserCredentialsProof.AreUnprovedCredentialsHeld"/>): the owner of the address signed in with a code
    /// and hasn't confirmed the address yet, so those credentials may be whoever registered with it. A sign-in with an
    /// e-mailed code goes ahead; confirming the address, with the password typed on that page if it is the owner's, ends
    /// the hold.
    /// </summary>
    /// <param name="user">The user signing in.</param>
    public override async Task<IActionResult> ValidatingLoginAsync(IUser user)
    {
        if (user is not User account ||
            !UserCredentialsProof.AreUnprovedCredentialsHeld(account) ||
            _httpContextAccessor.HttpContext?.Items.ContainsKey(EmailedCodeSignInItem) == true)
        {
            return null;
        }

        await _notifier.WarningAsync(H["This account's password and other sign-ins are paused until its owner confirms the email address. Sign in with a code e-mailed to the account, then confirm it's you; type the password there to keep it."]);

        return new RedirectToRouteResult("EmailSignIn", null);
    }

    /// <summary>
    /// Marks the request as a sign-in with an e-mailed code, which held credentials don't stop.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    public static void MarkEmailedCodeSignIn(HttpContext httpContext)
    {
        if (httpContext is not null)
        {
            httpContext.Items[EmailedCodeSignInItem] = true;
        }
    }

    public static void MarkCodeSignIn(HttpContext httpContext)
    {
        if (httpContext is not null)
        {
            httpContext.Items[CodeSignInItem] = true;
        }
    }

    /// <inheritdoc/>
    public override async Task LoggedInAsync(IUser user)
    {
        if (user is not User account ||
            UserCredentialsProof.CountCredentials(account) < 2 ||
            !UserCredentialsProof.HasCredentialsInDoubt(account) ||
            UserCredentialsProof.ChangedInCodeSession(account) ||
            _httpContextAccessor.HttpContext?.Items.ContainsKey(CodeSignInItem) == true)
        {
            return;
        }

        try
        {
            if (await _confirmationPage.GetUrlAsync(account) is not { } url)
            {
                return;
            }

            await _notifier.WarningAsync(H["This account has a password or other sign-ins that were added before this email address was confirmed as yours. If you didn't add them, <a href=\"{0}\">confirm it's you</a> to remove them.", url]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not tell a user at sign-in that their account has sign-ins added before its email address was confirmed.");
        }
    }
}
