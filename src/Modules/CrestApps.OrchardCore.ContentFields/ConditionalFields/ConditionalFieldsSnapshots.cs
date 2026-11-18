using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.ContentFields.ConditionalFields;

/// <summary>
/// What each item held before its editor was read in this request (its stored values on an edit, a new item's on a
/// create), so a hidden field gets back what it had instead of what was posted. Scoped to the request.
/// </summary>
internal sealed class ConditionalFieldsSnapshots
{
    private readonly Dictionary<ContentItem, JsonObject> _snapshots = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Keeps a copy of an item's content before the field editors write to it.
    /// </summary>
    /// <param name="contentItem">The item.</param>
    public void Take(ContentItem contentItem)
        => _snapshots.TryAdd(contentItem, (JsonObject)((JsonObject)contentItem.Content).DeepClone());

    /// <summary>
    /// Gets the copy taken of an item, if any.
    /// </summary>
    /// <param name="contentItem">The item.</param>
    /// <returns>The copy, or <see langword="null"/>.</returns>
    public JsonObject Get(ContentItem contentItem)
        => _snapshots.GetValueOrDefault(contentItem);
}
