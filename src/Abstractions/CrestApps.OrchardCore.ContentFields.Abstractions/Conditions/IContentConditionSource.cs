using CrestApps.OrchardCore.Conditions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentFields.Conditions;

/// <summary>
/// A value of a content item that field rules can read besides its fields, such as a ticket's category. Register it with
/// <c>services.AddScoped&lt;IContentConditionSource, MySource&gt;()</c>.
/// </summary>
public interface IContentConditionSource
{
    /// <summary>
    /// Describes the sources it offers on a content type.
    /// </summary>
    /// <param name="type">The content type.</param>
    /// <returns>The sources; none when it doesn't apply to the type. Their names have no dot.</returns>
    Task<IEnumerable<ContentConditionSourceDescriptor>> DescribeAsync(ContentTypeDefinition type);

    /// <summary>
    /// Reads one of its sources on an item.
    /// </summary>
    /// <param name="name">The source's name.</param>
    /// <param name="contentItem">The item, as the editor has it.</param>
    /// <returns>The value, or <see langword="null"/> when the source isn't one of its own.</returns>
    Task<ConditionValue> ReadAsync(string name, ContentItem contentItem);
}
