namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// Describes what the structured data is built for.
/// </summary>
public enum StructuredDataPurpose
{
    /// <summary>
    /// The JSON-LD script of a page the site renders. It includes the site-wide nodes, such as the organization.
    /// </summary>
    Page,

    /// <summary>
    /// One schema.org object returned to an agent, such as an NLWeb answer. It describes only the content item.
    /// </summary>
    Answer,
}
