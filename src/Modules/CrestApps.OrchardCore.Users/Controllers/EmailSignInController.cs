using CrestApps.OrchardCore.Core.Throttling;
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

        // The code proves the email is theirs, by a round trip, which is what lets a user claim what belongs to the address.
        // It is recorded before the login is validated, so Orchard's check that the email is confirmed sees it confirmed.
        var confirmationOffered = false;

        if (user is User account && !UserEmailOwnership.IsVerified(account.Properties, account.Email) && EmailOwnershipProof.HasCredentials(account))
        {
            // A password or an external login set before anyone proved the address may be somebody else's, and recording
            // the proof hands over what belongs to the address, so it would have to remove them. A code signs the owner in
            // with nothing removed, recorded or confirmed (confirming would let that password in where confirmation is
            // required); the owner confirms it is theirs, with a second code, when they choose to.
            confirmationOffered = true;
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
                await _notifier.InformationAsync(H["Your email address is confirmed. The password and other sign-ins this account had before were removed. Sign in with a code again, or set a new password with \"Forgot your password?\"."]);
            }
        }

        foreach (var loginFormEvent in _loginFormEvents)
        {
            if (await loginFormEvent.ValidatingLoginAsync(user) is { } refusal)
            {
                return refusal;
            }
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        await _signInManager.SignInAsync(user, isPersistent: false);

        await _loginFormEvents.InvokeAsync((handler, signedIn) => handler.LoggedInAsync(signedIn), user, _logger);

        if (confirmationOffered)
        {
            await _notifier.InformationAsync(H["You're signed in. To link what was placed with your email before, such as guest orders, <a href=\"{0}\">confirm it's you</a>.", Url.RouteUrl("EmailSignInConfirm", new { returnUrl = model.ReturnUrl })]);
        }

        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl)
            : LocalRedirect(Url.Content("~/"));
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
    /// Gets or sets the account's password, typed on the confirmation page by an owner who knows it, so it is kept.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account has a password or an external login.
    /// </summary>
    public bool HasCredentials { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the request is signed in with the account's password or through one of
    /// its external logins, which confirming keeps.
    /// </summary>
    public bool CredentialsShown { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the request is signed in with the account's password, which confirming
    /// keeps, removing the external logins.
    /// </summary>
    public bool PasswordShown { get; set; }

    /// <summary>
    /// Gets or sets the provider of the account's external login the request is signed in through, which confirming
    /// keeps, removing the password and the other logins; or <see langword="null"/>.
    /// </summary>
    public string ShownLoginProvider { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the confirmation page offers the password field.
    /// </summary>
    public bool AsksPassword { get; set; }
}
