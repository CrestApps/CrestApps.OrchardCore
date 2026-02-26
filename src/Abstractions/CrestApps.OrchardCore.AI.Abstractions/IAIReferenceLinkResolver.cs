namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Resolves links for AI completion references based on the reference type.
/// Implementations are registered as keyed services using the reference type as the key,
/// allowing different strategies for different source types (e.g., content items, documents, custom).
/// </summary>
public interface IAIReferenceLinkResolver
{
    /// <summary>
    /// Resolves a link URL for the given reference.
    /// </summary>
    /// <param name="referenceId">The unique identifier of the referenced resource.</param>
    /// <param name="metadata">Optional metadata associated with the reference.</param>
    /// <returns>The resolved link URL, or <c>null</c> if no link can be generated.</returns>
    string ResolveLink(string referenceId, IDictionary<string, object> metadata);
}
