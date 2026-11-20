using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentFields.Conditions;

/// <summary>
/// Tells, for each field of an item, whether it applies (its show-when rule, and the visibility providers) and whether
/// it holds a value, so a page that lists an item's fields can say "Not set" for the empty ones that apply. Registered
/// with the Conditional Fields feature.
/// </summary>
public interface IContentFieldStateService
{
    /// <summary>
    /// Gets the state of each field of an item, in the type's order.
    /// </summary>
    /// <param name="contentItem">The item.</param>
    /// <param name="type">Its type.</param>
    /// <returns>The fields' states.</returns>
    Task<IReadOnlyList<ContentFieldState>> GetStatesAsync(ContentItem contentItem, ContentTypeDefinition type);
}

/// <summary>
/// A field of an item: whether it applies and whether it holds a value.
/// </summary>
/// <param name="Key">The field, keyed <c>{TypePartName}.{FieldName}</c>.</param>
/// <param name="PartName">The type part's name.</param>
/// <param name="FieldName">The field's name.</param>
/// <param name="DisplayName">The field's display name.</param>
/// <param name="IsHidden">Whether the field doesn't apply to the item (a rule or a provider hides it).</param>
/// <param name="IsEmpty">Whether the field holds no value; <see langword="false"/> also when no reader knows its type.</param>
public sealed record ContentFieldState(string Key, string PartName, string FieldName, string DisplayName, bool IsHidden, bool IsEmpty);
