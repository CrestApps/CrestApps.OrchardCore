using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Roles;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.AI.Agent.Roles;

/// <summary>
/// Lists the roles of the site, as the Roles admin page does: name, description, and whether a role is a system role
/// or the administrator role.
/// </summary>
public sealed class ListRolesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listRoles";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of roles on a page. The default and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists all roles with their description and whether each is a system role or the administrator role. Use it to get exact role names.";

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

        var roleService = services.GetRequiredService<IRoleService>();
        var roles = (await roleService.GetRolesAsync())
            .OrderBy(role => role.RoleName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // A site has few roles, so a page holds as many as a list may return.
        var page = Math.Max(1, arguments.GetFirstValueOrDefault("page", 1));
        var pageSize = Math.Clamp(arguments.GetFirstValueOrDefault("pageSize", AgentToolResults.MaxPageSize), 1, AgentToolResults.MaxPageSize);
        var items = new List<object>();

        foreach (var role in roles.Skip((page - 1) * pageSize).Take(pageSize))
        {
            var isSystemRole = await roleService.IsSystemRoleAsync(role.RoleName);

            items.Add(new
            {
                name = role.RoleName,
                description = role.RoleDescription,
                isSystemRole,
                isAdminRole = isSystemRole && await roleService.IsAdminRoleAsync(role.RoleName),
            });
        }

        return AgentToolResults.Json(new
        {
            items,
            total = roles.Count,
            page,
            pageSize,
            truncated = roles.Count > page * pageSize
                ? true
                : (bool?)null,
            note = "System roles cannot be deleted. The administrator role holds every permission, and Anonymous and Authenticated apply to every visitor and every signed-in user.",
        });
    }
}
