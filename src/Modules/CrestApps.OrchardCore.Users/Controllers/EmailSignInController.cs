using System.Security.Claims;
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
/// Signing in with a code emailed to the account's address, instead of a password. The pages answer the same way, and
/// in the same time, whether or not an account has the email, so they never tell who has one. They are not there when
/// the site turned off local sign-in.
/// </summary>
[AllowAnonymous]
[Feature(UsersConstants.Feature.EmailCodeSignIn)]
public sealed class EmailSignInController : Controller
{
    private static readonly System.ComponentModel.DataAnnotations.EmailAddressAttribute _emailAddress = new();

    private readonly UserManager<IUser> _userManager;
    private readonly SignInManager<IUser> _signInManager;
    private readonly EmailSignInCodes _codes;
    private readonly IEmailSignInCodeSender _codeSender;
    private readonly ISiteService _siteService;
    private readonly IEnumerable<ILoginFormEvent> _loginFormEvents;
    private readonly EmailOwnershipProof _proof;
    private readonly INotifier _notifier;
    private readonly EmailSignInEligibility _eligibility;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public EmailSignInController(
        UserManager<IUser> userManager,
        SignInManager<IUser> signInManager,
        EmailSignInCodes codes,
        IEmailSignInCodeSender codeSender,
        ISiteService siteService,
        IEnumerable<ILoginFormEvent> loginFormEvents,
        EmailOwnershipProof proof,
        INotifier notifier,
        EmailSignInEligibility eligibility,
        ILogger<EmailSignInController> logger,
        IStringLocalizer<EmailSignInController> stringLocalizer,
        IHtmlLocalizer<EmailSignInController> htmlLocalizer)
    {
        _loginFormEvents = loginFormEvents;
        _proof = proof;
        _notifier = notifier;
        _eligibility = eligibility;
        _userManager = userManager;
        _signInManager = signInManager;
        _codes = codes;
        _codeSender = codeSender;
        _siteService = siteService;
        _logger = logger;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    [HttpGet("sign-in/email", Name = "EmailSignIn")]
    public async Task<IActionResult> Index(string returnUrl = null)
    {
        if (!await EmailSignInAvailability.IsAvailableAsync(_siteService))
        {
            return NotFound();
        }

        return View(new EmailSignInViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("sign-in/email")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(EmailSignInCodes.RateLimitGroup)]
    public async Task<IActionResult> Index(EmailSignInViewModel model)
    {
        if (!await EmailSignInAvailability.IsAvailableAsync(_siteService))
        {
            return NotFound();
        }

        var email = model.Email?.Trim();

        if (string.IsNullOrEmpty(email) || !_emailAddress.IsValid(email))
        {
            ModelState.AddModelError(nameof(model.Email), S["Enter the email of your account."]);

            return View(model);
        }

        if (!await _codes.TryCountSendAsync(email, ClientNetworkKey.NetworkOnly(HttpContext), HttpContext.RequestAborted))
        {
            ModelState.AddModelError(nameof(model.Email), S["Too many codes were asked for. Wait a few minutes, or sign in with your password."]);

            return View(model);
        }

        if (await FindUserAsync(email) is { } account)
        {
            // Sent after the response, so the time the mail server takes never tells a known address from an unknown one.
            // The code is for the account, and goes to the account's own address, never to what was typed.
            var code = await _codes.IssueAsync(account.UserId, account.Email, HttpContext.RequestAborted);

            await _codeSender.QueueAsync(account.User.UserName, account.Email, code, HttpContext.RequestAborted);
        }

        // The code form is shown straight away rather than through a redirect, so the email never goes in a URL.
        return View(nameof(Code), new EmailSignInViewModel { Email = email, ReturnUrl = model.ReturnUrl });
    }

    [HttpGet("sign-in/email/code", Name = "EmailSignInCode")]
    public IActionResult Code(string returnUrl = null)
        => RedirectToRoute("EmailSignIn", new { returnUrl });

    [HttpPost("sign-in/email/code")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(EmailSignInCodes.RateLimitGroup)]
    public async Task<IActionResult> Code(EmailSignInViewModel model)
    {
        if (!await EmailSignInAvailability.IsAvailableAsync(_siteService))
        {
            return NotFound();
        }

        var email = model.Email?.Trim();

        // The events a password sign-in raises after its form is posted, so audit, lockout and any other hook sees this
        // sign-in. LoggingInAsync is not raised: its handlers check what the password form carries (reCAPTCHA's widget
        // among them), which this form has none of, so with reCAPTCHA on every code was refused. Guessing is limited by
        // the code's five tries, the checks allowed per network and the account's lockout instead.
        if (!await _codes.TryCountVerifyAsync(ClientNetworkKey.NetworkOnly(HttpContext), HttpContext.RequestAborted))
        {
            ModelState.AddModelError(nameof(model.Code), S["Too many codes were tried. Wait a few minutes, or sign in with your password."]);

            return View(model);
        }

        var found = string.IsNullOrEmpty(email) ? null : await FindUserAsync(email);

        // A wrong code and no account read the same.
        if (found is null)
        {
            await _loginFormEvents.InvokeAsync((handler, userName) => handler.LoggingInFailedAsync(userName), email, _logger);

            return CodeRefused(model);
        }

        var user = found.User;
        var check = await _codes.CheckAsync(found.UserId, found.Email, model.Code, HttpContext.RequestAborted);

        if (check != EmailSignInCodeCheck.Right)
        {
            // A wrong code counts toward the account's lockout, as a wrong password does, so guessing stops for good once
            // the lockout threshold is reached instead of resuming with every new code. Only a live code that was
            // compared counts: digits posted for an address no code was sent to would otherwise lock anyone out.
            if (check == EmailSignInCodeCheck.Wrong)
            {
                await _userManager.AccessFailedAsync(user);

                if (await _userManager.IsLockedOutAsync(user))
                {
                    await _loginFormEvents.InvokeAsync((handler, lockedOut) => handler.IsLockedOutAsync(lockedOut), user, _logger);
                }
            }

            await _loginFormEvents.InvokeAsync((handler, failed) => handler.LoggingInFailedAsync(failed), user, _logger);

            return CodeRefused(model);
        }

        return await SignInWithRightCodeAsync(user, model);
    }

    // K4: the choice the code page offers an account that signs in only through external logins and whose address isn't
    // proved, posted with the protected step the right code left. Nothing was removed before this.
    [HttpPost("sign-in/email/take-back", Name = "EmailSignInTakeBack")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(EmailSignInCodes.RateLimitGroup)]
    public async Task<IActionResult> TakeBack(EmailSignInViewModel model)
    {
        if (!await EmailSignInAvailability.IsAvailableAsync(_siteService))
        {
            return NotFound();
        }

        // The account must still be the one the code reached, unchanged since (the step holds its security stamp), and
        // still be one a code takes back.
        if (_proof.ReadStepUserId(EmailOwnershipProof.TakeBackStep, model.Token) is not { } userId ||
            await _userManager.FindByIdAsync(userId) is not User account ||
            !_proof.IsStepFor(EmailOwnershipProof.TakeBackStep, model.Token, account) ||
            !IsExternalOnlyTakeBack(account) ||
            !await _eligibility.AllowsAsync(account))
        {
            ModelState.AddModelError(nameof(model.Email), S["That took too long, or the account changed. Ask for a new code."]);

            return View(nameof(Index), new EmailSignInViewModel { ReturnUrl = model.ReturnUrl });
        }

        var providers = LoginProviderNames(account);
        var proof = await _proof.RecordAsync(account, HttpContext.RequestAborted);

        if (proof == EmailProofResult.Failed)
        {
            ModelState.AddModelError(nameof(model.Email), S["That didn't work. Ask for a new code and try again."]);

            return View(nameof(Index), new EmailSignInViewModel { ReturnUrl = model.ReturnUrl });
        }

        await _notifier.InformationAsync(H["Your email address is confirmed, and {0} was removed from this account. Sign in with a code next time, or link {0} again from your account's external logins.", providers]);

        return await CompleteSignInAsync(account, credentialsInDoubt: false, model.ReturnUrl);
    }

    // The code proves the email is theirs, by a round trip, which is what lets a user claim what belongs to the address.
    // It is recorded before the login is validated, so Orchard's check that the email is confirmed sees it confirmed.
    private async Task<IActionResult> SignInWithRightCodeAsync(IUser user, EmailSignInViewModel model)
    {
        var credentialsInDoubt = false;

        // On a site that requires confirmed addresses, though, Orchard refuses every sign-in to an unconfirmed account, so
        // its password and logins never signed anyone in, and the owner couldn't reset the password either: that account
        // is proved below like one with no credentials, which removes them.
        if (user is User account &&
            !UserEmailOwnership.IsVerified(account.Properties, account.Email) &&
            !string.IsNullOrEmpty(account.PasswordHash) &&
            !_proof.CredentialsNeverSignedIn(account))
        {
            // A password or an external login set before anyone proved the address may be somebody else's, and recording
            // the proof hands over what belongs to the address, so it would have to remove them. A code signs the owner in
            // with nothing removed, recorded or confirmed (confirming would let that password in where confirmation is
            // required); the owner confirms it is theirs, with a second code, when they choose to. The session is marked as
            // begun with an e-mailed code, so every page tells them so until then (EmailOwnershipNoticeFilter).
            credentialsInDoubt = true;
        }
        else if (user is User external && IsExternalOnlyTakeBack(external))
        {
            // G5 and K4: an account that signs in only through external logins and whose address isn't proved. A code never
            // signs in to one with its logins kept, which would get around the provider's checks, and it may be the owner's
            // own passwordless account, so nothing is removed until they choose to remove the logins and use their email.
            return View("TakeBack", new EmailSignInViewModel
            {
                Email = model.Email,
                ReturnUrl = model.ReturnUrl,
                Token = _proof.ProtectStep(EmailOwnershipProof.TakeBackStep, external),
                LoginProviders = LoginProviderNames(external),
            });
        }
        else if (user is User proved && (!proved.EmailConfirmed || !UserEmailOwnership.IsVerified(proved.Properties, proved.Email)))
        {
            var proof = await _proof.RecordAsync(proved, HttpContext.RequestAborted);

            if (proof == EmailProofResult.Failed)
            {
                return CodeRefused(model);
            }

            if (proof == EmailProofResult.CredentialsRemoved)
            {
                await _notifier.InformationAsync(H["Your email address is confirmed. The password and other sign-ins added to this account before it was confirmed were removed, because they may not have been yours. Sign in with a code next time, or set a new password with \"Forgot your password?\"."]);
            }
        }

        return await CompleteSignInAsync(user, credentialsInDoubt, model.ReturnUrl);
    }

    private async Task<IActionResult> CompleteSignInAsync(IUser user, bool credentialsInDoubt, string returnUrl)
    {
        foreach (var loginFormEvent in _loginFormEvents)
        {
            if (await loginFormEvent.ValidatingLoginAsync(user) is { } refusal)
            {
                return refusal;
            }
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        if (credentialsInDoubt)
        {
            // What the account's credentials were at this sign-in goes with the session, so a login linked or removed from
            // it later marks the account (EmailOwnershipUserEventHandler). Its pages warn, so the sign-in notice is skipped.
            EmailOwnershipLoginFormEvent.MarkCodeSignIn(HttpContext);
            await _signInManager.SignInWithClaimsAsync(user, isPersistent: false,
            [
                new Claim(UserCredentialsProof.AuthenticationMethodReferenceClaim, UserCredentialsProof.EmailedCodeMethod),
                new Claim(UserCredentialsProof.CodeSessionCredentialsClaim, UserCredentialsProof.CredentialsFingerprint((User)user)),
            ]);
        }
        else
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
        }

        await _loginFormEvents.InvokeAsync((handler, signedIn) => handler.LoggedInAsync(signedIn), user, _logger);

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : LocalRedirect(Url.Content("~/"));
    }

    // An account with only external logins, whose address isn't proved, whose logins could sign in (see
    // EmailOwnershipProof.CredentialsNeverSignedIn): a code reaches it only to take it back, by the owner's choice.
    private bool IsExternalOnlyTakeBack(User account)
        => string.IsNullOrEmpty(account.PasswordHash) &&
            account.LoginInfos is { Count: > 0 } &&
            !UserEmailOwnership.IsVerified(account.Properties, account.Email) &&
            !_proof.CredentialsNeverSignedIn(account);

    private static string LoginProviderNames(User account)
        => string.Join(", ", (account.LoginInfos ?? []).Select(login => login.ProviderDisplayName ?? login.LoginProvider).Distinct(StringComparer.Ordinal));

    // An account a code can sign in to, with its id and its own address.
    private sealed record CodeAccount(IUser User, string UserId, string Email);

    private ViewResult CodeRefused(EmailSignInViewModel model)
    {
        ModelState.AddModelError(nameof(model.Code), S["That code isn't right or has expired. Check the latest email, or ask for a new code."]);

        return View(model);
    }

    // An account that can sign in with a code (see EmailSignInEligibility). The typed address must be the account's own: a database that compares text loosely (SQL Server's default
    // collation treats AE as Æ, TH as Þ and SS as ß) answers a look-alike address with another account.
    private async Task<CodeAccount> FindUserAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null ||
            await _userManager.GetEmailAsync(user) is not { } accountEmail ||
            !IsSameAddress(email, accountEmail) ||
            !await _eligibility.AllowsAsync(user))
        {
            return null;
        }

        return new CodeAccount(user, await _userManager.GetUserIdAsync(user), accountEmail);
    }

