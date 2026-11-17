using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using OrchardCore.Roles;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Agent.Roles;

/// <summary>
/// Lists the permissions the enabled features provide, the ones the permission matrix of the Roles admin page offers,
/// with the group that page lists each under and whether Orchard Core marks it as security critical.
/// </summary>
public sealed class ListPermissionsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listPermissions";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "filter": {
          "type": "string",
          "description": "Text to find in the name, the description or the group of a permission, for example content or users. Leave it out to list every permission."
        },
        "securityCriticalOnly": {
          "type": "boolean",
          "description": "List only the permissions marked as security critical. The default is false."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of permissions on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the permissions that exist on this site, grouped by feature, with name, description and whether each is security critical; optional text filter. Use it to find exact permission names before saveRole.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, RolesPermissions.ViewRoles))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("filter", out var filter);
        filter = filter?.Trim();

        var securityCriticalOnly = arguments.GetFirstValueOrDefault("securityCriticalOnly", false);
        var installed = await RoleToolSupport.GetInstalledPermissionsAsync(services);
        var matching = installed
            .Where(permission => !securityCriticalOnly || permission.Permission.IsSecurityCritical)
            .Where(permission => string.IsNullOrEmpty(filter) || Matches(permission, filter))
            .OrderBy(permission => permission.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(permission => permission.Permission.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var items = matching
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(permission => new
            {
                name = permission.Permission.Name,
                description = permission.Permission.Description?.Value,
                group = permission.Group,
                feature = permission.FeatureId,
                securityCritical = permission.Permission.IsSecurityCritical
                    ? true
                    : (bool?)null,
                impliedBy = GetImpliedBy(permission.Permission),
            })
            .ToList();

        return AgentToolResults.Json(new
        {
            items,
            total = matching.Count,
            page,
            pageSize,
            truncated = matching.Count > page * pageSize
                ? true
                : (bool?)null,
            note = "A role that holds a permission named in impliedBy also has the permission. Granting a security-critical permission gives control over users, security or the code the site runs.",
        });
    }

    private static string[] GetImpliedBy(Permission permission)
    {
        var names = permission.ImpliedBy?
            .Where(impliedBy => impliedBy is not null)
            .Select(impliedBy => impliedBy.Name)
            .ToArray();

        return names is { Length: > 0 }
            ? names
            : null;
    }

    private static bool Matches(InstalledPermission permission, string filter)
    {
        return permission.Permission.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            (permission.Permission.Description?.Value?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (permission.Group?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
