using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.TenantHierarchy.Handlers;

/// <summary>
/// Fails every user-management permission whose resource is a linked user, so no child administrator can change the
/// password, name, email or roles of a linked user, or delete it from the admin screens. In ASP.NET Core a failure wins
/// over the success of any other handler, including the one that gives administrators every permission.
/// </summary>
public sealed class LinkedUserAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private static readonly string[] _protectedPermissions =
    [
        "ManageUsers",
        "EditUsers",
        "DeleteUsers",
        "AssignRoleToUsers",
        "DisableTwoFactorAuthenticationForUsers",
    ];

    private static readonly string[] _protectedPermissionPrefixes =
    [
        "EditUsersInRole_",
        "DeleteUsersInRole_",
        "AssignRoleToUsers_",
        "ManageUsersInRole_",
    ];

    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedUserAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider, used to resolve the linked user service lazily.</param>
    public LinkedUserAuthorizationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Returns whether a permission manages users.
    /// </summary>
    /// <param name="permissionName">The permission name.</param>
    internal static bool IsUserManagementPermission(string permissionName)
    {
        if (string.IsNullOrEmpty(permissionName))
        {
            return false;
        }

        return _protectedPermissions.Contains(permissionName, StringComparer.Ordinal) ||
            _protectedPermissionPrefixes.Any(prefix => permissionName.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.Resource is not IUser target || !IsUserManagementPermission(requirement.Permission?.Name))
        {
            return;
        }

        var userManager = _serviceProvider.GetRequiredService<UserManager<IUser>>();
        var linkedUserService = _serviceProvider.GetRequiredService<LinkedUserService>();

        if (await linkedUserService.IsLinkedUserAsync(await userManager.GetUserIdAsync(target)))
        {
            context.Fail();
        }
    }
}