    // Compared as Orchard Core normalizes an address for its lookup, but exactly.
    private static bool IsSameAddress(string typed, string accountEmail)
        => !string.IsNullOrWhiteSpace(accountEmail) &&
            string.Equals(typed?.Trim().Normalize().ToUpperInvariant(), accountEmail.Trim().Normalize().ToUpperInvariant(), StringComparison.Ordinal);
}

public class EmailSignInViewModel
{
    public string Email { get; set; }

    public string Code { get; set; }

    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets what the previous step established, protected (see <see cref="EmailOwnershipProof.ProtectStep"/>):
    /// a right code for an account a code takes back, or a fresh sign-in through one of the account's external logins.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the names of the providers the account signs in with.
    /// </summary>
    public string LoginProviders { get; set; }

    /// <summary>
    /// Gets or sets the account's password, typed on the confirmation page by an owner who knows it, so it is kept.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account has a password or an external login.
    /// </summary>
    public bool HasCredentials { get; set; }

    /// <summary>
    /// Gets or sets the name of the provider of the external login the owner just signed in through again on the
    /// confirmation page, which confirming keeps; or <see langword="null"/>.
    /// </summary>
    public string KeptLoginProvider { get; set; }

    /// <summary>
    /// Gets or sets the account's login providers, by scheme, with their names: signing in through one again on the
    /// confirmation page keeps that login.
    /// </summary>
    public IReadOnlyDictionary<string, string> KeepableLogins { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets a value indicating whether the confirmation page offers the password field.
    /// </summary>
    public bool AsksPassword { get; set; }
}
