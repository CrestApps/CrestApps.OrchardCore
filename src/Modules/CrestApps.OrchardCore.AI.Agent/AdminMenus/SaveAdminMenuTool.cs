using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using OrchardCore;

namespace CrestApps.OrchardCore.AI.Agent.AdminMenus;

/// <summary>
/// Creates a custom admin menu or replaces an existing one through the <c>AdminMenu</c> recipe step, which saves the
/// menu with the service the admin pages use. The step runs inside the call, so its errors are returned.
/// </summary>
public sealed class SaveAdminMenuTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAdminMenu";

    private const string TypeMember = "$type";

    private const string NameMember = "Name";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the menu to replace, from listAdminMenus. Leave it out to create a new menu."
        },
        "name": {
          "type": "string",
          "description": "The name of the menu. It also names the permission to view the menu."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the menu shows in the admin. When changing a menu and this is left out, the menu keeps its state; a new menu is enabled."
        },
        "menuItems": {
          "type": "array",
          "description": "The whole node tree of the menu, in the shape listAdminMenus returns with includeNodes: each node has $type (from nodeTypes), its fields such as LinkText, LinkUrl and IconClass, Enabled, and its child nodes in Items. Nodes left out are removed.",
          "items": {
            "type": "object"
          }
        }
      },
      "required": [
        "name",
        "menuItems"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a custom admin menu or replaces an existing one, including its name, enabled state and whole node tree. Send the complete tree: nodes left out are removed.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageAdminMenu))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("id", out var id);
        arguments.TryGetFirstString("name", out var name);

        id = id?.Trim();
        name = name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return "Nothing was saved: give the name of the menu.";
        }

        if (!AgentToolArguments.TryReadJsonArray(arguments, "menuItems", out var menuItems) || menuItems is null)
        {
            return "Nothing was saved: menuItems must be the list of the menu's nodes, in the shape listAdminMenus returns with includeNodes. Send an empty list for a menu without nodes.";
        }

        var menus = await AdminMenuToolSupport.ReadMenusAsync(services);

        if (menus is null)
        {
            return "Nothing was saved: the Admin Menu feature does not provide its export step on this site, so the menu could not be checked.";
        }

        JsonObject existing = null;

        if (!string.IsNullOrEmpty(id))
        {
            existing = menus.FirstOrDefault(menu => string.Equals(AdminMenuToolSupport.Get(menu, "Id")?.ToString(), id, StringComparison.Ordinal));

            if (existing is null)
            {
                return $"Nothing was saved: there is no admin menu with the ID '{id}'. Leave the id out to create a new menu, or call listAdminMenus to find it.";
            }
        }

        var nodeTypes = await AdminMenuToolSupport.GetNodeTypesAsync(services);
        var problems = new List<string>();

        ValidateNodes(menuItems, nodeTypes, "menuItems", problems);

        if (problems.Count > 0)
        {
            return $"Nothing was saved: {string.Join(" ", problems)}";
        }

        var enabled = arguments.TryGetFirst<bool>("enabled", out var givenEnabled)
            ? givenEnabled
            : (existing is null ? null : AdminMenuToolSupport.Get(existing, "Enabled"))?.GetValue<bool>() ?? true;

        var created = existing is null;

        if (created)
        {
            id = IdGenerator.GenerateId();
        }

        var errors = await RecipeStepRunner.RunAsync(services, AdminMenuToolSupport.RecipeStepName, new JsonObject
        {
            ["name"] = AdminMenuToolSupport.RecipeStepName,
            ["data"] = new JsonArray(new JsonObject
            {
                ["Id"] = id,
                [NameMember] = name,
                ["Enabled"] = enabled,
                ["MenuItems"] = menuItems,
            }),
        });

        if (errors.Count > 0)
        {
            return $"Nothing was saved: {string.Join(" ", errors)}";
        }

        // The saved menu document is committed when the request ends, so the result describes what was saved.
        return AgentToolResults.Json(new
        {
            saved = true,
            created,
            id,
            name,
            enabled,
            nodeCount = AdminMenuToolSupport.CountNodes(menuItems),
            link = AgentToolLinks.GetAdminPath(services, "OrchardCore.AdminMenu", "Node", "List", new() { ["id"] = id }),
            note = created
                ? $"The menu also adds the permission 'View Admin Menu - {name}'; give it to the roles that should see the menu."
                : null,
        });
    }

    private static void ValidateNodes(JsonArray nodes, List<AdminNodeType> nodeTypes, string path, List<string> problems)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var nodePath = $"{path}[{i}]";

            if (nodes[i] is not JsonObject node)
            {
                problems.Add($"{nodePath} must be an object.");

                continue;
            }

            var type = node[TypeMember] is JsonValue typeValue && typeValue.TryGetValue<string>(out var typeText) ? typeText : null;

            if (string.IsNullOrEmpty(type))
            {
                problems.Add($"{nodePath} has no $type; use the type of a node type from listAdminMenus with includeNodes.");
            }
            else if (nodeTypes is not null)
            {
                var nodeType = nodeTypes.FirstOrDefault(candidate => string.Equals(candidate.Type, type, StringComparison.Ordinal));

                if (nodeType is null)
                {
                    problems.Add($"{nodePath} has the $type '{type}', which is not a node type of this site. The types are: {string.Join(", ", nodeTypes.Select(candidate => candidate.Type))}.");
                }
                else
                {
                    foreach (var required in nodeType.Required)
                    {
                        var value = AdminMenuToolSupport.Get(node, required);

                        if (value is null || (value is JsonValue text && text.TryGetValue<string>(out var requiredText) && string.IsNullOrWhiteSpace(requiredText)))
                        {
                            problems.Add($"{nodePath} ({nodeType.Name}) needs {required}.");
                        }
                    }
                }
            }

            if (AdminMenuToolSupport.Get(node, "Items") is JsonArray children)
            {
                ValidateNodes(children, nodeTypes, $"{nodePath}.Items", problems);
            }
        }
    }
}
