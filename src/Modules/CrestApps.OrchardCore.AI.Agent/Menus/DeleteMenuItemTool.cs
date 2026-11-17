using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Flows;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.AI.Agent.Menus;

/// <summary>
/// Deletes one item of a menu with the items under it, and saves the menu through the content manager.
/// </summary>
public sealed class DeleteMenuItemTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteMenuItem";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "menuContentItemId": {
          "type": "string",
          "description": "The content item ID of the menu, from getMenuTree."
        },
        "menuItemId": {
          "type": "string",
          "description": "The ID of the menu item to delete, from getMenuTree."
        },
        "publish": {
          "type": "boolean",
          "description": "Whether the menu is published after the change, which makes it live. The default is false: the change is saved in a draft of the menu."
        }
      },
      "required": [
        "menuContentItemId",
        "menuItemId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a menu item and all its child items from a menu. The change is saved as a draft of the menu unless publish is true.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageMenu))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("menuContentItemId", out var menuContentItemId))
        {
            return "The menuContentItemId argument is required.";
        }

        if (!arguments.TryGetFirstString("menuItemId", out var menuItemId))
        {
            return "The menuItemId argument is required.";
        }

        var publish = arguments.GetFirstValueOrDefault("publish", false);
        var menu = await services.GetRequiredService<IContentManager>().GetAsync(menuContentItemId, VersionOptions.Latest);

        if (menu is null)
        {
            return $"No content item with the ID '{menuContentItemId}' was found. Use getMenuTree to list the menus.";
        }

        if (publish && !await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.PublishContent, menu))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var menuDefinition = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(menu.ContentType);

        if (!MenuContent.IsMenu(menuDefinition))
        {
            return $"Nothing was deleted: content item '{menu.ContentItemId}' is a '{menu.ContentType}', not a menu.";
        }

        var tree = ContentItemListTree.Menu;
        var root = NestedContentItemTree.ToJson(services, menu);
        var entries = tree.GetEntries(root);
        var entry = NestedContentItemTree.Find(entries, menuItemId);

        if (entry is null)
        {
            return $"Nothing was deleted: menu '{menu.ContentItemId}' has no item with the ID '{menuItemId}'. Use getMenuTree to get the IDs.";
        }

        var deletedChildItems = ContentItemListTree.CountDescendants(entries, entry);

        entry.Container.Items.RemoveAt(entry.Position);

        var (saved, saveError) = await tree.SaveAsync(services, menu, menuDefinition, root, publish);

        if (saveError is not null)
        {
            return saveError.Replace("Nothing was saved", "Nothing was deleted", StringComparison.Ordinal);
        }

        return AgentToolResults.Json(new
        {
            deleted = true,
            menuContentItemId = saved.ContentItemId,
            menuItemId = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.ContentItemId)),
            title = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.DisplayText)),
            deletedChildItems,
            published = saved.Published,
            note = tree.GetStateNote(saved),
        });
    }
}
