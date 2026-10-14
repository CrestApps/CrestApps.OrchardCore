using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Security.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Manages the access grants of the current parent tenant: who may enter which child tenants, with which roles.
/// </summary>
public sealed class AccessGrantManager
{
    /// <summary>
    /// The parent role that gets the default grant.
    /// </summary>
    public const string DefaultParentRole = "Administrator";

    /// <summary>
    /// The child role the default grant gives.
    /// </summary>
    public const string DefaultChildRole = "Administrator";

    private readonly AccessGrantStore _grantStore;
    private readonly ChildTenantEntryStore _entryStore;
    private readonly ChildTenantManager _childTenantManager;
    private readonly UserManager<IUser> _userManager;
    private readonly IRoleService _roleService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessGrantManager"/> class.
    /// </summary>
    /// <param name="grantStore">The access grant store.</param>
    /// <param name="entryStore">The registry store.</param>
    /// <param name="childTenantManager">The child tenant manager, used for the activity log.</param>
    /// <param name="userManager">The user manager of the parent tenant.</param>
    /// <param name="roleService">The role service of the parent tenant.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AccessGrantManager(
        AccessGrantStore grantStore,
        ChildTenantEntryStore entryStore,
        ChildTenantManager childTenantManager,
        UserManager<IUser> userManager,
        IRoleService roleService,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<AccessGrantManager> stringLocalizer)
    {
        _grantStore = grantStore;
        _entryStore = entryStore;
        _childTenantManager = childTenantManager;
        _userManager = userManager;
        _roleService = roleService;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the grants for one child tenant, or for every child tenant when <paramref name="childEntryId"/> is
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="childEntryId">The registry entry of the child tenant, or <see langword="null"/>.</param>
    public Task<IReadOnlyList<AccessGrant>> ListAsync(string childEntryId)
    {
        return string.IsNullOrEmpty(childEntryId)
            ? _grantStore.ListParentWideAsync()
            : _grantStore.ListForChildAsync(childEntryId);
    }

    /// <summary>
    /// Lists the role names of the parent tenant, without the built-in anonymous and authenticated roles.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetParentRolesAsync()
    {
        return (await _roleService.GetRolesAsync())
            .Select(role => role.RoleName)
            .Where(role => !string.Equals(role, "Anonymous", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(role, "Authenticated", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Adds a grant. A user grant names the user by user name or email; a role grant names a parent role.
    /// </summary>
    /// <param name="principalType">Who the grant applies to.</param>
    /// <param name="principal">The user name, email or role name.</param>
    /// <param name="childEntryId">The registry entry of the child tenant, or <see langword="null"/> for every child tenant.</param>
    /// <param name="childRoles">The child roles.</param>
    public async Task<TenantHierarchyResult> AddAsync(
        AccessGrantPrincipalType principalType,
        string principal,
        string childEntryId,
        IEnumerable<string> childRoles)
    {
        var roles = (childRoles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roles.Length == 0)
        {
            return TenantHierarchyResult.Failure(S["Select at least one role."]);
        }

        if (string.IsNullOrWhiteSpace(principal))
        {
            return TenantHierarchyResult.Failure(principalType == AccessGrantPrincipalType.User
                ? S["Select a user."]
                : S["Select a role."]);
        }

        ChildTenantEntry entry = null;

        if (!string.IsNullOrEmpty(childEntryId))
        {
            entry = await _entryStore.FindByEntryIdAsync(childEntryId);

            if (entry is null)
            {
                return TenantHierarchyResult.Failure(S["The {0} was not found.", S["child tenant"]]);
            }
        }

        string principalId;
        string principalName;

        if (principalType == AccessGrantPrincipalType.User)
        {
            var user = await _userManager.FindByNameAsync(principal.Trim()) ?? await _userManager.FindByEmailAsync(principal.Trim());

            if (user is not User parentUser)
            {
                return TenantHierarchyResult.Failure(S["No user with this user name or email exists."]);
            }

            principalId = parentUser.UserId;
            principalName = parentUser.UserName;
        }
        else
        {
            var role = (await GetParentRolesAsync()).FirstOrDefault(name => string.Equals(name, principal.Trim(), StringComparison.OrdinalIgnoreCase));

            if (role is null)
            {
                return TenantHierarchyResult.Failure(S["The role was not found."]);
            }

            principalId = role;
            principalName = role;
        }

        var existing = (await ListAsync(childEntryId))
            .FirstOrDefault(grant => grant.PrincipalType == principalType &&
                string.Equals(grant.PrincipalId, principalId, StringComparison.OrdinalIgnoreCase));

        var grantToSave = existing ?? new AccessGrant
        {
            GrantId = IdGenerator.GenerateId(),
            PrincipalType = principalType,
            PrincipalId = principalId,
            PrincipalName = principalName,
            ChildEntryId = entry?.EntryId,
            CreatedUtc = _clock.UtcNow,
        };

        grantToSave.ChildRoles = roles;
        grantToSave.CreatedByName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;

        await _grantStore.SaveAsync(grantToSave);
        await _childTenantManager.RecordAsync(
            HierarchyAuditEventNames.GrantAdded,
            entry,
            $"{principalType} {principalName}: {string.Join(", ", roles)}");

        return TenantHierarchyResult.Success;
    }

    /// <summary>
    /// Removes a grant.
    /// </summary>
    /// <param name="grantId">The grant identifier.</param>
    public async Task<TenantHierarchyResult> RemoveAsync(string grantId)
    {
        var grant = await _grantStore.FindAsync(grantId);

        if (grant is null)
        {
            return TenantHierarchyResult.Failure(S["The access grant was not found."]);
        }

        var entry = string.IsNullOrEmpty(grant.ChildEntryId)
            ? null
            : await _entryStore.FindByEntryIdAsync(grant.ChildEntryId);

        _grantStore.Delete(grant);
        await _childTenantManager.RecordAsync(
            HierarchyAuditEventNames.GrantRemoved,
            entry,
            $"{grant.PrincipalType} {grant.PrincipalName}");

        return TenantHierarchyResult.Success;
    }

    /// <summary>
    /// Adds the default grant: the parent's administrators enter every child tenant as administrators.
    /// </summary>
    public async Task SeedDefaultAsync()
    {
        if ((await _grantStore.ListParentWideAsync()).Count > 0)
        {
            return;
        }

        await _grantStore.SaveAsync(new AccessGrant
        {
            GrantId = IdGenerator.GenerateId(),
            PrincipalType = AccessGrantPrincipalType.Role,
            PrincipalId = DefaultParentRole,
            PrincipalName = DefaultParentRole,
            ChildRoles = [DefaultChildRole],
            CreatedUtc = _clock.UtcNow,
        });
    }
}
