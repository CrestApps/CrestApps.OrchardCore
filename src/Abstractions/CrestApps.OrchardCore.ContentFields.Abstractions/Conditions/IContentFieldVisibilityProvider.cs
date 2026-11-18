using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentFields.Conditions;

/// <summary>
/// Hides fields of an editor for a reason of its own, besides their rules, such as the fields customers don't fill in
/// on a customer's form. A hidden field is treated as one whose rule isn't met: whatever was posted for it is replaced
/// by what the item held before the post, and its validation errors are removed.
/// </summary>
public interface IContentFieldVisibilityProvider
{
    /// <summary>
    /// Gets the fields it hides on an item in the current request.
    /// </summary>
    /// <param name="contentItem">The item.</param>
    /// <param name="type">Its content type.</param>
    /// <returns>The fields, as <c>{TypePartName}.{FieldName}</c>.</returns>
    Task<IEnumerable<string>> GetHiddenFieldsAsync(ContentItem contentItem, ContentTypeDefinition type);
}
