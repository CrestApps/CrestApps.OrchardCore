using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Users.Core.Services;
using CrestApps.OrchardCore.Users.Services;
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
/// An account that had a password or an external login before anyone proved its address is signed in by a code without
/// that proof being recorded, because recording it would hand over what belongs to the address (the guest orders placed
/// with it) while whoever set that password, possibly somebody else, could still sign in. Here the owner asks for it:
/// the code is checked, the earlier password and external logins are removed and every earlier sign-in ends, and only
/// then is the address recorded as proved.
/// </summary>
[Authorize]
[Feature(UsersConstants.Feature.EmailCodeSignIn)]
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

        var view = CreateView(account, model.ReturnUrl);

        if (!await _codes.TryCountSendAsync(account.Email, ClientNetworkKey.NetworkOnly(HttpContext), HttpContext.RequestAborted))
        {
            ModelState.AddModelError(nameof(model.Email), S["Too many codes were asked for. Wait a few minutes and try again."]);

            return View(view);
        }

        // Mailed to the account's own address, after the response.
        await _codeSender.QueueAsync(account.UserName, account.Email, await _codes.IssueAsync(account.UserId, account.Email, HttpContext.RequestAborted), HttpContext.RequestAborted);

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

        var view = CreateView(account, model.ReturnUrl);

        if (!await _codes.TryCountVerifyAsync(ClientNetworkKey.NetworkOnly(HttpContext), HttpContext.RequestAborted))
        {
            ModelState.AddModelError(nameof(model.Code), S["Too many codes were tried. Wait a few minutes and try again."]);

            return View(view);
        }

        // The mailbox alone doesn't say whose the account's password and external logins are. They are kept when this
        // request also shows them: signed in with them, or the password typed here. A wrong password is refused before
        // the code is looked at, so it never spends the code, and it counts toward the lockout as at sign-in.
        var credentialsShown = view.CredentialsShown;

        if (!credentialsShown && !string.IsNullOrEmpty(model.Password))
        {
            if (string.IsNullOrEmpty(account.PasswordHash) || !await _userManager.CheckPasswordAsync(account, model.Password))
            {
                await _userManager.AccessFailedAsync(account);
                ModelState.AddModelError(nameof(model.Password), S["That password isn't right. Leave it empty if you don't know it."]);

                return View(view);
            }

            credentialsShown = true;
        }

        var check = await _codes.CheckAsync(account.UserId, account.Email, model.Code, HttpContext.RequestAborted);

        if (check == EmailSignInCodeCheck.Wrong)
        {
            // Counts toward the account's lockout, as a wrong password does.
            await _userManager.AccessFailedAsync(account);
        }

        var proof = check == EmailSignInCodeCheck.Right
            ? await _proof.RecordAsync(account, credentialsShown, HttpContext.RequestAborted)
            : EmailProofResult.Failed;

        if (proof == EmailProofResult.Failed)
        {
            ModelState.AddModelError(nameof(model.Code), S["That code isn't right or has expired. Check the latest email, or ask for a new code."]);

            return View(view);
        }

        // When the credentials were removed, every earlier sign-in ended with the proof, this browser's too, so it is
        // signed in again. Raising the sign-in is what hands over what belongs to the address.
        if (proof == EmailProofResult.CredentialsRemoved)
        {
            await _signInManager.SignInAsync(account, isPersistent: false);
        }

        await _loginFormEvents.InvokeAsync((handler, signedIn) => handler.LoggedInAsync(signedIn), account, _logger);

        await _notifier.SuccessAsync(proof == EmailProofResult.CredentialsRemoved
            ? H["Your email address is confirmed. The password and other sign-ins this account had before were removed: sign in with a code, or set a new password with \"Forgot your password?\"."]
            : H["Your email address is confirmed."]);

        return RedirectToLocal(model.ReturnUrl);
    }

    // What the confirmation pages show: whether this request already shows the account's credentials, and whether the
    // account has a password to type instead.
    private EmailSignInViewModel CreateView(User account, string returnUrl)
    {
        var credentialsShown = UserCredentialsProof.ShowsCredentials(User, account);

        return new EmailSignInViewModel
        {
            Email = account.Email,
            ReturnUrl = returnUrl,
            HasCredentials = UserCredentialsProof.HasCredentials(account),
            CredentialsShown = credentialsShown,
            AsksPassword = !credentialsShown && !string.IsNullOrEmpty(account.PasswordHash),
        };
    }

    // The signed-in account, when a code can confirm its address: the same accounts code sign-in serves, on a site that
    // allows local sign-in.
    private async Task<User> FindAccountAsync()
    {
        if (!await EmailSignInAvailability.IsAvailableAsync(_siteService) ||
            await _userManager.GetUserAsync(User) is not User account ||
            string.IsNullOrWhiteSpace(account.Email) ||
            !await _eligibility.AllowsAsync(account))
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
