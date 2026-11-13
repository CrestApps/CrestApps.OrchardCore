using CrestApps.OrchardCore.Core.Throttling;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Controllers;

/// <summary>
/// Signing in with a code emailed to the account's address, instead of a password. The pages answer the same way
/// whether or not an account has the email, so they never tell who has one.
/// </summary>
[AllowAnonymous]
[Feature(UsersConstants.Feature.EmailCodeSignIn)]
public sealed class EmailSignInController : Controller
{
    private static readonly System.ComponentModel.DataAnnotations.EmailAddressAttribute _emailAddress = new();

    private readonly UserManager<IUser> _userManager;
    private readonly SignInManager<IUser> _signInManager;
    private readonly EmailSignInCodes _codes;
    private readonly IEmailService _emailService;
    private readonly ISiteService _siteService;
    private readonly IEnumerable<ILoginFormEvent> _loginFormEvents;
    private readonly IClock _clock;
    private readonly EmailSignInOptions _options;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public EmailSignInController(
        UserManager<IUser> userManager,
        SignInManager<IUser> signInManager,
        EmailSignInCodes codes,
        IEmailService emailService,
        ISiteService siteService,
        IEnumerable<ILoginFormEvent> loginFormEvents,
        IClock clock,
        IOptions<EmailSignInOptions> options,
        ILogger<EmailSignInController> logger,
        IStringLocalizer<EmailSignInController> stringLocalizer)
    {
        _loginFormEvents = loginFormEvents;
        _clock = clock;
        _options = options.Value;
        _userManager = userManager;
        _signInManager = signInManager;
        _codes = codes;
        _emailService = emailService;
        _siteService = siteService;
        _logger = logger;
        S = stringLocalizer;
    }

    [HttpGet("sign-in/email", Name = "EmailSignIn")]
    public IActionResult Index(string returnUrl = null)
        => View(new EmailSignInViewModel { ReturnUrl = returnUrl });

    [HttpPost("sign-in/email")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(EmailSignInCodes.RateLimitGroup)]
    public async Task<IActionResult> Index(EmailSignInViewModel model)
    {
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

        if (await FindUserAsync(email) is { } user)
        {
            await SendCodeAsync(user, email);
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

        var user = string.IsNullOrEmpty(email) ? null : await FindUserAsync(email);

        // A wrong code and no account read the same.
        if (user is null)
        {
            await _loginFormEvents.InvokeAsync((handler, userName) => handler.LoggingInFailedAsync(userName), email, _logger);

            return CodeRefused(model);
        }

        var check = await _codes.CheckAsync(email, model.Code, HttpContext.RequestAborted);

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
        if (user is User account && (!account.EmailConfirmed || !UserEmailOwnership.IsVerified(account.Properties, account.Email)))
        {
            account.EmailConfirmed = true;
            UserEmailOwnership.MarkVerified(account.Properties, account.Email, _clock.UtcNow);
            await _userManager.UpdateAsync(account);
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

        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl)
            : LocalRedirect(Url.Content("~/"));
    }

    private ViewResult CodeRefused(EmailSignInViewModel model)
    {
        ModelState.AddModelError(nameof(model.Code), S["That code isn't right or has expired. Check the latest email, or ask for a new code."]);

        return View(model);
    }

    // An account that can sign in with a code: enabled, not locked out, not using two-factor sign-in, which a code would
    // get around, and not in a role kept to password sign-in (Administrator by default).
    private async Task<IUser> FindUserAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null ||
            (user is User account && !account.IsEnabled) ||
            await _userManager.IsLockedOutAsync(user) ||
            await _userManager.GetTwoFactorEnabledAsync(user) ||
            await IsInExcludedRoleAsync(user))
        {
            return null;
        }

        return user;
    }

    private async Task<bool> IsInExcludedRoleAsync(IUser user)
    {
        foreach (var role in _options.ExcludedRoles ?? [])
        {
            if (!string.IsNullOrWhiteSpace(role) && await _userManager.IsInRoleAsync(user, role))
            {
                return true;
            }
        }

        return false;
    }

    private async Task SendCodeAsync(IUser user, string email)
    {
        var code = await _codes.IssueAsync(email, HttpContext.RequestAborted);
        var site = await _siteService.GetSiteSettingsAsync();
        var siteName = string.IsNullOrWhiteSpace(site.SiteName) ? S["the site"].Value : site.SiteName;
        var encoder = HtmlEncoder.Default;

        var result = await _emailService.SendAsync(new MailMessage
        {
            To = email,
            Subject = S["Your sign-in code for {0}: {1}", siteName, code],
            TextBody = S["Your sign-in code is {0}. It works for 10 minutes. If you didn't ask for it, ignore this email.", code],
            HtmlBody = $"<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#212529\"><p>{encoder.Encode(S["Your sign-in code for {0} is:", siteName].Value)}</p><p style=\"font-size:28px;font-weight:bold;letter-spacing:6px\">{code}</p><p style=\"color:#6c757d\">{encoder.Encode(S["It works for 10 minutes. If you didn't ask for it, ignore this email.", code].Value)}</p></div>",
        });

        if (!result.Succeeded)
        {
            _logger.LogWarning("A sign-in code couldn't be emailed to user {UserName}.", user.UserName);
        }
    }
}

public class EmailSignInViewModel
{
    public string Email { get; set; }

    public string Code { get; set; }

    public string ReturnUrl { get; set; }
}
