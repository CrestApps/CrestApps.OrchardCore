using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Security.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Creates, finds and updates the linked users of a child tenant: the local users that belong to parent users.
/// </summary>
public sealed class LinkedUserService
{
    private const int MaxUserNameAttempts = 50;

    private static readonly TimeSpan _linkedCacheDuration = TimeSpan.FromMinutes(5);

    private readonly UserManager<IUser> _userManager;
    private readonly IRoleService _roleService;
    private readonly UserLinkStore _userLinkStore;
    private readonly IMemoryCache _memoryCache;
    private readonly ShellSettings _shellSettings;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedUserService"/> class.
    /// </summary>
    /// <param name="userManager">The user manager of the child tenant.</param>
    /// <param name="roleService">The role service of the child tenant.</param>
    /// <param name="userLinkStore">The user link store.</param>
    /// <param name="memoryCache">The memory cache.</param>
    /// <param name="shellSettings">The settings of the child tenant.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public LinkedUserService(
        UserManager<IUser> userManager,
        IRoleService roleService,
        UserLinkStore userLinkStore,
        IMemoryCache memoryCache,
        ShellSettings shellSettings,
        IClock clock,
        ILogger<LinkedUserService> logger)
    {
        _userManager = userManager;
        _roleService = roleService;
        _userLinkStore = userLinkStore;
        _memoryCache = memoryCache;
        _shellSettings = shellSettings;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Finds the linked user of the parent user that redeemed a code, or creates it, then syncs its roles. Links are
    /// matched by parent tenant and parent user only, never by email or user name.
    /// </summary>
    /// <param name="redemption">The redeemed delegated access.</param>
    public async Task<User> ProvisionAsync(DelegatedAccessRedemption redemption)
    {
        ArgumentNullException.ThrowIfNull(redemption);

        var link = await _userLinkStore.FindCurrentAsync(redemption.ParentTenantId, redemption.ParentUserId);
        User user = null;

        if (link is not null)
        {
            user = await _userManager.FindByIdAsync(link.ChildUserId) as User;

            if (user is null)
            {
                // The local user was deleted. The old link is kept, so the earlier user identifier stays traceable.
                link.IsCurrent = false;
                await _userLinkStore.SaveAsync(link);
                link = null;
            }
        }

        if (user is null)
        {
            user = await CreateUserAsync(redemption);
            link = new UserLink
            {
                ChildUserId = user.UserId,
                ParentTenantId = redemption.ParentTenantId,
                ParentUserId = redemption.ParentUserId,
                CreatedUtc = _clock.UtcNow,
            };
        }
        else if (!user.IsEnabled || !string.IsNullOrEmpty(user.PasswordHash))
        {
            // The parent manages its own users: a child administrator can neither lock a parent user out nor give the
            // linked user a password, for example through the Users recipe step, which writes to the store directly.
            user.IsEnabled = true;
            user.PasswordHash = null;
            await _userManager.UpdateAsync(user);
        }

        link.ParentUserName = redemption.ParentUserName;
        link.LastEnteredUtc = _clock.UtcNow;

        await SyncRolesAsync(user, link, redemption.ChildRoles);

        _memoryCache.Set(GetLinkedCacheKey(user.UserId), true, _linkedCacheDuration);

        return user;
    }

    /// <summary>
    /// Gives a linked user exactly the granted roles that exist in the child tenant, then saves the link. Every other
    /// role is removed, because a linked user gets its roles from the parent only.
    /// </summary>
    /// <param name="user">The linked user.</param>
    /// <param name="link">The link of the user.</param>
    /// <param name="grantedRoles">The roles granted by the parent.</param>
    public async Task SyncRolesAsync(User user, UserLink link, IEnumerable<string> grantedRoles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(link);

        var existingRoles = (await _roleService.GetRolesAsync())
            .Select(role => role.RoleName)
            .ToDictionary(name => name, StringComparer.OrdinalIgnoreCase);

        var target = (grantedRoles ?? [])
            .Select(role => existingRoles.TryGetValue(role, out var name) ? name : null)
            .Where(role => role is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var current = user.RoleNames?.ToList() ?? [];

        foreach (var role in target.Where(role => !current.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            var result = await _userManager.AddToRoleAsync(user, role);
            LogFailure(result, "add the role", role, user);
        }

        foreach (var role in current.Where(role => !target.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            var result = await _userManager.RemoveFromRoleAsync(user, role);
            LogFailure(result, "remove the role", role, user);
        }

        link.ManagedRoles = target.ToArray();
        await _userLinkStore.SaveAsync(link);
    }

    /// <summary>
    /// Returns the current link of a local user, or <see langword="null"/> when the user is not linked.
    /// </summary>
    /// <param name="childUserId">The local user identifier.</param>
    public Task<UserLink> FindLinkAsync(string childUserId)
        => _userLinkStore.FindByChildUserIdAsync(childUserId);

    /// <summary>
    /// Returns whether a local user is, or once was, linked to a parent user. The answer is cached briefly.
    /// </summary>
    /// <param name="childUserId">The local user identifier.</param>
    public async Task<bool> IsLinkedUserAsync(string childUserId)
    {
        if (string.IsNullOrEmpty(childUserId))
        {
            return false;
        }

        var key = GetLinkedCacheKey(childUserId);

        if (_memoryCache.TryGetValue<bool>(key, out var isLinked))
        {
            return isLinked;
        }

        isLinked = await _userLinkStore.FindByChildUserIdAsync(childUserId) is not null;
        _memoryCache.Set(key, isLinked, _linkedCacheDuration);

        return isLinked;
    }

    private async Task<User> CreateUserAsync(DelegatedAccessRedemption redemption)
    {
        string userName = null;

        for (var attempt = 1; attempt <= MaxUserNameAttempts; attempt++)
        {
            var candidate = TenantHierarchyNaming.BuildLinkedUserName(redemption.ParentUserName, redemption.ParentSlug, attempt);

            if (await _userManager.FindByNameAsync(candidate) is null)
            {
                userName = candidate;

                break;
            }
        }

        if (userName is null)
        {
            throw new InvalidOperationException("No free user name was found for the linked user.");
        }

        // The real email is used when no local user has it, so the parent user receives the child's notifications.
        var email = redemption.Email;

        if (string.IsNullOrWhiteSpace(email) || await _userManager.FindByEmailAsync(email) is not null)
        {
            email = $"{userName}@linked.invalid";
        }

        var user = new User
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            IsEnabled = true,
        };

        var result = await _userManager.CreateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"The linked user could not be created: {string.Join(' ', result.Errors.Select(error => error.Description))}");
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Child tenant '{ChildTenant}' created linked user '{UserId}' for parent user '{ParentUserId}' of parent tenant '{ParentTenantId}'.",
                _shellSettings.Name,
                user.UserId,
                redemption.ParentUserId,
                redemption.ParentTenantId);
        }

        return user;
    }

    private void LogFailure(IdentityResult result, string action, string role, User user)
    {
        if (result.Succeeded)
        {
            return;
        }

        _logger.LogWarning(
            "Could not {Action} '{Role}' for linked user '{UserId}': {Errors}",
            action,
            role,
            user.UserId,
            string.Join(' ', result.Errors.Select(error => error.Description)));
    }

    private string GetLinkedCacheKey(string childUserId)
        => $"TenantHierarchy:Linked:{_shellSettings.Name}:{childUserId}";
}
