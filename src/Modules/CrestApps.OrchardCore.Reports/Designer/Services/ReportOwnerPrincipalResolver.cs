using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Builds the principal of a report's owner. A saved report reads its data with its owner's current access, so a
/// report stops working when its owner is deleted or disabled, or loses access to the data it reads.
/// </summary>
public sealed class ReportOwnerPrincipalResolver
{
    private readonly UserManager<IUser> _userManager;
    private readonly IUserClaimsPrincipalFactory<IUser> _principalFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportOwnerPrincipalResolver"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="principalFactory">The factory that builds a user's principal with their roles and claims.</param>
    public ReportOwnerPrincipalResolver(
        UserManager<IUser> userManager,
        IUserClaimsPrincipalFactory<IUser> principalFactory)
    {
        _userManager = userManager;
        _principalFactory = principalFactory;
    }

    /// <summary>
    /// Builds the principal of a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The principal, or <see langword="null"/> when the user does not exist or is disabled.</returns>
    public async Task<ClaimsPrincipal> ResolveAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null || (user is User { IsEnabled: false }))
        {
            return null;
        }

        return await _principalFactory.CreateAsync(user);
    }
}
