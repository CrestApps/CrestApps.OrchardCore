using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyUserAccessor"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="logger">The logger.</param>
    public DefaultTelephonyUserAccessor(
        UserManager<IUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DefaultTelephonyUserAccessor> logger)
    {
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
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
        ArgumentNullException.ThrowIfNull(user);

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            var codes = string.Join(", ", result.Errors.Select(error => error.Code));

            _logger.LogError(
                "Failed to persist telephony token changes for the current user. Identity error codes: {ErrorCodes}",
                codes);

            throw new TelephonyUserPersistenceException(
                $"Telephony token changes could not be persisted (identity error codes: {codes}).");
        }
    }
}

