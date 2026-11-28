using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Documents;
using OrchardCore.Roles;
using OrchardCore.Security;
using OrchardCore.Security.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Agent.Roles;

/// <summary>
/// Deletes a role through the role manager, as the Delete action of the Roles admin page does. System roles are
/// refused, as on that page. The handlers Orchard Core runs when a role is removed take the role from the users and
/// the OpenID applications that have it.
/// </summary>
public sealed class DeleteRoleTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteRole";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "roleName": {
          "type": "string",
          "description": "The exact name of the role to delete, from listRoles."
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
    public override string Description => "Deletes a custom role and removes it from every user and OpenID application that has it; this cannot be undone. System roles cannot be deleted.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, RolesPermissions.ManageRoles))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("roleName", out var roleName))
        {
            return "Nothing was deleted: give roleName. Use listRoles to get the names of the roles.";
        }

        var role = await RoleToolSupport.FindRoleAsync(services, roleName);

        if (role is null)
        {
            return $"Nothing was deleted: there is no role named '{roleName}'. Use listRoles to get the names of the roles.";
        }

        // The check of the Delete action.
        if (await services.GetRequiredService<IRoleService>().IsSystemRoleAsync(role.RoleName))
        {
            return $"Nothing was deleted: '{role.RoleName}' is a system role, and system roles cannot be deleted.";
        }

        // Counted before the role is removed from the users. The user manager is there when the Users feature is on.
        var userManager = services.GetService<UserManager<IUser>>();
        var usersInRole = userManager is null
            ? (int?)null
            : (await userManager.GetUsersInRoleAsync(role.RoleName)).Count;

        var result = await services.GetRequiredService<RoleManager<IRole>>().DeleteAsync(role);

        if (!result.Succeeded)
        {
            // As the Delete action does when it fails.
            await services.GetRequiredService<IDocumentStore>().CancelAsync();

            return $"Nothing was deleted: {string.Join(" ", result.Errors.Select(identityError => identityError.Description))}".TrimEnd();
        }

        return AgentToolResults.Json(new
        {
            deleted = true,
            roleName = role.RoleName,
            usersThatHadTheRole = usersInRole,
            note = "The role cannot be restored. It was taken from the users and the OpenID applications that had it, and those users are signed out of their open sessions.",
        });
    }
}
