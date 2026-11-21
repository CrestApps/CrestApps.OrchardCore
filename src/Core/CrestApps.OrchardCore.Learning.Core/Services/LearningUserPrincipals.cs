using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Builds the claims principal of a user from their id, to authorize work done for a person outside their request: a
/// background import for the person who queued it, or a cohort link applied for the person who saved it.
/// </summary>
public static class LearningUserPrincipals
{
    /// <summary>
    /// Builds the principal of a user.
    /// </summary>
    /// <param name="services">The services of the scope.</param>
    /// <param name="userId">The user id.</param>
    /// <returns>The principal, or <see langword="null"/> when there is no such user or no users feature.</returns>
    public static async Task<ClaimsPrincipal> CreateAsync(IServiceProvider services, string userId)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrEmpty(userId) ||
            services.GetService<UserManager<IUser>>() is not { } users ||
            services.GetService<IUserClaimsPrincipalFactory<IUser>>() is not { } principals ||
            await users.FindByIdAsync(userId) is not { } user)
        {
            return null;
        }

        return await principals.CreateAsync(user);
    }
}
