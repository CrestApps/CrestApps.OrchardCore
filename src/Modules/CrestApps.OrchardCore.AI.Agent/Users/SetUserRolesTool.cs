using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security;
using OrchardCore.Security.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Adds roles to a user and removes roles from a user with the checks of the role editor of the Users admin area: the
/// permission to edit the user, the permission to assign each role, and the guard that keeps one enabled
/// administrator. Unlike that editor it never changes the roles of the caller. Everything is checked before anything
/// is changed, so a refused call leaves the user as it was.
/// </summary>
public sealed class SetUserRolesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setUserRoles";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userId": {
          "type": "string",
          "description": "The ID of the user, from listUsers or getUser."
        },
        "add": {
          "type": "array",
          "description": "The exact names of the roles to give the user, from listRoles.",
          "items": {
            "type": "string"
          }
        },
        "remove": {
          "type": "array",
          "description": "The exact names of the roles to take from the user, from getUser.",
          "items": {
            "type": "string"
          }
        }
      },
      "required": [
        "userId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds roles to a user or removes roles from a user. It refuses to change the calling user's own roles, to remove the administrator role from the only enabled administrator, and to touch a role the caller may not assign. Removing a role signs the user out of their open sessions.";

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

        if (caller is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("userId", out var userId))
        {
            return "Nothing was changed: give the userId of the user. Use listUsers to find it.";
        }

        var (user, error) = await UserToolAccess.FindAsync(services, userId, UsersPermissions.EditUsers);

        if (error is not null)
        {
            return error;
        }

        // The role editor lets an administrator change their own roles. A chat must not: one sentence could lock the
        // caller out or raise the caller's own access.
        if (UserToolAccess.IsCaller(caller, user))
        {
            return "Nothing was changed: this tool does not change the roles of your own account. Another administrator can change them on the Users page.";
        }

        var givenAdds = ReadNames(arguments, "add");
        var givenRemoves = ReadNames(arguments, "remove");

        if (givenAdds.Count == 0 && givenRemoves.Count == 0)
        {
            return "Nothing was changed: give add or remove with the names of roles.";
        }

        // The roles a user can hold: every role except the system roles Anonymous and Authenticated.
        var assignableRoles = (await services.GetRequiredService<IRoleService>().GetAssignableRolesAsync()).ToList();
        var adds = new List<IRole>();
        var removes = new List<IRole>();
        var unknown = new List<string>();

        Resolve(assignableRoles, givenAdds, adds, unknown);
        Resolve(assignableRoles, givenRemoves, removes, unknown);

        if (unknown.Count > 0)
        {
            return $"Nothing was changed: {Quote(unknown)} cannot be given to or taken from a user. The roles that can are: {string.Join(", ", assignableRoles.Select(role => role.RoleName).Order(StringComparer.OrdinalIgnoreCase))}.";
        }

        var both = adds.Where(role => removes.Contains(role)).Select(role => role.RoleName).ToList();

        if (both.Count > 0)
        {
            return $"Nothing was changed: {Quote(both)} is in both add and remove.";
        }

        // The check of the role editor, for every role the call touches: the permission to assign roles, on the role.
        var notAssignable = new List<string>();

        foreach (var role in adds.Concat(removes))
        {
            if (!await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.AssignRoleToUsers, role))
            {
                notAssignable.Add(role.RoleName);
            }
        }

        if (notAssignable.Count > 0)
        {
            return $"Nothing was changed: you are not allowed to assign the role {Quote(notAssignable)}.";
        }

        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var roleStore = services.GetRequiredService<IUserRoleStore<IUser>>();
        var toAdd = new List<string>();
        var toRemove = new List<string>();
        var unchanged = new List<string>();

        foreach (var role in adds)
        {
            if (await roleStore.IsInRoleAsync(user, userManager.NormalizeName(role.RoleName), cancellationToken))
            {
                unchanged.Add(role.RoleName);
            }
            else
            {
                toAdd.Add(role.RoleName);
            }
        }

        foreach (var role in removes)
        {
            if (await roleStore.IsInRoleAsync(user, userManager.NormalizeName(role.RoleName), cancellationToken))
            {
                toRemove.Add(role.RoleName);
            }
            else
            {
                unchanged.Add(role.RoleName);
            }
        }

        if (toAdd.Count == 0 && toRemove.Count == 0)
        {
            return "Nothing was changed: the user already has the roles to add and has none of the roles to remove.";
        }

        // The site must keep one enabled administrator. The name of the administrator role is configurable.
        var administratorRole = await UserToolAccess.GetAdministratorRoleAsync(services, toRemove);

        if (administratorRole is not null && await UserToolAccess.IsOnlyEnabledAdministratorAsync(services, user))
        {
            return $"Nothing was changed: '{user.UserName}' is the only enabled administrator, so the role '{administratorRole}' cannot be removed. Give the role to another enabled user first.";
        }

        var previousRoles = user.RoleNames.ToList();

        foreach (var roleName in toRemove)
        {
            await roleStore.RemoveFromRoleAsync(user, userManager.NormalizeName(roleName), cancellationToken);
        }

        foreach (var roleName in toAdd)
        {
            await roleStore.AddToRoleAsync(user, userManager.NormalizeName(roleName), cancellationToken);
        }

        // As in the role editor, a removed role changes the security stamp, which ends the sessions the user has
        // open. Both calls validate and store the user.
        var result = toRemove.Count > 0
            ? await userManager.UpdateSecurityStampAsync(user)
            : await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            // The loaded user is shared with the rest of the request, so the roles are put back.
            user.RoleNames = previousRoles;

            return $"Nothing was changed: {string.Join(" ", result.Errors.Select(identityError => identityError.Description))}".TrimEnd();
        }

        return AgentToolResults.Json(new
        {
            changed = true,
            userId = user.UserId,
            userName = user.UserName,
            added = toAdd,
            removed = toRemove,
            unchanged = unchanged.Count > 0
                ? unchanged
                : null,
            roles = user.RoleNames.ToArray(),
            note = toRemove.Count > 0
                ? "The user is signed out of their open sessions and signs in again with the new roles."
                : "The new roles apply the next time the user signs in or their session is refreshed.",
        });
    }

    private static List<string> ReadNames(AIFunctionArguments arguments, string key)
    {
        var names = new List<string>();

        if (!arguments.TryGetFirst<string[]>(key, out var given) || given is null)
        {
            return names;
        }

        foreach (var name in given)
        {
            var trimmed = name?.Trim();

            if (!string.IsNullOrEmpty(trimmed) && !names.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(trimmed);
            }
        }

        return names;
    }

    private static void Resolve(List<IRole> assignableRoles, List<string> names, List<IRole> roles, List<string> unknown)
    {
        foreach (var name in names)
        {
            var role = assignableRoles.FirstOrDefault(candidate => string.Equals(candidate.RoleName, name, StringComparison.OrdinalIgnoreCase));

            if (role is null)
            {
                unknown.Add(name);
            }
            else
            {
                roles.Add(role);
            }
        }
    }

    private static string Quote(IEnumerable<string> names)
        => string.Join(", ", names.Select(name => $"'{name}'"));
}
