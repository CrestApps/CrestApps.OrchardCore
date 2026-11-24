using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Roles;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.AI.Agent.Roles;

/// <summary>
/// Returns one role with the permissions the View page of the Roles admin area shows: the ones assigned to the role,
/// the ones a user who holds only this role has in effect (asked from the authorization service), and the ones that
/// come from the Anonymous and Authenticated roles; or compares the permissions of two roles.
/// </summary>
public sealed class GetRoleDetailsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getRole";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "roleName": {
          "type": "string",
          "description": "The name of the role, from listRoles."
        },
        "filter": {
          "type": "string",
          "description": "Text a permission name must contain, to narrow the lists of a role that holds many permissions. Leave it out to list them all."
        },
        "compareWith": {
          "type": "string",
          "description": "The name of a second role, from listRoles, to compare with: the result then lists the permissions only the role has, only the second role has, and both have, instead of the role's details."
        }
      },
      "required": [
        "roleName"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Gets a role's description and permissions: the ones assigned directly and the effective ones, including those inherited from Anonymous and Authenticated. With compareWith, it compares the permissions of two roles instead. Use it to answer \"what can this role do\" or \"how do these roles differ\" and before saveRole.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, RolesPermissions.ViewRoles))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("roleName", out var roleName))
        {
            return "Give roleName, the name of the role. Use listRoles to get the role names.";
        }

        var role = await RoleToolSupport.FindRoleAsync(services, roleName);

        if (role is null)
        {
            return $"There is no role named '{roleName}'. Use listRoles to get the role names.";
        }

        arguments.TryGetFirstString("filter", out var filter);
        filter = filter?.Trim();

        if (arguments.TryGetFirstString("compareWith", out var compareWith) && !string.IsNullOrWhiteSpace(compareWith))
        {
            var other = await RoleToolSupport.FindRoleAsync(services, compareWith);

            if (other is null)
            {
                return $"There is no role named '{compareWith.Trim()}' to compare with. Use listRoles to get the role names.";
            }

            if (string.Equals(other.RoleName, role.RoleName, StringComparison.OrdinalIgnoreCase))
            {
                return "Give compareWith the name of another role: a role compared with itself has no differences.";
            }

            return await CompareAsync(services, role, other, filter);
        }

        var roleService = services.GetRequiredService<IRoleService>();
        var isSystemRole = await roleService.IsSystemRoleAsync(role.RoleName);
        var isAdminRole = await roleService.IsAdminRoleAsync(role.RoleName);
        var description = new
        {
            name = role.RoleName,
            description = role.RoleDescription,
            isSystemRole,
            isAdminRole,
            adminUrl = RoleToolSupport.GetAdminUrl(services, role.RoleName),
        };

        // As on the admin page, the permissions of the administrator role are not listed: it holds them all.
        if (isAdminRole)
        {
            return AgentToolResults.Json(new
            {
                role = description,
                holdsEveryPermission = true,
                note = "The administrator role holds every permission, and its permissions cannot be changed.",
            });
        }

        var installed = await RoleToolSupport.GetInstalledPermissionsAsync(services);
        var assigned = RoleToolSupport.GetAssignedPermissionNames(role);
        var isAnonymousRole = string.Equals(role.RoleName, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase);
        var isAuthenticatedRole = string.Equals(role.RoleName, OrchardCoreConstants.Roles.Authenticated, StringComparison.OrdinalIgnoreCase);
        var effective = await GetEffectiveAsync(services, role, installed);

        // Every user has what Anonymous has, and every signed-in user also has what Authenticated has.
        var fromAnonymous = isAnonymousRole
            ? []
            : await GetGrantedAsync(services, OrchardCoreConstants.Roles.Anonymous, installed);

        var fromAuthenticated = isAnonymousRole || isAuthenticatedRole
            ? []
            : await GetGrantedAsync(services, OrchardCoreConstants.Roles.Authenticated, installed);

        var securityCritical = installed
            .Where(permission => permission.Permission.IsSecurityCritical)
            .Select(permission => permission.Permission.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var installedNames = installed
            .Select(permission => permission.Permission.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var truncated = false;

        return AgentToolResults.Json(new
        {
            role = description,
            assignedPermissions = RoleToolSupport.Limit(assigned, filter, ref truncated),
            effectivePermissions = RoleToolSupport.Limit(effective, filter, ref truncated),
            inheritedFromAnonymous = RoleToolSupport.Limit(fromAnonymous, filter, ref truncated),
            inheritedFromAuthenticated = RoleToolSupport.Limit(fromAuthenticated, filter, ref truncated),
            securityCriticalPermissions = RoleToolSupport.Limit(effective.Where(securityCritical.Contains), filter, ref truncated),
            notProvidedByAnEnabledFeature = RoleToolSupport.Limit(assigned.Where(name => !installedNames.Contains(name)), filter, ref truncated),
            counts = new
            {
                assigned = assigned.Count,
                effective = effective.Count,
            },
            truncated = truncated
                ? true
                : (bool?)null,
            note = truncated
                ? $"A list holds at most {RoleToolSupport.MaxPermissionNames} names. Give filter to narrow the lists."
                : "effectivePermissions is what a user who holds only this role may do: the assigned permissions, the ones they imply, and the ones every user gets from Anonymous and Authenticated. Use listPermissions for what a permission means.",
        });
    }

    private static async Task<object> CompareAsync(IServiceProvider services, Role role, Role other, string filter)
    {
        var roleService = services.GetRequiredService<IRoleService>();
        var roleIsAdmin = await roleService.IsAdminRoleAsync(role.RoleName);
        var otherIsAdmin = await roleService.IsAdminRoleAsync(other.RoleName);

        // The administrator role holds every permission without a list, so there is nothing to set side by side.
        if (roleIsAdmin || otherIsAdmin)
        {
            var adminRole = roleIsAdmin
                ? role.RoleName
                : other.RoleName;

            return AgentToolResults.Json(new
            {
                role = role.RoleName,
                comparedWith = other.RoleName,
                holdsEveryPermission = adminRole,
                note = roleIsAdmin && otherIsAdmin
                    ? "Both roles are administrator roles: each holds every permission."
                    : $"'{adminRole}' is the administrator role and holds every permission, so it can do everything the other role can. Use getRole on the other role for its permissions.",
            });
        }

        var installed = await RoleToolSupport.GetInstalledPermissionsAsync(services);
        var roleEffective = await GetEffectiveAsync(services, role, installed);
        var otherEffective = await GetEffectiveAsync(services, other, installed);
        var roleAssigned = RoleToolSupport.GetAssignedPermissionNames(role);
        var otherAssigned = RoleToolSupport.GetAssignedPermissionNames(other);
        var truncated = false;

        return AgentToolResults.Json(new
        {
            role = role.RoleName,
            comparedWith = other.RoleName,
            effectivePermissions = new
            {
                onlyInRole = RoleToolSupport.Limit(roleEffective.Except(otherEffective, StringComparer.OrdinalIgnoreCase), filter, ref truncated),
                onlyInComparedRole = RoleToolSupport.Limit(otherEffective.Except(roleEffective, StringComparer.OrdinalIgnoreCase), filter, ref truncated),
                inBoth = RoleToolSupport.Limit(roleEffective.Intersect(otherEffective, StringComparer.OrdinalIgnoreCase), filter, ref truncated),
            },
            assignedPermissions = new
            {
                onlyInRole = RoleToolSupport.Limit(roleAssigned.Except(otherAssigned, StringComparer.OrdinalIgnoreCase), filter, ref truncated),
                onlyInComparedRole = RoleToolSupport.Limit(otherAssigned.Except(roleAssigned, StringComparer.OrdinalIgnoreCase), filter, ref truncated),
                inBoth = RoleToolSupport.Limit(roleAssigned.Intersect(otherAssigned, StringComparer.OrdinalIgnoreCase), filter, ref truncated),
            },
            truncated = truncated
                ? true
                : (bool?)null,
            note = truncated
                ? $"A list holds at most {RoleToolSupport.MaxPermissionNames} names. Give filter to narrow the lists."
                : "effectivePermissions is what a user who holds only that role may do, including what every user gets from Anonymous and Authenticated; assignedPermissions is what each role is given directly.",
        });
    }

    // What a user who holds only this role may do. The authorization service is asked, with a principal that has the
    // role and its claims and that is signed in unless the role is Anonymous, as the admin page asks it.
    private static async Task<List<string>> GetEffectiveAsync(IServiceProvider services, Role role, List<InstalledPermission> installed)
    {
        var authenticationType = string.Equals(role.RoleName, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase)
            ? null
            : "RolePermissions";

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role.RoleName), .. role.RoleClaims.Select(claim => claim.ToClaim())],
            authenticationType);

        var principal = new ClaimsPrincipal(identity);
        var authorizationService = services.GetRequiredService<IAuthorizationService>();
        var effective = new List<string>();

        foreach (var permission in installed)
        {
            if (await authorizationService.AuthorizeAsync(principal, permission.Permission))
            {
                effective.Add(permission.Permission.Name);
            }
        }

        return effective;
    }

    private static async Task<List<string>> GetGrantedAsync(IServiceProvider services, string roleName, List<InstalledPermission> installed)
    {
        var claims = (await services.GetRequiredService<IRoleService>().GetRoleClaimsAsync(roleName)).ToList();
        var grantingService = services.GetRequiredService<IPermissionGrantingService>();

        return installed
            .Where(permission => grantingService.IsGranted(new PermissionRequirement(permission.Permission), claims))
            .Select(permission => permission.Permission.Name)
            .ToList();
    }
}
