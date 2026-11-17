using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Flows;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Infrastructure.Html;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Agent.Menus;

/// <summary>
/// Adds, updates or moves one item of a menu, and saves the menu through the content manager. The other items of the
/// menu are left as they are.
/// </summary>
public sealed class SaveMenuItemTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveMenuItem";

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
          "description": "The ID of the menu item to update or move, from getMenuTree. Leave it out to add a new menu item."
        },
        "contentType": {
          "type": "string",
          "description": "For a new menu item: its content type, which must have the MenuItem stereotype. LinkMenuItem is a link to a URL, ContentMenuItem a link to a content item, HtmlMenuItem a link with HTML as its text."
        },
        "title": {
          "type": "string",
          "description": "The text of the menu item. Required for a new menu item."
        },
        "url": {
          "type": "string",
          "description": "For a link or HTML menu item: the URL, for example /careers, ~/careers or https://example.com."
        },
        "target": {
          "type": "string",
          "description": "For a link or HTML menu item: the target of the link, for example _blank. An empty text clears it."
        },
        "html": {
          "type": "string",
          "description": "For an HTML menu item: the HTML shown as the text of the link."
        },
        "linkedContentItemId": {
          "type": "string",
          "description": "For a content menu item: the ID of the content item it links to. Required for a new content menu item."
        },
        "checkContentPermissions": {
          "type": "boolean",
          "description": "For a content menu item: whether the item is shown only to users who may view the linked content item."
        },
        "permissionNames": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "description": "The names of the permissions a user needs to see the menu item, when its content type has the menu item permission part. The list replaces the stored one; an empty list shows the item to everyone. It hides the link; it does not protect the page."
        },
        "parentMenuItemId": {
          "type": "string",
          "description": "The ID of the menu item that takes this item under it. Give the menu's own content item ID for the top level. Leave it out to add at the top level, or to keep an existing item where it is."
        },
        "position": {
          "type": "integer",
          "minimum": 0,
          "description": "The position among the items of the same parent, starting at 0. Leave it out to add at the end, or to keep the position of an existing item."
        },
        "publish": {
          "type": "boolean",
          "description": "Whether the menu is published after the change, which makes it live. The default is false: the change is saved in a draft of the menu."
        }
      },
      "required": [
        "menuContentItemId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds an item to a menu, updates an existing item, or moves it under another parent or to another position, leaving the other items untouched. Supports link, content and HTML menu items; the change is saved as a draft of the menu unless publish is true.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageMenu))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("menuContentItemId", out var menuContentItemId))
        {
            return "The menuContentItemId argument is required.";
        }

        var hasPosition = arguments.TryGetFirst<int>("position", out var position);

        if (hasPosition && position < 0)
        {
            return "Nothing was saved: position must be 0 or more.";
        }

        var publish = arguments.GetFirstValueOrDefault("publish", false);
        var contentManager = services.GetRequiredService<IContentManager>();
        var menu = await contentManager.GetAsync(menuContentItemId, VersionOptions.Latest);

        if (menu is null)
        {
            return $"No content item with the ID '{menuContentItemId}' was found. Use getMenuTree to list the menus.";
        }

        if (publish && !await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.PublishContent, menu))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var menuDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(menu.ContentType);

        if (!MenuContent.IsMenu(menuDefinition))
        {
            return $"Nothing was saved: content item '{menu.ContentItemId}' is a '{menu.ContentType}', not a menu.";
        }

        var tree = ContentItemListTree.Menu;
        var root = NestedContentItemTree.ToJson(services, menu);
        var entries = tree.GetEntries(root);
        var hasParent = arguments.TryGetFirstString("parentMenuItemId", out var parentMenuItemId);

        NestedContentItemEntry entry = null;
        JsonObject item;
        ContentTypeDefinition itemDefinition;

        if (arguments.TryGetFirstString("menuItemId", out var menuItemId))
        {
            entry = NestedContentItemTree.Find(entries, menuItemId);

            if (entry is null)
            {
                return $"Nothing was saved: menu '{menu.ContentItemId}' has no item with the ID '{menuItemId}'. Use getMenuTree to get the IDs.";
            }

            item = entry.Item;

            var itemType = NestedContentItemTree.GetString(item, nameof(ContentItem.ContentType));

            itemDefinition = string.IsNullOrEmpty(itemType)
                ? null
                : await contentDefinitionManager.GetTypeDefinitionAsync(itemType);
        }
        else
        {
            if (!arguments.TryGetFirstString("contentType", out var contentType))
            {
                return "Nothing was saved: give contentType (LinkMenuItem, ContentMenuItem or HtmlMenuItem) to add a menu item, or menuItemId to update or move one.";
            }

            itemDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType.Trim());

            if (itemDefinition is null)
            {
                return $"Nothing was saved: there is no content type named '{contentType}'.";
            }

            if (!itemDefinition.StereotypeEquals(MenuContent.MenuItemStereotype, StringComparison.OrdinalIgnoreCase))
            {
                return $"Nothing was saved: the content type '{itemDefinition.Name}' does not have the MenuItem stereotype, so it cannot be a menu item.";
            }

            if (!arguments.TryGetFirstString("title", out _))
            {
                return "Nothing was saved: a new menu item needs a title.";
            }

            if (MenuContent.HasPart(itemDefinition, MenuContent.ContentPart) && !arguments.TryGetFirstString("linkedContentItemId", out _))
            {
                return "Nothing was saved: a new content menu item needs linkedContentItemId, the content item it links to.";
            }

            // As the menu editor does, a new item starts from the content type so it gets its own ID.
            item = NestedContentItemTree.ToJson(services, await contentManager.NewAsync(itemDefinition.Name));
        }

        var (updates, error) = await ApplyFieldsAsync(services, arguments, itemDefinition, item);

        if (error is not null)
        {
            return $"Nothing was saved: {error}";
        }

        var moves = entry is null || hasParent || hasPosition;

        if (!updates && !moves)
        {
            return "Nothing was saved: give a field to change (title, url, target, html, linkedContentItemId, checkContentPermissions or permissionNames), or parentMenuItemId or position to move the item.";
        }

        var target = entry?.Container;
        var newPosition = entry?.Position ?? 0;

        if (moves)
        {
            if (hasParent || entry is null)
            {
                target = tree.FindTarget(root, entries, parentMenuItemId);
            }

            if (target is null)
            {
                return $"Nothing was saved: menu '{menu.ContentItemId}' has no item with the ID '{parentMenuItemId}' to put the item under. Use getMenuTree to get the IDs.";
            }

            if (entry is not null && ContentItemListTree.IsInside(target.Owner, entry.Item))
            {
                return "Nothing was saved: a menu item cannot be moved under itself or under one of its own items.";
            }

            newPosition = ContentItemListTree.Place(entry, item, target, hasPosition ? position : null);
        }

        var (saved, saveError) = await tree.SaveAsync(services, menu, menuDefinition, root, publish);

        if (saveError is not null)
        {
            return saveError;
        }

        var action = "added";

        if (entry is not null)
        {
            action = !moves
                ? "updated"
                : updates
                    ? "updatedAndMoved"
                    : "moved";
        }

        return AgentToolResults.Json(new
        {
            saved = true,
            action,
            menuContentItemId = saved.ContentItemId,
            menuItemId = NestedContentItemTree.GetString(item, nameof(ContentItem.ContentItemId)),
            contentType = NestedContentItemTree.GetString(item, nameof(ContentItem.ContentType)),
            title = NestedContentItemTree.GetString(item, nameof(ContentItem.DisplayText)),
            parentMenuItemId = target.OwnerId,
            position = newPosition,
            published = saved.Published,
            note = tree.GetStateNote(saved),
        });
    }

    /// <summary>
    /// Writes the given fields into the menu item, each into the part the menu item editor writes it to.
    /// </summary>
    private static async Task<(bool Changed, string Error)> ApplyFieldsAsync(
        IServiceProvider services,
        AIFunctionArguments arguments,
        ContentTypeDefinition definition,
        JsonObject item)
    {
        var changed = false;
        var contentType = NestedContentItemTree.GetString(item, nameof(ContentItem.ContentType));
        var urlPartName = MenuContent.HasPart(definition, MenuContent.LinkPart)
            ? MenuContent.LinkPart
            : MenuContent.HasPart(definition, MenuContent.HtmlPart)
                ? MenuContent.HtmlPart
                : null;

        if (arguments.TryGetFirstString("title", out var title))
        {
            item[nameof(ContentItem.DisplayText)] = title.Trim();
            changed = true;
        }

        if (arguments.TryGetFirstString("url", true, out var url))
        {
            if (urlPartName is null)
            {
                return (false, $"a '{contentType}' menu item has no url. Only link and HTML menu items have one.");
            }

            url = url.Trim();

            if (!IsSafeUrl(services, url))
            {
                return (false, $"'{url}' is not a valid URL for a menu item.");
            }

            GetPart(item, urlPartName)["Url"] = url;
            changed = true;
        }

        if (arguments.TryGetFirstString("target", true, out var target))
        {
            if (urlPartName is null)
            {
                return (false, $"a '{contentType}' menu item has no target. Only link and HTML menu items have one.");
            }

            GetPart(item, urlPartName)["Target"] = target.Trim();
            changed = true;
        }

        if (arguments.TryGetFirstString("html", true, out var html))
        {
            if (!MenuContent.HasPart(definition, MenuContent.HtmlPart))
            {
                return (false, $"a '{contentType}' menu item has no html. Only HTML menu items have one.");
            }

            // The editor stores the HTML as typed; the menu sanitizes it when it is shown, by the settings of the part.
            GetPart(item, MenuContent.HtmlPart)["Html"] = html;
            changed = true;
        }

        var isContentItem = MenuContent.HasPart(definition, MenuContent.ContentPart);

        if (arguments.TryGetFirstString("linkedContentItemId", out var linkedContentItemId))
        {
            if (!isContentItem)
            {
                return (false, $"a '{contentType}' menu item does not link to a content item. Only content menu items do.");
            }

            var linked = await services.GetRequiredService<IContentManager>().GetAsync(linkedContentItemId.Trim(), VersionOptions.Latest);

            if (linked is null)
            {
                return (false, $"no content item with the ID '{linkedContentItemId}' was found to link to.");
            }

            var part = GetPart(item, MenuContent.ContentPart);

            if (part[MenuContent.SelectedContentItemField] is not JsonObject field)
            {
                field = [];
                part[MenuContent.SelectedContentItemField] = field;
            }

            field["ContentItemIds"] = new JsonArray(linked.ContentItemId);
            changed = true;
        }

        if (arguments.TryGetFirst<bool>("checkContentPermissions", out var checkContentPermissions))
        {
            if (!isContentItem)
            {
                return (false, $"a '{contentType}' menu item has no checkContentPermissions. Only content menu items have it.");
            }

            GetPart(item, MenuContent.ContentPart)["CheckContentPermissions"] = checkContentPermissions;
            changed = true;
        }

        if (arguments.TryGetFirst<string[]>("permissionNames", out var permissionNames) && permissionNames is not null)
        {
            if (!MenuContent.HasPart(definition, MenuContent.PermissionPart))
            {
                return (false, $"the content type '{contentType}' does not have the menu item permission part, so the item cannot ask for permissions.");
            }

            var (names, unknown) = await FindPermissionsAsync(services, permissionNames);

            if (unknown.Count > 0)
            {
                return (false, $"these are not names of permissions: {string.Join(", ", unknown)}.");
            }

            GetPart(item, MenuContent.PermissionPart)["PermissionNames"] = new JsonArray(names.Select(name => (JsonNode)name).ToArray());
            changed = true;
        }

        return (changed, null);
    }

    private static async Task<(List<string> Names, List<string> Unknown)> FindPermissionsAsync(IServiceProvider services, IEnumerable<string> permissionNames)
    {
        var permissionService = services.GetRequiredService<IPermissionService>();
        var names = new List<string>();
        var unknown = new List<string>();

        foreach (var permissionName in permissionNames.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()))
        {
            var permission = await permissionService.FindByNameAsync(permissionName);

            if (permission is null)
            {
                unknown.Add(permissionName);
            }
            else if (!names.Contains(permission.Name, StringComparer.Ordinal))
            {
                names.Add(permission.Name);
            }
        }

        return (names, unknown);
    }

    private static JsonObject GetPart(JsonObject item, string partName)
    {
        if (item[partName] is not JsonObject part)
        {
            part = [];
            item[partName] = part;
        }

        return part;
    }

    /// <summary>
    /// The check of the menu item editors: the URL is well formed and the HTML sanitizer leaves it as it is in a link, so
    /// a script URL is refused.
    /// </summary>
    private static bool IsSafeUrl(IServiceProvider services, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return true;
        }

        var urlToValidate = url.Split('#', 2)[0];

        if (urlToValidate.StartsWith("~/", StringComparison.Ordinal))
        {
            var pathBase = services.GetService<IHttpContextAccessor>()?.HttpContext?.Request.PathBase ?? PathString.Empty;

            urlToValidate = pathBase.Add(new PathString(urlToValidate.Substring(1))).Value;
        }

        try
        {
            urlToValidate = urlToValidate.ToUriComponents();
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (!Uri.IsWellFormedUriString(urlToValidate, UriKind.RelativeOrAbsolute))
        {
            return false;
        }

        var link = $"<a href=\"{HtmlEncoder.Default.Encode(urlToValidate)}\"></a>";

        return string.Equals(link, services.GetRequiredService<IHtmlSanitizerService>().Sanitize(link), StringComparison.OrdinalIgnoreCase);
    }
}
