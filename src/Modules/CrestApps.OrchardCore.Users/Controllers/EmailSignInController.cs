using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Controllers;

/// <summary>
/// Signing in with a code emailed to the account's address, instead of a password. The pages answer the same way
/// whether or not an account has the email, so they never tell who has one.
/// </summary>
[AllowAnonymous]
public sealed class EmailSignInController : Controller
{
    private static readonly System.ComponentModel.DataAnnotations.EmailAddressAttribute _emailAddress = new();

    private readonly UserManager<IUser> _userManager;
    private readonly SignInManager<IUser> _signInManager;
    private readonly EmailSignInCodes _codes;
    private readonly IEmailService _emailService;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public EmailSignInController(
        UserManager<IUser> userManager,
        SignInManager<IUser> signInManager,
        EmailSignInCodes codes,
        IEmailService emailService,
        ISiteService siteService,
        ILogger<EmailSignInController> logger,
        IStringLocalizer<EmailSignInController> stringLocalizer)
    {
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
    public async Task<IActionResult> Index(EmailSignInViewModel model)
    {
        var email = model.Email?.Trim();

        if (string.IsNullOrEmpty(email) || !_emailAddress.IsValid(email))
        {
            ModelState.AddModelError(nameof(model.Email), S["Enter the email of your account."]);

            return View(model);
        }

        if (!await _codes.TryCountSendAsync(email, HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.RequestAborted))
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
    public async Task<IActionResult> Code(EmailSignInViewModel model)
    {
        var email = model.Email?.Trim();
        var user = string.IsNullOrEmpty(email) ? null : await FindUserAsync(email);

        // A wrong code and no account read the same.
        if (user is null || !await _codes.VerifyAsync(email, model.Code, HttpContext.RequestAborted))
        {
            ModelState.AddModelError(nameof(model.Code), S["That code isn't right or has expired. Check the latest email, or ask for a new code."]);

            return View(model);
        }

        // The code proves the email is theirs.
        if (user is User account && !account.EmailConfirmed)
        {
            account.EmailConfirmed = true;
            await _userManager.UpdateAsync(account);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl)
            : LocalRedirect(Url.Content("~/"));
    }

    // An account that can sign in with a code: enabled, not locked out, and not using two-factor sign-in, which a
    // code would get around.
    private async Task<IUser> FindUserAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null ||
            (user is User account && !account.IsEnabled) ||
            await _userManager.IsLockedOutAsync(user) ||
            await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return null;
        }

        return user;
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
