using CrestApps.OrchardCore.Users.Controllers;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Filters;

/// <summary>
/// Tells a user whose account's address isn't proved, and has a password or an external login their session does not
/// show (<see cref="UserCredentialsProof.HasCredentialsNotShown"/>, which counts a credential as shown only while the
/// session's cookie carries the account's current security stamp), on every page they open, that somebody else may hold
/// them, and how to remove them: "Confirm it's you". Whoever registered with the address, or changed their email to it,
/// could otherwise keep reading what the owner adds to the account. It is decided on the account, not on how the
/// session began, so it stays after the owner links or removes a login, which signs the browser in again. It stops once
/// the address is proved.
/// </summary>
/// <remarks>
/// It reads the account only on a page (a GET that renders a view) of a session marked as one to an account with
/// credentials in doubt (<see cref="UserCredentialsProof.CredentialsInDoubtClaim"/>, added when the session's claims are
/// built, see <see cref="Services.EmailOwnershipClaimsProvider"/>) or begun with an e-mailed code: one user read per
/// such page. Other sessions and other requests cost nothing. The notice goes through Orchard Core's notifier, which
/// runs on the result after this.
/// </remarks>
public sealed class EmailOwnershipNoticeFilter : IAsyncActionFilter
{
    private readonly UserManager<IUser> _userManager;
    private readonly IEmailOwnershipConfirmationPage _confirmationPage;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public EmailOwnershipNoticeFilter(
        UserManager<IUser> userManager,
        IEmailOwnershipConfirmationPage confirmationPage,
        INotifier notifier,
        IHtmlLocalizer<EmailOwnershipNoticeFilter> htmlLocalizer)
    {
        _userManager = userManager;
        _confirmationPage = confirmationPage;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var executed = await next();
        var httpContext = context.HttpContext;

        if (executed.Result is not ViewResult ||
            !HttpMethods.IsGet(httpContext.Request.Method) ||
            context.Controller is EmailOwnershipController or EmailSignInController ||
            !UserCredentialsProof.MayHaveCredentialsInDoubt(httpContext.User) ||
            await _userManager.GetUserAsync(httpContext.User) is not User account ||
            !UserCredentialsProof.HasCredentialsNotShown(httpContext.User, account) ||
            await _confirmationPage.GetUrlAsync(account, httpContext.Request.PathBase + httpContext.Request.Path + httpContext.Request.QueryString, httpContext.RequestAborted) is not { } url)
        {
            return;
        }

        await _notifier.WarningAsync(H["This account has a password or other sign-ins that were added before this email address was confirmed as yours. If you didn't add them, <a href=\"{0}\">confirm it's you</a> to remove them.", url]);
    }
}
