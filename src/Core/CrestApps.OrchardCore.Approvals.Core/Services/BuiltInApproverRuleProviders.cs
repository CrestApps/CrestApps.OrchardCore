using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Localization;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// Anyone who holds a permission (the rule's value is its name). Deciding asks the authorization service; the notice
/// list is the users of the roles that grant it, or a permission that implies it, and the administrators.
/// </summary>
public sealed class PermissionApproverRuleProvider : IApproverRuleProvider
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IPermissionProvider> _permissionProviders;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionApproverRuleProvider"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="permissionProviders">The permission providers, to find a permission by name.</param>
    /// <param name="serviceProvider">The services, for the role and user stores, which may be off.</param>
    public PermissionApproverRuleProvider(IAuthorizationService authorizationService, IEnumerable<IPermissionProvider> permissionProviders, IServiceProvider serviceProvider)
    {
        _authorizationService = authorizationService;
        _permissionProviders = permissionProviders;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public string Name
        => ApproverRuleKinds.Permission;

    /// <inheritdoc />
    public LocalizationSource DisplayName { get; } = LocalizationSource.Create<PermissionApproverRuleProvider>("Anyone with the permission");

    /// <inheritdoc />
    public bool NeedsValue
        => true;

    /// <inheritdoc />
    public bool DependsOnRequest
        => false;

    /// <inheritdoc />
    public async Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => await FindPermissionAsync(rule?.Value) is { } permission && await _authorizationService.AuthorizeAsync(user, permission);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var permission = await FindPermissionAsync(rule?.Value);
        var roleService = _serviceProvider.GetService<IRoleService>();

        if (permission is null || roleService is null)
        {
            return [];
        }

        var names = new HashSet<string>(StringComparer.Ordinal) { permission.Name };

        foreach (var implying in permission.ImpliedBy ?? [])
        {
            names.Add(implying.Name);
        }

        var roles = new List<string> { OrchardCoreConstants.Roles.Administrator };

        foreach (var role in await roleService.GetRolesAsync())
        {
            if (role is Role editable && editable.RoleClaims?.Any(claim => claim.ClaimType == Permission.ClaimType && names.Contains(claim.ClaimValue)) == true)
            {
                roles.Add(role.RoleName);
            }
        }

        return await ApproverUsers.InRolesAsync(_serviceProvider, roles);
    }

    private async Task<Permission> FindPermissionAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (var provider in _permissionProviders)
        {
            foreach (var permission in await provider.GetPermissionsAsync())
            {
                if (string.Equals(permission.Name, name.Trim(), StringComparison.Ordinal))
                {
                    return permission;
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Anyone in a role (the rule's value is the role's name).
/// </summary>
public sealed class RoleApproverRuleProvider : IApproverRuleProvider
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleApproverRuleProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, for the user store, which may be off.</param>
    public RoleApproverRuleProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public string Name
        => ApproverRuleKinds.Role;

    /// <inheritdoc />
    public LocalizationSource DisplayName { get; } = LocalizationSource.Create<RoleApproverRuleProvider>("Anyone in the role");

    /// <inheritdoc />
    public bool NeedsValue
        => true;

    /// <inheritdoc />
    public bool DependsOnRequest
        => false;

    /// <inheritdoc />
    public Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => Task.FromResult(!string.IsNullOrWhiteSpace(rule?.Value) && user.IsInRole(rule.Value.Trim()));

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
        => ApproverUsers.InRolesAsync(_serviceProvider, [rule?.Value?.Trim()]);
}

/// <summary>
/// Named users (the rule's value is their user ids, separated by commas), chosen with the user picker.
/// </summary>
public sealed class UsersApproverRuleProvider : IApproverRuleProvider
{
    /// <inheritdoc />
    public string Name
        => ApproverRuleKinds.Users;

    /// <inheritdoc />
    public LocalizationSource DisplayName { get; } = LocalizationSource.Create<UsersApproverRuleProvider>("Named users");

    /// <inheritdoc />
    public bool NeedsValue
        => true;

    /// <inheritdoc />
    public bool DependsOnRequest
        => false;

    /// <inheritdoc />
    public Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => Task.FromResult(user.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } id && Ids(rule).Contains(id, StringComparer.Ordinal));

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(Ids(rule));

    private static string[] Ids(ApproverRule rule)
        => [.. (rule?.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal)];
}

/// <summary>
/// Finds users for approver rule providers.
/// </summary>
public static class ApproverUsers
{
    /// <summary>
    /// Returns the ids of the users in any of some roles.
    /// </summary>
    /// <param name="serviceProvider">The services, for the user store, which may be off.</param>
    /// <param name="roles">The role names.</param>
    /// <returns>The user ids.</returns>
    public static async Task<IReadOnlyList<string>> InRolesAsync(IServiceProvider serviceProvider, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var userManager = serviceProvider.GetService<UserManager<IUser>>();

        if (userManager is null)
        {
            return [];
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var role in (roles ?? []).Where(role => !string.IsNullOrEmpty(role)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var user in await userManager.GetUsersInRoleAsync(role))
            {
                if (await userManager.GetUserIdAsync(user) is { Length: > 0 } id)
                {
                    ids.Add(id);
                }
            }
        }

        return [.. ids];
    }
}
