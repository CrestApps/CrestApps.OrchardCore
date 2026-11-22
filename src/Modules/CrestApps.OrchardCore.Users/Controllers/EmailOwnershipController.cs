using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Users.Core.Services;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Controllers;

/// <summary>
/// Confirming, with a code e-mailed to the account's address, that the person signed in is the one who controls it.
/// An account that had a password or an external login before anyone proved its address gets nothing that belongs to
/// the address (the guest orders placed with it) until then, because whoever set them, possibly somebody else, could
/// still sign in. Here the owner asks for it: the code is checked, the credentials this request didn't show are removed
/// and every earlier sign-in ends, and only then is the address recorded as proved.
/// </summary>
[Authorize]
[Feature(UsersConstants.Feature.EmailOwnership)]
public sealed class EmailOwnershipController : Controller
{
    private readonly UserManager<IUser> _userManager;
    private readonly SignInManager<IUser> _signInManager;
    private readonly EmailSignInCodes _codes;
    private readonly IEmailSignInCodeSender _codeSender;
    private readonly EmailOwnershipProof _proof;
    private readonly EmailSignInEligibility _eligibility;
    private readonly ISiteService _siteService;
    private readonly IEnumerable<ILoginFormEvent> _loginFormEvents;
    private readonly INotifier _notifier;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public EmailOwnershipController(
        UserManager<IUser> userManager,
        SignInManager<IUser> signInManager,
        EmailSignInCodes codes,
        IEmailSignInCodeSender codeSender,
        EmailOwnershipProof proof,
        EmailSignInEligibility eligibility,
        ISiteService siteService,
        IEnumerable<ILoginFormEvent> loginFormEvents,
        INotifier notifier,
        ILogger<EmailOwnershipController> logger,
        IStringLocalizer<EmailOwnershipController> stringLocalizer,
        IHtmlLocalizer<EmailOwnershipController> htmlLocalizer)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _codes = codes;
        _codeSender = codeSender;
        _proof = proof;
        _eligibility = eligibility;
        _siteService = siteService;
        _loginFormEvents = loginFormEvents;
        _notifier = notifier;
        _logger = logger;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    [HttpGet("sign-in/email/confirm", Name = "EmailSignInConfirm")]
    public async Task<IActionResult> Confirm(string returnUrl = null)
    {
        if (await FindAccountAsync() is not { } account)
        {
            return NotFound();
        }

        if (UserEmailOwnership.IsVerified(account.Properties, account.Email))
        {
            await _notifier.InformationAsync(H["Your email address is already confirmed."]);

            return RedirectToLocal(returnUrl);
        }

        return View(CreateView(account, returnUrl));
    }

    [HttpPost("sign-in/email/confirm")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(EmailSignInCodes.RateLimitGroup)]
    public async Task<IActionResult> Confirm(EmailSignInViewModel model)
    {
        if (await FindAccountAsync() is not { } account)
        {
            return NotFound();
        }

        var keptLogin = _proof.ReadKeptLogin(model.Token, account);

        if (KeptLoginLapsed(account, model, keptLogin) is { } lapsed)
        {
            return lapsed;
        }

        var view = CreateView(account, model.ReturnUrl, keptLogin, model.Token);

        if (!await _codes.TryCountSendAsync(account.Email, ClientNetworkKey.NetworkOnly(HttpContext), HttpContext.RequestAborted))
        {
            ModelState.AddModelError(nameof(model.Email), S["Too many codes were asked for. Wait a few minutes and try again."]);

            return View(view);
        }

        if (await _codes.IssueAsync(account.UserId, account.Email, HttpContext.RequestAborted) is not { } code)
        {
            ModelState.AddModelError(nameof(model.Email), S["Too many codes were asked for today. Try again tomorrow."]);

            return View(view);
        }

        // Mailed to the account's own address, after the response.
        await _codeSender.QueueAsync(account.UserName, account.Email, code, HttpContext.RequestAborted);

        return View(nameof(ConfirmCode), view);
    }

    [HttpGet("sign-in/email/confirm/code", Name = "EmailSignInConfirmCode")]
    public IActionResult ConfirmCode(string returnUrl = null)
        => RedirectToRoute("EmailSignInConfirm", new { returnUrl });

