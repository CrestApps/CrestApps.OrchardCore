namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// The options of the structured data engine.
/// </summary>
public sealed class StructuredDataOptions
{
    /// <summary>
    /// Gets the names of the content parts whose content items are described on their pages without the Schema.org
    /// part, because a module describes them, such as <c>ProductPart</c>. Other content items get structured data only
    /// when their content type has the Schema.org part.
    /// </summary>
    public ISet<string> SelfDescribingParts { get; } = new HashSet<string>(StringComparer.Ordinal);
}
