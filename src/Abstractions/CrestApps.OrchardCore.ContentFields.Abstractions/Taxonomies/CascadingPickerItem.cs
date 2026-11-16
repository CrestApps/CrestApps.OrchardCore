namespace CrestApps.OrchardCore.ContentFields.Taxonomies;

/// <summary>
/// One choice of the <c>TaxonomyCascadingPicker</c> shape: a term with its parent, offered one level at a time
/// (Category, then Sub-category...). Give the items in tree order.
/// </summary>
/// <param name="Id">The value posted, such as the term's content item id.</param>
/// <param name="Text">What it reads as.</param>
/// <param name="ParentId">The item above it, or <see langword="null"/> at the top.</param>
/// <param name="Selectable">Whether it can be the answer itself (not when only the most specific item may be chosen and it has children).</param>
/// <param name="Description">Help shown under the picker once it is chosen, if any.</param>
public sealed record CascadingPickerItem(string Id, string Text, string ParentId, bool Selectable = true, string Description = null);
