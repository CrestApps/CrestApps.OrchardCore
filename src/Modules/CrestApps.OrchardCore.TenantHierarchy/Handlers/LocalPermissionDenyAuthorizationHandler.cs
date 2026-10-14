using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Environment.Shell;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.TenantHierarchy.Handlers;

/// <summary>
/// Fails the permissions the parent policy denies to the local users of a child tenant, for example importing recipes.
/// Users that entered through delegated access are not affected.
/// </summary>
public sealed class LocalPermissionDenyAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly HashSet<string> _deniedPermissions;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalPermissionDenyAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the child tenant.</param>
    public LocalPermissionDenyAuthorizationHandler(ShellSettings shellSettings)
    {
        _deniedPermissions = shellSettings.GetDeniedLocalPermissions().ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (_deniedPermissions.Count > 0 &&
            context.User?.Identity?.IsAuthenticated == true &&
            !DelegatedAccessClaims.IsDelegated(context.User) &&
            requirement.Permission?.Name is { } name &&
            _deniedPermissions.Contains(name))
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }
}
