using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// The default <see cref="IApproverDirectory"/>: permissions through the authorization service, roles through the
/// person's claims and the role store, managers and named staff through Staff when it is on.
/// </summary>
public sealed class ApproverDirectory : IApproverDirectory
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IPermissionProvider> _permissionProviders;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApproverDirectory"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="permissionProviders">The permission providers, to find a permission by name.</param>
    /// <param name="serviceProvider">The services, for the staff directory and hierarchy and the user and role stores,
    /// which may be off.</param>
    public ApproverDirectory(IAuthorizationService authorizationService, IEnumerable<IPermissionProvider> permissionProviders, IServiceProvider serviceProvider)
    {
        _authorizationService = authorizationService;
        _permissionProviders = permissionProviders;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(request);

        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        switch (rule.Kind)
        {
            case ApproverKind.Permission:
                return await FindPermissionAsync(rule.Value) is { } permission && await _authorizationService.AuthorizeAsync(user, permission);
            case ApproverKind.Role:
                return !string.IsNullOrWhiteSpace(rule.Value) && user.IsInRole(rule.Value.Trim());
            case ApproverKind.Manager:
                var userId = UserId(user);
                return userId is not null && (await GetManagerUserIdsAsync(request, cancellationToken)).Contains(userId, StringComparer.Ordinal);
            case ApproverKind.Staff:
                var id = UserId(user);
                return id is not null && (await GetStaffUserIdsAsync(rule.Value, cancellationToken)).Contains(id, StringComparer.Ordinal);
            default:
                return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(request);

        return rule.Kind switch
        {
            ApproverKind.Permission => await GetPermissionUserIdsAsync(rule.Value),
            ApproverKind.Role => await GetRoleUserIdsAsync([rule.Value?.Trim()]),
            ApproverKind.Manager => await GetManagerUserIdsAsync(request, cancellationToken),
            ApproverKind.Staff => await GetStaffUserIdsAsync(rule.Value, cancellationToken),
            _ => [],
        };
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

    // The people of a permission, for notices: users of the roles that grant it, or a permission that implies it, and
    // the administrators. Deciding always asks the authorization service, so a notice that misses someone never
    // stops them deciding.
    private async Task<IReadOnlyList<string>> GetPermissionUserIdsAsync(string name)
    {
        var permission = await FindPermissionAsync(name);
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

        return await GetRoleUserIdsAsync(roles);
    }

    private async Task<IReadOnlyList<string>> GetRoleUserIdsAsync(IEnumerable<string> roles)
    {
        var userManager = _serviceProvider.GetService<UserManager<IUser>>();

        if (userManager is null)
        {
            return [];
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var role in roles.Where(role => !string.IsNullOrEmpty(role)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var user in await userManager.GetUsersInRoleAsync(role))
            {
                if (await userManager.GetUserIdAsync(user) is { Length: > 0 } id)
                {
                    ids.Add(id);
                }
            }
        }

        return ids.ToList();
    }

    // The managers up the chain of the staff member the request is about, who have a user.
    private async Task<IReadOnlyList<string>> GetManagerUserIdsAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        var hierarchy = _serviceProvider.GetService<IStaffHierarchy>();

        if (hierarchy is null || string.IsNullOrEmpty(request.SubjectStaffMemberId))
        {
            return [];
        }

        return (await hierarchy.GetManagerChainAsync(request.SubjectStaffMemberId, cancellationToken))
            .Select(manager => manager.UserId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> GetStaffUserIdsAsync(string value, CancellationToken cancellationToken)
    {
        var directory = _serviceProvider.GetService<IStaffDirectory>();
        var ids = (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (directory is null || ids.Length == 0)
        {
            return [];
        }

        return (await directory.GetAsync(ids, cancellationToken)).Values
            .Select(member => member.UserId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string UserId(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier);
}
