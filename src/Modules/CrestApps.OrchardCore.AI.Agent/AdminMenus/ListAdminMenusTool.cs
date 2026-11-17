using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.AdminMenus;

/// <summary>
/// Lists the custom admin menus, as the Admin Menus page does, optionally with the node tree of each menu in the shape
/// <c>saveAdminMenu</c> takes, and the node types this site offers.
/// </summary>
public sealed class ListAdminMenusTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAdminMenus";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "menuId": {
          "type": "string",
          "description": "The ID of one menu to return. Leave it out to list every custom admin menu."
        },
        "includeNodes": {
          "type": "boolean",
          "description": "Whether to return the full node tree of each menu and the node types this site offers. Ask for it before changing a menu, so the nodes to keep are sent back. The default is false."
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
          "description": "The number of menus on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the custom admin menus, optionally with their full node tree and the node types available on this site. Call it before changing a menu so existing nodes are kept.";

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

        arguments.TryGetFirstString("menuId", out var menuId);
        var includeNodes = arguments.GetFirstValueOrDefault("includeNodes", false);

        var menus = await AdminMenuToolSupport.ReadMenusAsync(services);

        if (menus is null)
        {
            return "The admin menus cannot be read: the Admin Menu feature does not provide its export step on this site.";
        }

        var selected = string.IsNullOrWhiteSpace(menuId)
            ? menus
            : menus.Where(menu => string.Equals(AdminMenuToolSupport.Get(menu, "Id")?.ToString(), menuId.Trim(), StringComparison.Ordinal)).ToList();

        if (!string.IsNullOrWhiteSpace(menuId) && selected.Count == 0)
        {
            return $"There is no admin menu with the ID '{menuId.Trim()}'. Call listAdminMenus without a menuId to see the menus.";
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        var items = selected
            .OrderBy(menu => AdminMenuToolSupport.Get(menu, "Name")?.ToString(), StringComparer.OrdinalIgnoreCase)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(menu =>
            {
                var nodes = AdminMenuToolSupport.Get(menu, "MenuItems") as JsonArray;

                return new
                {
                    id = AdminMenuToolSupport.Get(menu, "Id")?.ToString(),
                    name = AdminMenuToolSupport.Get(menu, "Name")?.ToString(),
                    enabled = AdminMenuToolSupport.Get(menu, "Enabled")?.GetValue<bool>() ?? true,
                    nodeCount = AdminMenuToolSupport.CountNodes(nodes),
                    menuItems = includeNodes ? nodes?.DeepClone() : null,
                };
            })
            .ToList();

        return AgentToolResults.Json(new
        {
            items,
            total = selected.Count,
            page,
            pageSize,
            truncated = selected.Count > page * pageSize
                ? true
                : (bool?)null,
            nodeTypes = includeNodes ? await AdminMenuToolSupport.GetNodeTypesAsync(services) : null,
            link = AgentToolLinks.GetAdminPath(services, "OrchardCore.AdminMenu", "Menu", "List"),
            note = includeNodes
                ? "Each node keeps its $type. To change a menu, send its whole menuItems tree back to saveAdminMenu with the changes: nodes left out are removed."
                : "Call again with includeNodes true to get the node trees before changing a menu.",
        });
    }
}
