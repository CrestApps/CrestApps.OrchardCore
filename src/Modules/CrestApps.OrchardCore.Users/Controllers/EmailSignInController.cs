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
/// do the same work, whether or not an account a code can reach has the email, so they never tell who has one: an
/// address without one is counted and checked under a stand-in id (<see cref="EmailSignInCodes.GetStandInId"/>). They
/// are not there when the site turned off local sign-in.
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

        // An address no account a code can reach has gets a code too, under a stand-in id, that is never mailed: the
        // account's daily count and the code's cache entry are the same work either way, so the time taken never tells
        // a known address from an unknown one.
        var account = await FindUserAsync(email);
        var code = await _codes.IssueAsync(account?.UserId ?? _codes.GetStandInId(email), account?.Email ?? email, HttpContext.RequestAborted);

        if (account is not null && code is not null)
        {
            // Sent after the response, so the time the mail server takes never tells a known address from an unknown one.
            // The code is for the account, and goes to the account's own address, never to what was typed.
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

        // An address without an account is checked against its stand-in's code, the same work in the same time. Its
        // check is never a sign-in, whatever it answers.
        var check = await _codes.CheckAsync(found?.UserId ?? _codes.GetStandInId(email), found?.Email ?? email, model.Code, HttpContext.RequestAborted);

        if (check == EmailSignInCodeCheck.Blocked)
        {
            ModelState.AddModelError(nameof(model.Code), S["Too many wrong codes were tried. Sign in with your password, or try again tomorrow."]);

            return View(model);
        }

        // A wrong code and no account read the same.
        if (found is null)
        {
            await _loginFormEvents.InvokeAsync((handler, userName) => handler.LoggingInFailedAsync(userName), email, _logger);

            return CodeRefused(model);
        }

        var user = found.User;

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

    // K4: the choice the code page offers an account without a password that signs in only through external logins or
    // passkeys and whose address isn't proved, posted with the protected step the right code left. Nothing was removed
    // before this.
    [HttpPost("sign-in/email/take-back", Name = "EmailSignInTakeBack")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(EmailSignInCodes.RateLimitGroup)]
    public async Task<IActionResult> TakeBack(EmailSignInViewModel model)
    {
        if (!await EmailSignInAvailability.IsAvailableAsync(_siteService))
        {
            return NotFound();
        }

        // The account must still be the one the code reached, with the address the code went to and unchanged since (the
        // step holds its security stamp, address and sign-in methods), and still be one a code takes back.
        if (_proof.ReadStepUserId(EmailOwnershipProof.TakeBackStep, model.Token) is not { } userId ||
            await _userManager.FindByIdAsync(userId) is not User account ||
            !IsTakeBack(account) ||
            !await _eligibility.AllowsAsync(account))
        {
            return TakeBackRefused(model);
        }

        if (!_proof.IsStepFor(EmailOwnershipProof.TakeBackStep, model.Token, account))
        {
            // B6: a passkey or a login added since the choice was shown keeps the security stamp, and would be removed
            // with the rest without the owner having seen it. The choice is shown again, naming what it removes now.
            if (_proof.RenewStepForChangedCredentials(EmailOwnershipProof.TakeBackStep, model.Token, account) is not { } renewed)
            {
                return TakeBackRefused(model);
            }

            ModelState.AddModelError(string.Empty, S["This account's sign-ins changed since this page opened. Check what is removed now, and choose again."]);

            return TakeBackChoice(account, model, renewed);
        }

        var providers = LoginProviderNames(account);
        // A5: the login policies (Tenant Hierarchy's linked users, for one) are asked first, for the account as it will be
        // once proved, so a refusal leaves its logins, passkeys and sessions as they were.
        if (await EmailOwnershipProof.ValidateLoginAsProvedAsync(account, _loginFormEvents) is { } refusal)
        {
            return refusal;
        }

        var hadPasskeys = UserCredentialsProof.HasPasskeys(account);
        var proof = await _proof.RecordAsync(account, HttpContext.RequestAborted);

        if (proof == EmailProofResult.Failed)
        {
            ModelState.AddModelError(nameof(model.Email), S["That didn't work. Ask for a new code and try again."]);

            return View(nameof(Index), new EmailSignInViewModel { ReturnUrl = model.ReturnUrl });
        }

        if (!string.IsNullOrEmpty(providers))
        {
            await _notifier.InformationAsync(H["Your email address is confirmed, and {0} was removed from this account. Sign in with a code next time, or link {0} again from your account's external logins.", providers]);
        }

        if (hadPasskeys)
        {
            await _notifier.InformationAsync(string.IsNullOrEmpty(providers)
                ? H["Your email address is confirmed, and this account's passkeys were removed. Sign in with a code next time, or add a passkey again from your account's security settings."]
                : H["This account's passkeys were removed too. You can add a passkey again from your account's security settings."]);
        }

        return await CompleteSignInAsync(account, credentialsInDoubt: false, model.ReturnUrl, validated: true);
    }

    // The code proves the email is theirs, by a round trip, which is what lets a user claim what belongs to the address.
    // The login policies are asked first, as the account will be once proved (Orchard's check that the email is confirmed
    // sees it confirmed), and only then is the proof recorded, so a refusal changes nothing (A5).
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
        else if (user is User external && IsTakeBack(external))
        {
            // G5 and K4: an account without a password that signs in only through external logins or passkeys and whose
            // address isn't proved. A code never signs in to one with them kept: that would get around the provider's
            // checks, or let in a passkey whoever registered with the address added. And it may be the owner's own
            // passwordless account, so nothing is removed until they choose to remove them and use their email.
            return TakeBackChoice(external, model, _proof.ProtectStep(EmailOwnershipProof.TakeBackStep, external));
        }
        else if (user is User proved && (!proved.EmailConfirmed || !UserEmailOwnership.IsVerified(proved.Properties, proved.Email)))
        {
            if (await EmailOwnershipProof.ValidateLoginAsProvedAsync(proved, _loginFormEvents) is { } refusal)
            {
                return refusal;
            }

            var hadPasskeys = !UserEmailOwnership.IsVerified(proved.Properties, proved.Email) && UserCredentialsProof.HasPasskeys(proved);
            var proof = await _proof.RecordAsync(proved, HttpContext.RequestAborted);

            if (proof == EmailProofResult.Failed)
            {
                return CodeRefused(model);
            }

            if (proof == EmailProofResult.CredentialsRemoved)
            {
                await _notifier.InformationAsync(H["Your email address is confirmed. The password and other sign-ins added to this account before it was confirmed were removed, because they may not have been yours. Sign in with a code next time, or set a new password with \"Forgot your password?\"."]);
            }

            if (hadPasskeys)
            {
                await _notifier.InformationAsync(H["This account's passkeys were removed too. You can add a passkey again from your account's security settings."]);
            }

            return await CompleteSignInAsync(user, credentialsInDoubt: false, model.ReturnUrl, validated: true);
        }

        return await CompleteSignInAsync(user, credentialsInDoubt, model.ReturnUrl);
    }

    // validated: the login policies were already asked, before the proof was recorded.
    private async Task<IActionResult> CompleteSignInAsync(IUser user, bool credentialsInDoubt, string returnUrl, bool validated = false)
    {
        foreach (var loginFormEvent in validated ? Enumerable.Empty<ILoginFormEvent>() : _loginFormEvents)
        {
            if (await loginFormEvent.ValidatingLoginAsync(user) is { } refusal)
            {
                return refusal;
            }
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        if (credentialsInDoubt)
        {
            // C10: the password or logins may be whoever registered with the owner's address, so every other session and
            // token of the account ends now; theirs too, and the owner's on other devices, who sign in again.
            if (!await _proof.EndOtherSignInsAsync((User)user, HttpContext.RequestAborted))
            {
                ModelState.AddModelError(nameof(EmailSignInViewModel.Email), S["That didn't work. Ask for a new code and try again."]);

                return View(nameof(Index), new EmailSignInViewModel { ReturnUrl = returnUrl });
            }

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

    // An account without a password, with external logins or passkeys, whose address isn't proved, whose credentials
    // could sign in (see EmailOwnershipProof.CredentialsNeverSignedIn; Orchard Core's passkey sign-in runs the same email
    // confirmation check): a code reaches it only to take it back, by the owner's choice.
    private bool IsTakeBack(User account)
        => string.IsNullOrEmpty(account.PasswordHash) &&
            (account.LoginInfos is { Count: > 0 } || UserCredentialsProof.HasPasskeys(account)) &&
            !UserEmailOwnership.IsVerified(account.Properties, account.Email) &&
            !_proof.CredentialsNeverSignedIn(account);

    // The take-back choice, naming what the account signs in with now.
    private ViewResult TakeBackChoice(User account, EmailSignInViewModel model, string token)
        => View("TakeBack", new EmailSignInViewModel
        {
            Email = model.Email,
            ReturnUrl = model.ReturnUrl,
            Token = token,
            LoginProviders = LoginProviderNames(account),
            Passkeys = account.Passkeys?.Count ?? 0,
            SignInMethods = SignInMethodNames(account),
        });

    private ViewResult TakeBackRefused(EmailSignInViewModel model)
    {
        ModelState.AddModelError(nameof(model.Email), S["That took too long, or the account changed. Ask for a new code."]);

        return View(nameof(Index), new EmailSignInViewModel { ReturnUrl = model.ReturnUrl });
    }

    private static string LoginProviderNames(User account)
        => string.Join(", ", (account.LoginInfos ?? []).Select(login => login.ProviderDisplayName ?? login.LoginProvider).Distinct(StringComparer.Ordinal));

    // What the take-back page names: the providers, then the passkeys ("GitHub", "a passkey", "GitHub and 2 passkeys").
    private string SignInMethodNames(User account)
    {
        var providers = LoginProviderNames(account);
        var count = account.Passkeys?.Count ?? 0;

        if (count == 0)
        {
            return providers;
        }

        var passkeys = count == 1 ? S["a passkey"].Value : S["{0} passkeys", count].Value;

        return string.IsNullOrEmpty(providers) ? passkeys : S["{0} and {1}", providers, passkeys].Value;
    }

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
    /// Gets or sets how many passkeys the account has, which taking it back or confirming its address removes.
    /// </summary>
    public int Passkeys { get; set; }

    /// <summary>
    /// Gets or sets what the account signs in with, as the take-back page names it: its providers and its passkeys.
    /// </summary>
    public string SignInMethods { get; set; }

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
