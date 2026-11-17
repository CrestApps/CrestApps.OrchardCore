using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Users;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Data.Documents;
using OrchardCore.Roles;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Agent.Roles;

/// <summary>
/// Creates a role, or changes the description and the permissions of a role, through the role manager the Roles
/// admin page uses, with the rules of that page (a role name without a slash that no other role has; the permissions
/// of the administrator role cannot be changed) and four more a chat needs: only permissions an enabled feature
/// provides, no security-critical permission for Anonymous or Authenticated, no new permission on a role the caller
/// holds, and every security-critical permission named in the call so the person who approves sees it. Everything is
/// checked before anything is changed.
/// </summary>
public sealed class SaveRoleTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveRole";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "roleName": {
          "type": "string",
          "description": "The name of the role to update, from listRoles, or the name of the role to create when create is true."
        },
        "create": {
          "type": "boolean",
          "description": "True to create a role with this name; the call is refused when a role with the name exists. False or left out to update a role; the call is refused when there is no role with the name."
        },
        "description": {
          "type": "string",
          "description": "The description of the role. Leave it out to keep the description of a role that is updated."
        },
        "permissions": {
          "type": "array",
          "description": "Exact permission names, from listPermissions. What is done with them is set by permissionMode. Leave it out to keep the permissions.",
          "items": {
            "type": "string"
          }
        },
        "permissionMode": {
          "type": "string",
          "enum": [
            "add",
            "remove",
            "replace"
          ],
          "description": "add (default) gives the role the listed permissions and keeps the others; remove takes the listed permissions away; replace makes the listed permissions the only ones assigned to the role."
        },
        "copyPermissionsFrom": {
          "type": "string",
          "description": "For a new role only: the name of a role whose assigned permissions the new role starts with. The permissions list is added to them."
        },
        "securityCriticalPermissions": {
          "type": "array",
          "description": "The security-critical permissions this call gives the role, named again so the person who approves the call sees them. The call is refused, and says which ones are missing, when it would give a security-critical permission that is not listed here.",
          "items": {
            "type": "string"
          }
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
    public override string Description => "Creates a role, or updates a role's description and permissions (add, remove or replace; a new role can copy the permissions of another role). It refuses to change the administrator role's permissions, to give a security-critical permission to Anonymous or Authenticated, and to add permissions to a role the caller holds. It names every security-critical permission it would grant: list them in securityCriticalPermissions to confirm.";

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
        var caller = AgentToolAuthorization.GetCaller(services);

        if (caller is null || !await AIToolCallerAuthorization.IsAuthorizedAsync(services, RolesPermissions.ManageRoles))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("roleName", out var roleName) || string.IsNullOrWhiteSpace(roleName))
        {
            return "Nothing was saved: give roleName. Use listRoles to get the names of the roles.";
        }

        roleName = roleName.Trim();

        var create = arguments.GetFirstValueOrDefault("create", false);
        var hasDescription = arguments.TryGetFirstString("description", true, out var description);
        var hasPermissions = arguments.TryGetFirst<string[]>("permissions", out var givenPermissions) && givenPermissions is not null;
        var hasCopyFrom = arguments.TryGetFirstString("copyPermissionsFrom", out var copyPermissionsFrom);
        var permissionNames = CleanNames(givenPermissions);
        var mode = "add";

        if (arguments.TryGetFirstString("permissionMode", out var permissionMode))
        {
            mode = permissionMode.Trim().ToLowerInvariant();

            if (mode is not ("add" or "remove" or "replace"))
            {
                return "Nothing was saved: permissionMode must be add, remove or replace.";
            }
        }

        var roleManager = services.GetRequiredService<RoleManager<IRole>>();
        var roleService = services.GetRequiredService<IRoleService>();
        var current = new List<string>();
        var copied = new List<string>();
        Role role = null;

        if (create)
        {
            // The two checks of the Create page.
            if (roleName.Contains('/'))
            {
                return "Nothing was saved: a role name cannot contain a slash.";
            }

            if (await roleManager.FindByNameAsync(roleName) is not null)
            {
                return $"Nothing was saved: a role named '{roleName}' exists. Leave create out to update it, or pick another name.";
            }

            if (mode == "remove" || (mode == "replace" && hasCopyFrom))
            {
                return "Nothing was saved: a new role gets the copied permissions and the listed ones. Use permissionMode add.";
            }

            if (hasCopyFrom)
            {
                var source = await RoleToolSupport.FindRoleAsync(services, copyPermissionsFrom);

                if (source is null)
                {
                    return $"Nothing was saved: there is no role named '{copyPermissionsFrom}' to copy permissions from. Use listRoles to get the names of the roles.";
                }

                if (await roleService.IsAdminRoleAsync(source.RoleName))
                {
                    return "Nothing was saved: the administrator role holds every permission without a list of its own, so there is nothing to copy. List the permissions the new role needs.";
                }

                copied = RoleToolSupport.GetAssignedPermissionNames(source);
            }
        }
        else
        {
            role = await RoleToolSupport.FindRoleAsync(services, roleName);

            if (role is null)
            {
                return $"Nothing was saved: there is no role named '{roleName}'. Use listRoles to get the names, or set create to true to create the role.";
            }

            if (hasCopyFrom)
            {
                return "Nothing was saved: copyPermissionsFrom is for a new role only. Give the permissions to add to this role.";
            }

            if (!hasDescription && !hasPermissions)
            {
                return "Nothing was saved: give description or permissions to change the role.";
            }

            // The Edit page ignores the permissions posted for the administrator role; the tool says so.
            if (hasPermissions && await roleService.IsAdminRoleAsync(role.RoleName))
            {
                return "Nothing was saved: the administrator role holds every permission and its permissions cannot be changed. Only its description can be changed.";
            }

            current = RoleToolSupport.GetAssignedPermissionNames(role);
        }

        var installed = (await RoleToolSupport.GetInstalledPermissionsAsync(services))
            .ToDictionary(permission => permission.Permission.Name, permission => permission.Permission, StringComparer.OrdinalIgnoreCase);

        // As on the Clone page, a copy takes the permissions an enabled feature provides and leaves the others out.
        var notCopied = copied.Where(name => !installed.ContainsKey(name)).ToList();

        // The permissions the role ends with: for a new role, the copied ones plus the listed ones.
        var final = create
            ? copied.Where(installed.ContainsKey).ToList()
            : new List<string>(current);

        var removed = new List<string>();
        var notAssigned = new List<string>();

        if (mode == "remove")
        {
            foreach (var name in permissionNames)
            {
                if (final.RemoveAll(assigned => string.Equals(assigned, name, StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    removed.Add(name);
                }
                else
                {
                    notAssigned.Add(name);
                }
            }
        }
        else if (hasPermissions)
        {
            if (mode == "replace")
            {
                removed = final.Where(assigned => !permissionNames.Contains(assigned, StringComparer.OrdinalIgnoreCase)).ToList();
                final = final.Where(assigned => permissionNames.Contains(assigned, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            foreach (var name in permissionNames)
            {
                if (!final.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    final.Add(name);
                }
            }
        }

        // What the call gives that the role (or, for a copy, no role) had not: the names that need the checks below.
        var granted = create
            ? final
            : final.Where(name => !current.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList();

        // The permission matrix of the admin page offers the permissions of the enabled features only.
        var unknown = granted.Where(name => !installed.ContainsKey(name)).ToList();

        if (unknown.Count > 0)
        {
            return $"Nothing was saved: no enabled feature provides the permission {Quote(unknown)}. Use listPermissions to get the exact names.";
        }

        // The stored name is the name the feature gives the permission.
        final = final.Select(name => installed.TryGetValue(name, out var permission) ? permission.Name : name).ToList();
        granted = granted.Select(name => installed[name].Name).ToList();

        var securityCritical = granted.Where(name => installed[name].IsSecurityCritical).ToList();

        // Anonymous is every visitor and Authenticated is every signed-in user.
        if (securityCritical.Count > 0 && IsEveryoneRole(roleName))
        {
            return $"Nothing was saved: {Quote(securityCritical)} is security critical and cannot be given to the role '{roleName}', which every {(IsAnonymousRole(roleName) ? "visitor" : "signed-in user")} holds. Give it to a role that is assigned to chosen users.";
        }

        // A person with the permission to manage roles must not raise their own access through a chat.
        if (!create && granted.Count > 0 && await CallerHoldsRoleAsync(services, caller, role.RoleName))
        {
            return $"Nothing was saved: you hold the role '{role.RoleName}', and this tool does not add permissions to a role of the person who asks. An administrator can add them on the Roles page.";
        }

        var confirmed = CleanNames(arguments.GetFirstValueOrDefault<string[]>("securityCriticalPermissions"));
        var notConfirmed = securityCritical.Where(name => !confirmed.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList();

        if (notConfirmed.Count > 0)
        {
            return $"Nothing was saved: this call would give the role '{roleName}' the security-critical permission {Quote(notConfirmed)}. A security-critical permission gives control over users, security or the code the site runs. Tell the person which ones the role would get, and call again with all of them listed in securityCriticalPermissions.";
        }

        if (!create && !hasDescription && granted.Count == 0 && removed.Count == 0)
        {
            return mode == "remove"
                ? "Nothing was saved: the role has none of these permissions."
                : "Nothing was saved: the role already has these permissions.";
        }

        IdentityResult result;

        if (create)
        {
            role = new Role
            {
                RoleName = roleName,
                RoleDescription = description,
                RoleClaims = [.. final.Select(RoleClaim.Create)],
            };

            result = await roleManager.CreateAsync(role);
        }
        else
        {
            if (hasDescription)
            {
                role.RoleDescription = description;
            }

            if (hasPermissions)
            {
                // As the Edit page does: the permission claims are replaced and the other claims of the role are kept.
                role.RoleClaims.RemoveAll(claim => claim.ClaimType == Permission.ClaimType);
                role.RoleClaims.AddRange(final.Select(RoleClaim.Create));
            }

            result = await roleManager.UpdateAsync(role);
        }

        if (!result.Succeeded)
        {
            // As the admin page does when the role manager refuses.
            await services.GetRequiredService<IDocumentStore>().CancelAsync();

            return $"Nothing was saved: {string.Join(" ", result.Errors.Select(identityError => identityError.Description))}".TrimEnd();
        }

        return AgentToolResults.Json(new
        {
            saved = true,
            created = create,
            role = new
            {
                name = role.RoleName,
                description = role.RoleDescription,
                adminUrl = RoleToolSupport.GetAdminUrl(services, role.RoleName),
            },
            added = granted.Count > 0
                ? granted
                : null,
            removed = removed.Count > 0
                ? removed
                : null,
            notAssigned = notAssigned.Count > 0
                ? notAssigned
                : null,
            notCopied = notCopied.Count > 0
                ? notCopied
                : null,
            securityCriticalGranted = securityCritical.Count > 0
                ? securityCritical
                : null,
            assignedPermissionCount = final.Count,
            note = "Users who hold the role get the change at their next request. Use setUserRoles to give the role to a user.",
        });
    }

    private static async Task<bool> CallerHoldsRoleAsync(IServiceProvider services, ClaimsPrincipal caller, string roleName)
    {
        // The site owner and the administrators hold every permission already, so nothing is raised for them.
        if (await UserToolAccess.IsSiteOwnerAsync(services, caller.FindFirstValue(ClaimTypes.NameIdentifier)))
        {
            return false;
        }

        // The roles of the sign-in, and the stored ones in case they changed since the caller signed in.
        var callerRoles = caller.Identities
            .SelectMany(identity => identity.FindAll(identity.RoleClaimType))
            .Select(claim => claim.Value)
            .ToList();

        var userManager = services.GetService<UserManager<IUser>>();

        if (userManager is not null && await userManager.GetUserAsync(caller) is User user)
        {
            callerRoles.AddRange(user.RoleNames ?? []);
        }

        if (await UserToolAccess.GetAdministratorRoleAsync(services, callerRoles) is not null)
        {
            return false;
        }

        return IsEveryoneRole(roleName) || callerRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAnonymousRole(string roleName)
        => string.Equals(roleName, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase);

    private static bool IsEveryoneRole(string roleName)
        => IsAnonymousRole(roleName) || string.Equals(roleName, OrchardCoreConstants.Roles.Authenticated, StringComparison.OrdinalIgnoreCase);

    private static List<string> CleanNames(IEnumerable<string> names)
    {
        var cleaned = new List<string>();

        foreach (var name in names ?? [])
        {
            var trimmed = name?.Trim();

            if (!string.IsNullOrEmpty(trimmed) && !cleaned.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                cleaned.Add(trimmed);
            }
        }

        return cleaned;
    }

    private static string Quote(IEnumerable<string> names)
        => string.Join(", ", names.Select(name => $"'{name}'"));
}
