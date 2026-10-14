using System.Security.Claims;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Validates the principal of a child tenant on each request. A principal that entered through delegated access is
/// checked with the parent at the interval of the parent policy; a linked user without delegated access claims is
/// rejected, so a linked user can only ever be signed in by the parent.
/// </summary>
public sealed class DelegatedSessionPrincipalValidator
{
    private readonly ITenantHierarchyBroker _broker;
    private readonly LinkedUserService _linkedUserService;
    private readonly UserManager<IUser> _userManager;
    private readonly SignInManager<IUser> _signInManager;
    private readonly IMemoryCache _memoryCache;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatedSessionPrincipalValidator"/> class.
    /// </summary>
    /// <param name="broker">The tenant hierarchy broker.</param>
    /// <param name="linkedUserService">The linked user service.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="signInManager">The sign-in manager.</param>
    /// <param name="memoryCache">The memory cache.</param>
    /// <param name="shellSettings">The settings of the child tenant.</param>
    /// <param name="logger">The logger.</param>
    public DelegatedSessionPrincipalValidator(
        ITenantHierarchyBroker broker,
        LinkedUserService linkedUserService,
        UserManager<IUser> userManager,
        SignInManager<IUser> signInManager,
        IMemoryCache memoryCache,
        ShellSettings shellSettings,
        ILogger<DelegatedSessionPrincipalValidator> logger)
    {
        _broker = broker;
        _linkedUserService = linkedUserService;
        _userManager = userManager;
        _signInManager = signInManager;
        _memoryCache = memoryCache;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    /// <summary>
    /// Validates the principal of a cookie and rejects it, renews it or leaves it.
    /// </summary>
    /// <param name="context">The cookie validation context.</param>
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var principal = context.Principal;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var sessionId = DelegatedAccessClaims.GetSessionId(principal);
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(sessionId))
        {
            if (await _linkedUserService.IsLinkedUserAsync(userId))
            {
                _logger.LogWarning("Child tenant '{ChildTenant}' rejected linked user '{UserId}' signed in without delegated access.", _shellSettings.Name, userId);
                await RejectAsync(context);
            }

            return;
        }

        var validation = await GetValidationAsync(sessionId, DelegatedAccessClaims.GetValidationInterval(principal));

        if (!validation.IsActive)
        {
            await RejectAsync(context);

            return;
        }

        var rolesVersion = principal.FindFirstValue(TenantHierarchyConstants.ClaimTypes.RolesVersion);

        if (string.Equals(rolesVersion, validation.RolesVersion, StringComparison.Ordinal))
        {
            return;
        }

        if (await _userManager.FindByIdAsync(userId) is not User user)
        {
            await RejectAsync(context);

            return;
        }

        var link = await _linkedUserService.FindLinkAsync(user.UserId);

        if (link is null)
        {
            await RejectAsync(context);

            return;
        }

        await _linkedUserService.SyncRolesAsync(user, link, validation.ChildRoles);

        var renewed = await _signInManager.CreateUserPrincipalAsync(user);
        DelegatedAccessClaims.CopyTo(principal, (ClaimsIdentity)renewed.Identity, validation.RolesVersion);

        context.ReplacePrincipal(renewed);
        context.ShouldRenew = true;
    }

    /// <summary>
    /// Forgets the cached validation of a session, so the next request validates it with the parent again.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    public void Forget(string sessionId)
    {
        if (!string.IsNullOrEmpty(sessionId))
        {
            _memoryCache.Remove(GetCacheKey(sessionId));
        }
    }

    private async Task<DelegatedSessionValidation> GetValidationAsync(string sessionId, TimeSpan interval)
    {
        var key = GetCacheKey(sessionId);

        if (_memoryCache.TryGetValue<DelegatedSessionValidation>(key, out var cached))
        {
            return cached;
        }

        var validation = await _broker.ValidateSessionAsync(sessionId) ?? DelegatedSessionValidation.Inactive;
        _memoryCache.Set(key, validation, interval);

        return validation;
    }

    private string GetCacheKey(string sessionId)
        => $"TenantHierarchy:Session:{_shellSettings.Name}:{DelegatedAccessTokens.Hash(sessionId)}";

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