    [HttpPost("sign-in/email/confirm/code")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(EmailSignInCodes.RateLimitGroup)]
    public async Task<IActionResult> ConfirmCode(EmailSignInViewModel model)
    {
        if (await FindAccountAsync() is not { } account)
        {
            return NotFound();
        }

        var keptLogin = _proof.ReadKeptLogin(model.Token, account);

        // A4: the page promised to keep the login the owner signed in through again. When that step expired or the
        // account changed since, confirming would keep nothing and could remove the account's only login, so it stops
        // here, before the code is looked at or anything changes, and asks for the sign-in again.
        if (KeptLoginLapsed(account, model, keptLogin) is { } lapsed)
        {
            return lapsed;
        }

        var view = CreateView(account, model.ReturnUrl, keptLogin, model.Token);

        if (!await _codes.TryCountVerifyAsync(ClientNetworkKey.NetworkOnly(HttpContext), HttpContext.RequestAborted))
        {
            ModelState.AddModelError(nameof(model.Code), S["Too many codes were tried. Wait a few minutes and try again."]);

            return View(view);
        }

        // The mailbox and one credential don't say whose the others are: a password can be set again with the mailbox
        // alone, and a login linked from a code session, while somebody else's stays. So only what this flow shows is
        // kept: the password typed here, and the external login signed in through again at its provider on this page.
        // Nothing is taken from the session's cookie, which can be older than the account's credentials and still be
        // re-issued with the current security stamp (Orchard Core's "Reset authenticator app" refreshes it, K5). A wrong
        // password is refused before the code is looked at, so it never spends the code, and it counts toward the lockout
        // as at sign-in.
        var passwordShown = false;

        if (!string.IsNullOrEmpty(model.Password))
        {
            if (string.IsNullOrEmpty(account.PasswordHash) || !await _userManager.CheckPasswordAsync(account, model.Password))
            {
                await _userManager.AccessFailedAsync(account);
                ModelState.AddModelError(nameof(model.Password), S["That password isn't right. Leave it empty if you don't know it."]);

                return View(view);
            }

            passwordShown = true;
        }

        // A5: confirming signs this browser in again; the login policies (Tenant Hierarchy's linked users, for one) are
        // asked first, for the account as it will be once proved, so a refusal changes nothing and spends no code.
        if (await EmailOwnershipProof.ValidateLoginAsProvedAsync(account, _loginFormEvents) is { } refusal)
        {
            return refusal;
        }

        var check = await _codes.CheckAsync(account.UserId, account.Email, model.Code, HttpContext.RequestAborted);

        if (check == EmailSignInCodeCheck.Blocked)
        {
            ModelState.AddModelError(nameof(model.Code), S["Too many wrong codes were tried. Try again tomorrow."]);

            return View(view);
        }

        if (check == EmailSignInCodeCheck.Wrong)
        {
            // Counts toward the account's lockout, as a wrong password does.
            await _userManager.AccessFailedAsync(account);
        }

        // A first proof removes every passkey: none can be shown here (see EmailOwnershipProof).
        var hadPasskeys = UserCredentialsProof.HasPasskeys(account);
        var proof = check == EmailSignInCodeCheck.Right
            ? await _proof.RecordAsync(account, passwordShown, keptLogin, HttpContext.RequestAborted)
            : EmailProofResult.Failed;

        if (proof == EmailProofResult.Failed)
        {
            ModelState.AddModelError(nameof(model.Code), S["That code isn't right or has expired. Check the latest email, or ask for a new code."]);

            return View(view);
        }

        // A first proof ends every earlier sign-in, this browser's too, whether or not anything was removed, so it is
        // signed in again. Raising the sign-in is what hands over what belongs to the address.
        if (proof is EmailProofResult.CredentialsRemoved or EmailProofResult.SignInsEnded)
        {
            await _signInManager.SignInAsync(account, isPersistent: false);
        }

        await _loginFormEvents.InvokeAsync((handler, signedIn) => handler.LoggedInAsync(signedIn), account, _logger);

        await _notifier.SuccessAsync(ConfirmedMessage(proof, account));

        if (hadPasskeys && proof == EmailProofResult.CredentialsRemoved)
        {
            await _notifier.InformationAsync(H["This account's passkeys were removed too. You can add a passkey again from your account's security settings."]);
        }

        return RedirectToLocal(model.ReturnUrl);
    }

    // What the user is told once the address is confirmed: what the account kept, read from the account as saved.
    private LocalizedHtmlString ConfirmedMessage(EmailProofResult proof, User account)
    {
        if (proof != EmailProofResult.CredentialsRemoved)
        {
            return H["Your email address is confirmed."];
        }

        if (!string.IsNullOrEmpty(account.PasswordHash))
        {
            return H["Your email address is confirmed. You kept this account's password. Its other sign-ins were removed and its other sessions signed out, because they may not have been yours."];
        }

        if (account.LoginInfos is { Count: > 0 } logins)
        {
            return H["Your email address is confirmed. You kept the {0} sign-in you used. This account's password and other sign-ins were removed and its other sessions signed out, because they may not have been yours.", logins[0].ProviderDisplayName ?? logins[0].LoginProvider];
        }

        return H["Your email address is confirmed. The password and other sign-ins this account had before were removed: sign in with a code, or set a new password with \"Forgot your password?\"."];
    }

