using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Flows;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// The tree of content items a menu or a taxonomy holds. Its lists are at the same place in the JSON whatever the
/// content type of an item is, so they are found by name. <see cref="NestedContentItemTree"/> finds the lists of Flow,
/// Bag and widgets list parts from the content type instead. Both give the same entries, so the tools find, move and
/// save an item the same way.
/// </summary>
internal sealed class ContentItemListTree
{
    /// <summary>
    /// The items of a menu: the list of the menu items list part, on the menu and on each menu item.
    /// </summary>
    public static readonly ContentItemListTree Menu = new("menu", "MenuItemsListPart", "MenuItems", "MenuItemsListPart", "MenuItems");

    /// <summary>
    /// The terms of a taxonomy: the list of the taxonomy part, and the list each term holds itself.
    /// </summary>
    public static readonly ContentItemListTree Taxonomy = new("taxonomy", "TaxonomyPart", "Terms", null, "Terms");

    private const int MaxDepth = 20;

    private readonly string _listName;
    private readonly string _itemPartName;
    private readonly string _itemListName;

    private ContentItemListTree(
        string kind,
        string partName,
        string listName,
        string itemPartName,
        string itemListName)
    {
        Kind = kind;
        PartName = partName;
        _listName = listName;
        _itemPartName = itemPartName;
        _itemListName = itemListName;
    }

    /// <summary>
    /// Gets the kind of tree: menu or taxonomy.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets the name of the part of the top content item that holds the tree.
    /// </summary>
    public string PartName { get; }

    /// <summary>
    /// Lists every item of the tree in outline order: each item is followed by the items under it.
    /// </summary>
    /// <param name="root">The JSON of the top content item.</param>
    /// <returns>The items of the tree.</returns>
    public List<NestedContentItemEntry> GetEntries(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var entries = new List<NestedContentItemEntry>();
        var container = GetTopContainer(root, false);

        if (container is not null)
        {
            AddEntries(container, entries);
        }

        return entries;
    }

    /// <summary>
    /// Gets the list of the top content item.
    /// </summary>
    /// <param name="root">The JSON of the top content item.</param>
    /// <param name="create">Whether the list is added to the JSON when it is missing.</param>
    /// <returns>The list, or <see langword="null"/> when it is missing and is not created.</returns>
    public NestedContentItemContainer GetTopContainer(JsonObject root, bool create)
        => GetContainer(root, null, PartName, _listName, 1, create);

    /// <summary>
    /// Gets the list of the items under one item of the tree.
    /// </summary>
    /// <param name="parent">The item that owns the list.</param>
    /// <param name="create">Whether the list is added to the JSON when it is missing.</param>
    /// <returns>The list, or <see langword="null"/> when it is missing and is not created.</returns>
    public NestedContentItemContainer GetChildContainer(NestedContentItemEntry parent, bool create)
    {
        ArgumentNullException.ThrowIfNull(parent);

        return GetContainer(
            parent.Item,
            NestedContentItemTree.GetString(parent.Item, nameof(ContentItem.ContentItemId)),
            _itemPartName,
            _itemListName,
            parent.Container.Depth + 1,
            create);
    }

    /// <summary>
    /// Finds the list an item is added or moved to.
    /// </summary>
    /// <param name="root">The JSON of the top content item.</param>
    /// <param name="entries">The items of the tree.</param>
    /// <param name="parentId">The ID of the item that takes the item, or the ID of the top content item (or nothing) for the top list.</param>
    /// <returns>The list, or <see langword="null"/> when no item of the tree has the ID.</returns>
    public NestedContentItemContainer FindTarget(JsonObject root, IEnumerable<NestedContentItemEntry> entries, string parentId)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (string.IsNullOrEmpty(parentId) ||
            string.Equals(parentId, NestedContentItemTree.GetString(root, nameof(ContentItem.ContentItemId)), StringComparison.OrdinalIgnoreCase))
        {
            return GetTopContainer(root, true);
        }

        var parent = NestedContentItemTree.Find(entries, parentId);

