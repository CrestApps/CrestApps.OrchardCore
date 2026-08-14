using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="ITelephonyUserAccessor"/> that resolves the current user through the
/// <see cref="UserManager{TUser}"/> and the HTTP context.
/// </summary>
public sealed class DefaultTelephonyUserAccessor : ITelephonyUserAccessor
{
    private readonly UserManager<IUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyUserAccessor"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public DefaultTelephonyUserAccessor(
        UserManager<IUser> userManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public async Task<IUser> GetCurrentUserAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return await _userManager.GetUserAsync(principal);
    }

    /// <inheritdoc/>
    public async Task UpdateUserAsync(IUser user)
    {
        await _userManager.UpdateAsync(user);
    }
}
