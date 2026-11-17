using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Flows;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.AI.Agent.Menus;

/// <summary>
/// Returns the items of a menu as a flat, ordered tree, or lists the menus of the site.
/// </summary>
public sealed class GetMenuTreeTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getMenuTree";

    private const string MenuContentType = "Menu";

    private const int MaxItems = 200;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "menu": {
          "type": "string",
          "description": "The menu: its content item ID, its alias (for example main-menu) or its title. Leave it out to list the menus of the site."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns a menu's items as a flat, ordered tree: item ID, type, title, URL or linked content item, parent ID, depth and required permissions. The menu is given by content item ID, alias or title; without one it lists the site's menus. It reads the latest version of the menu, which is a draft when the menu has unpublished changes.";

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

        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("menu", out var menuName))
        {
            return await ListMenusAsync(services);
        }

        var (menu, error) = await ContentItemLookup.FindAsync(services, MenuContentType, menuName);

        if (error is not null)
        {
            return error;
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, menu))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var definition = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(menu.ContentType);

        if (!MenuContent.IsMenu(definition))
        {
            return $"Content item '{menu.ContentItemId}' is a '{menu.ContentType}', not a menu. Call this tool without a menu to list the menus.";
        }

        var root = NestedContentItemTree.ToJson(services, menu);
        var entries = ContentItemListTree.Menu.GetEntries(root);

        return AgentToolResults.Json(new
        {
            menuContentItemId = menu.ContentItemId,
            displayText = menu.DisplayText,
            alias = MenuContent.GetAlias(root),
            published = menu.Published,
            note = menu.Published
                ? null
                : "This is a draft of the menu. The site shows the published version until the menu is published.",
            items = entries.Take(MaxItems).Select(MenuContent.Describe),
            total = entries.Count,
            truncated = entries.Count > MaxItems
                ? true
                : (bool?)null,
        });
    }

    private static async Task<string> ListMenusAsync(IServiceProvider services)
    {
        var menus = new List<object>();
        var found = false;

        foreach (var menu in await ContentItemLookup.ListLatestAsync(services, MenuContentType))
        {
            found = true;

            if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, menu))
            {
                continue;
            }

            menus.Add(new
            {
                menuContentItemId = menu.ContentItemId,
                displayText = menu.DisplayText,
                alias = MenuContent.GetAlias(NestedContentItemTree.ToJson(services, menu)),
                published = menu.Published,
            });
        }

        if (found && menus.Count == 0)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        return AgentToolResults.Json(new
        {
            menus,
            total = menus.Count,
        });
    }
}
