using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Users;
using OrchardCore.Users.Events;

namespace CrestApps.OrchardCore.TenantHierarchy.Handlers;

/// <summary>
/// Refuses password and external logins for linked users, so a child administrator who gives a linked user a
/// password, through the admin screens, a password reset or the Users recipe step, still cannot sign in as it. The
/// check reads the tenant hierarchy's own collection, which the recipe cannot change.
/// </summary>
public sealed class LinkedUserLoginFormEvent : LoginFormEventBase
{
    private readonly LinkedUserService _linkedUserService;
    private readonly UserManager<IUser> _userManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedUserLoginFormEvent"/> class.
    /// </summary>
    /// <param name="linkedUserService">The linked user service.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LinkedUserLoginFormEvent(
        LinkedUserService linkedUserService,
        UserManager<IUser> userManager,
        IStringLocalizer<LinkedUserLoginFormEvent> stringLocalizer)
    {
        _linkedUserService = linkedUserService;
        _userManager = userManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task LoggingInAsync(string userName, Action<string, string> reportError)
    {
        if (string.IsNullOrEmpty(userName))
        {
            return;
        }

        var user = await _userManager.FindByNameAsync(userName) ?? await _userManager.FindByEmailAsync(userName);

        if (user is not null && await _linkedUserService.IsLinkedUserAsync(await _userManager.GetUserIdAsync(user)))
        {
            reportError(string.Empty, S["Invalid login attempt."]);
        }
    }

    /// <inheritdoc/>
    public override async Task<IActionResult> ValidatingLoginAsync(IUser user)
    {
        if (user is not null && await _linkedUserService.IsLinkedUserAsync(await _userManager.GetUserIdAsync(user)))
        {
            return new ForbidResult();
        }

        return null;
    }
}
