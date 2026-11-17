using CrestApps.OrchardCore.Users.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Controllers;

/// <summary>
/// Asks the signed-in person for their password again before a sensitive page, such as their pay, and returns them to
/// it. Failed tries count toward the account's lockout, like a sign-in.
/// </summary>
[Admin]
public sealed class PasswordConfirmationController : Controller
{
    private readonly IPasswordConfirmation _confirmation;
    private readonly UserManager<IUser> _userManager;
    private readonly SignInManager<IUser> _signInManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordConfirmationController"/> class.
    /// </summary>
    /// <param name="confirmation">The password confirmation.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="signInManager">The sign-in manager, for the password check and its lockout.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PasswordConfirmationController(IPasswordConfirmation confirmation, UserManager<IUser> userManager, SignInManager<IUser> signInManager, IStringLocalizer<PasswordConfirmationController> stringLocalizer)
    {
        _confirmation = confirmation;
        _userManager = userManager;
        _signInManager = signInManager;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the password box.
    /// </summary>
    /// <param name="returnUrl">The local page to return to.</param>
    /// <returns>The page.</returns>
    [Admin("confirm-password", "UsersPasswordConfirmation")]
    public async Task<IActionResult> Index(string returnUrl)
    {
        if (await _userManager.GetUserAsync(User) is not { } user)
        {
            return Forbid();
        }

        return View(new PasswordConfirmationViewModel
        {
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null,
            HasPassword = await _userManager.HasPasswordAsync(user),
            Minutes = (int)_confirmation.Window.TotalMinutes,
        });
    }

    /// <summary>
    /// Checks the password and returns to the page.
    /// </summary>
    /// <param name="model">The password and the page.</param>
    /// <returns>The page, or the password box again.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [Admin("confirm-password", "UsersPasswordConfirmation")]
    public async Task<IActionResult> IndexPost(PasswordConfirmationViewModel model)
    {
        if (await _userManager.GetUserAsync(User) is not { } user)
        {
            return Forbid();
        }

        model.ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;
        model.HasPassword = await _userManager.HasPasswordAsync(user);
        model.Minutes = (int)_confirmation.Window.TotalMinutes;

        if (!model.HasPassword)
        {
            return View(model);
        }

        var result = string.IsNullOrEmpty(model.Password)
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _confirmation.ConfirmAsync(User, HttpContext.RequestAborted);

            return string.IsNullOrEmpty(model.ReturnUrl) ? Redirect("~/Admin") : LocalRedirect(model.ReturnUrl);
        }

        ModelState.AddModelError(nameof(model.Password), result.IsLockedOut
            ? S["Too many wrong tries: the account is locked for a while."]
            : S["That isn't your password."]);
        model.Password = null;

        return View(model);
    }
}