        return parent is null
            ? null
            : GetChildContainer(parent, true);
    }

    /// <summary>
    /// Puts an item at a position of a list. An item that is already in the tree is taken out of its list first.
    /// </summary>
    /// <param name="entry">Where the item is now, or <see langword="null"/> for a new item.</param>
    /// <param name="item">The JSON of the item.</param>
    /// <param name="target">The list that takes the item.</param>
    /// <param name="position">The position in the list, or <see langword="null"/> to keep the position in the same list and to add at the end of another list.</param>
    /// <returns>The position the item got.</returns>
    public static int Place(NestedContentItemEntry entry, JsonObject item, NestedContentItemContainer target, int? position)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(target);

        var sameList = entry is not null && ReferenceEquals(target.Items, entry.Container.Items);

        entry?.Container.Items.RemoveAt(entry.Position);

        var newPosition = target.Items.Count;

        if (position.HasValue)
        {
            newPosition = Math.Min(position.Value, target.Items.Count);
        }
        else if (sameList)
        {
            newPosition = Math.Min(entry.Position, target.Items.Count);
        }

        target.Items.Insert(newPosition, item);

        return newPosition;
    }

    /// <summary>
    /// Finds the item of a list that another item is to be placed after.
    /// </summary>
    /// <param name="target">The list.</param>
    /// <param name="afterId">The ID of the item to place after.</param>
    /// <returns>The item, or <see langword="null"/> when the list has no item with the ID.</returns>
    public static JsonObject FindSibling(NestedContentItemContainer target, string afterId)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.Items
            .OfType<JsonObject>()
            .FirstOrDefault(sibling => string.Equals(
                NestedContentItemTree.GetString(sibling, nameof(ContentItem.ContentItemId)),
                afterId,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Puts an item first in a list or right after one of its items. An item that is already in the tree is taken out of
    /// its list first. With neither, an item keeps its position in the same list and goes to the end of another list.
    /// </summary>
    /// <param name="entry">Where the item is now, or <see langword="null"/> for a new item.</param>
    /// <param name="item">The JSON of the item.</param>
    /// <param name="target">The list that takes the item.</param>
    /// <param name="first">Whether the item goes first in the list.</param>
    /// <param name="sibling">The item of the list the item goes after, from <see cref="FindSibling"/>, or <see langword="null"/>.</param>
    /// <returns>The position the item got.</returns>
    public static int PlaceAfter(NestedContentItemEntry entry, JsonObject item, NestedContentItemContainer target, bool first, JsonObject sibling)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(target);

        if (!first && sibling is null)
        {
            return Place(entry, item, target, null);
        }

        entry?.Container.Items.RemoveAt(entry.Position);

        // The sibling is found by reference after the item is taken out, so its index is the one of the changed list.
        var newPosition = first
            ? 0
            : target.Items.IndexOf(sibling) + 1;

        target.Items.Insert(newPosition, item);

        return newPosition;
    }

    /// <summary>
    /// Saves the changed tree into the content item, validates it, and saves the draft or publishes it. As the menu and
    /// taxonomy editors do, a content type that can have a draft takes the change in a draft; any other takes it in its
    /// latest version, where the change is live at once when that version is published.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="contentItem">The top content item.</param>
    /// <param name="definition">The content type of the top content item.</param>
    /// <param name="root">The working JSON of the top content item, with the changes.</param>
    /// <param name="publish">Whether the content item is published after the change.</param>
    /// <returns>The saved content item, or the error that stopped it.</returns>
    public Task<(ContentItem ContentItem, string Error)> SaveAsync(
        IServiceProvider services,
        ContentItem contentItem,
        ContentTypeDefinition definition,
        JsonObject root,
        bool publish)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        var versionOptions = definition is not null && definition.IsDraftable()
            ? VersionOptions.DraftRequired
            : VersionOptions.Latest;

        return NestedContentItemTree.SaveAsync(services, contentItem.ContentItemId, root, [PartName], !publish, versionOptions);
    }

    /// <summary>
    /// Says whether a saved change is live.
    /// </summary>
    /// <param name="saved">The saved content item.</param>
    /// <returns>The sentence for the result of a tool.</returns>
    public string GetStateNote(ContentItem saved)
    {
        ArgumentNullException.ThrowIfNull(saved);

        return saved.Published
            ? $"The {Kind} is published, so the change is live."
            : $"The change is in a draft of the {Kind}. The site shows the published version until the {Kind} content item is published.";
    }

    /// <summary>
    /// Counts the items under an item, at any depth. They follow it in the outline at a greater depth.
    /// </summary>
    /// <param name="entries">The items of the tree, in outline order.</param>
    /// <param name="entry">The item.</param>
    /// <returns>The number of items under the item.</returns>
    public static int CountDescendants(IEnumerable<NestedContentItemEntry> entries, NestedContentItemEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(entry);

        return entries
            .SkipWhile(candidate => !ReferenceEquals(candidate, entry))
            .Skip(1)
            .TakeWhile(candidate => candidate.Container.Depth > entry.Container.Depth)
            .Count();
    }

    /// <summary>
    /// Whether a JSON node is another node or is inside it.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="ancestor">The node that may hold it.</param>
    /// <returns><see langword="true"/> when the node is the other node or is inside it.</returns>
    public static bool IsInside(JsonNode node, JsonNode ancestor)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private NestedContentItemContainer GetContainer(
        JsonObject owner,
        string ownerId,
        string partName,
        string listName,
        int depth,
        bool create)
    {
        var holder = owner;

        if (partName is not null)
        {
            holder = owner[partName] as JsonObject;

            if (holder is null)
            {
                if (!create)
                {
                    return null;
                }

                holder = [];
                owner[partName] = holder;
            }
        }

        if (holder[listName] is not JsonArray items)
        {
            if (!create)
            {
                return null;
            }

            items = [];
            holder[listName] = items;
        }

        return new NestedContentItemContainer
        {
            Kind = Kind,
            PartName = PartName,
            Items = items,
            Owner = owner,
            OwnerId = ownerId,
            TopLevelPartName = PartName,
            Depth = depth,
        };
    }

    private void AddEntries(NestedContentItemContainer container, List<NestedContentItemEntry> entries)
    {
        for (var position = 0; position < container.Items.Count; position++)
        {
            if (container.Items[position] is not JsonObject item)
            {
                continue;
            }

            var entry = new NestedContentItemEntry
            {
                Container = container,
                Position = position,
                Item = item,
            };

            entries.Add(entry);

            if (container.Depth >= MaxDepth)
            {
                continue;
            }

            var children = GetChildContainer(entry, false);

            if (children is not null)
            {
                AddEntries(children, entries);
            }
        }
    }
}
