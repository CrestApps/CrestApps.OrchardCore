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
/// come from the Anonymous and Authenticated roles.
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
    public override string Description => "Gets a role's description and permissions: the ones assigned directly and the effective ones, including those inherited from Anonymous and Authenticated. Use it to answer \"what can this role do\" and before saveRole.";

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

        // What a user who holds only this role may do. The authorization service is asked, with a principal that has
        // the role and its claims and that is signed in unless the role is Anonymous, as the admin page asks it.
        var authenticationType = isAnonymousRole
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
