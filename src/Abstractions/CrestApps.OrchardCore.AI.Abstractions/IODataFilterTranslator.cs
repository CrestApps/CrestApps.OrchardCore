namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Translates OData filter expressions into provider-specific filter queries
/// for use against the knowledge base index's filter fields.
/// Implementations are registered as keyed services using the provider name.
/// </summary>
public interface IODataFilterTranslator
{
    /// <summary>
    /// Translates an OData filter expression into a provider-specific filter string.
    /// The translated filter targets the "filters." prefixed fields in the knowledge base index.
    /// </summary>
    /// <param name="odataFilter">The OData filter expression (e.g., "status eq 'active' and category eq 'docs'").</param>
    /// <returns>A provider-specific filter string, or null if the filter could not be translated.</returns>
    string Translate(string odataFilter);
}
