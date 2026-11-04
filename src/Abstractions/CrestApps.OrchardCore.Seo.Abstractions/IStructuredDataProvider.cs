namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// Adds schema.org data to a page or to a content item. Modules implement it to describe the parts and content types
/// they own, such as a product's offers or an event's dates.
/// </summary>
/// <remarks>
/// Providers run in ascending <see cref="Order"/>. A provider adds properties to
/// <see cref="StructuredDataContext.MainEntity"/> or nodes to <see cref="StructuredDataContext.Graph"/>, and should
/// not remove what other providers wrote. Providers that run later win when two set the same property, so the content
/// type mapping and the editor's own JSON-LD, which run last, can override the defaults.
/// </remarks>
public interface IStructuredDataProvider
{
    /// <summary>
    /// Gets the order the provider runs in. Lower values run first.
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Adds the provider's data to the context.
    /// </summary>
    /// <param name="context">The structured data context.</param>
    /// <returns>A task that completes when the data is added.</returns>
    Task BuildAsync(StructuredDataContext context);
}
