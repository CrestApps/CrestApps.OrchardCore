using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Tells a user, once at sign-in, that their account's address isn't proved and it has more than one way to sign in (a
/// password and an external login, or several logins), some of which somebody else may have added, with the link to
/// "Confirm it's you". That is every account with both a password and a login until its address is proved, which is
/// why it is said at sign-in and not on every page: only a session an e-mailed code began, and every session of an
/// account whose credentials changed in such a session, are told on every page (<see cref="Filters.EmailOwnershipNoticeFilter"/>),
/// and they are not told here too.
/// </summary>

public sealed class EmailOwnershipLoginFormEvent : LoginFormEventBase
{
    private const string CodeSignInItem = "CrestApps.EmailOwnership.CodeSignIn";

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