    // K5: to keep one of the account's external logins, the owner signs in through it again here, and the login whose key
    // the provider returns is the one kept. The challenge is bound to the account (Identity's XSRF check on the external
    // cookie), so a sign-in started for another account is refused.
    [HttpPost("sign-in/email/confirm/login", Name = "EmailSignInConfirmLogin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmLogin(string provider, string returnUrl = null)
    {
        if (await FindAccountAsync() is not { } account ||
            string.IsNullOrEmpty(provider) ||
            account.LoginInfos?.Any(login => string.Equals(login.LoginProvider, provider, StringComparison.Ordinal)) != true)
        {
            return NotFound();
        }

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        var redirectUrl = Url.RouteUrl("EmailSignInConfirmLoginCallback", new { returnUrl });

        return Challenge(_signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, account.UserId), provider);
    }

    [HttpGet("sign-in/email/confirm/login/callback", Name = "EmailSignInConfirmLoginCallback")]
    public async Task<IActionResult> ConfirmLoginCallback(string returnUrl = null, string remoteError = null)
    {
        if (await FindAccountAsync() is not { } account)
        {
            return NotFound();
        }

        var info = remoteError is null ? await _signInManager.GetExternalLoginInfoAsync(account.UserId) : null;

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        if (UserEmailOwnership.IsVerified(account.Properties, account.Email))
        {
            return RedirectToLocal(returnUrl);
        }

        var login = info is null
            ? null
            : account.LoginInfos?.FirstOrDefault(candidate =>
                string.Equals(candidate.LoginProvider, info.LoginProvider, StringComparison.Ordinal) &&
                string.Equals(candidate.ProviderKey, info.ProviderKey, StringComparison.Ordinal));

        if (login is null)
        {
            ModelState.AddModelError(nameof(EmailSignInViewModel.Email), S["That sign-in isn't one of this account's, so it can't be kept. Sign in with the account you use here."]);

            return View(nameof(Confirm), CreateView(account, returnUrl));
        }

        var token = _proof.ProtectStep(EmailOwnershipProof.KeptLoginStep, account, login);

        return View(nameof(Confirm), CreateView(account, returnUrl, login, token));
    }

    // A kept-login step was posted but no longer holds: the confirmation page again, without it, asking for the sign-in.
    private ViewResult KeptLoginLapsed(User account, EmailSignInViewModel model, UserLoginInfo keptLogin)
    {
        if (string.IsNullOrEmpty(model.Token) || keptLogin is not null)
        {
            return null;
        }

        ModelState.AddModelError(nameof(EmailSignInViewModel.Email), S["The sign-in you used again to keep it took too long, or this account changed since, so nothing was changed. To keep it, sign in with it again below; otherwise ask for a code without it."]);

        return View(nameof(Confirm), CreateView(account, model.ReturnUrl));
    }

    // What the confirmation pages show: the external login the owner just signed in through again (kept), the account's
    // other logins they can sign in through to keep instead, whether the account has a password to type, and how many
    // passkeys confirming removes.
    private static EmailSignInViewModel CreateView(User account, string returnUrl, UserLoginInfo keptLogin = null, string token = null)
        => new()
        {
            Email = account.Email,
            ReturnUrl = returnUrl,
            Token = keptLogin is null ? null : token,
            HasCredentials = UserCredentialsProof.HasCredentials(account),
            KeptLoginProvider = keptLogin is null ? null : keptLogin.ProviderDisplayName ?? keptLogin.LoginProvider,
            KeepableLogins = (account.LoginInfos ?? [])
                .DistinctBy(login => login.LoginProvider, StringComparer.Ordinal)
                .ToDictionary(login => login.LoginProvider, login => login.ProviderDisplayName ?? login.LoginProvider, StringComparer.Ordinal),
            AsksPassword = !string.IsNullOrEmpty(account.PasswordHash),
            Passkeys = account.Passkeys?.Count ?? 0,
        };

    // The signed-in account, when a code can confirm its address: the accounts code sign-in serves, and one that signs
    // in only through an external provider, on a site that allows local sign-in.
    private async Task<User> FindAccountAsync()
    {
        if (!await EmailSignInAvailability.IsAvailableAsync(_siteService) ||
            await _userManager.GetUserAsync(User) is not User account ||
            string.IsNullOrWhiteSpace(account.Email) ||
            !await _eligibility.AllowsConfirmationAsync(account))
        {
            return null;
        }

        return account;
    }

    private LocalRedirectResult RedirectToLocal(string returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : LocalRedirect(Url.Content("~/"));
}
