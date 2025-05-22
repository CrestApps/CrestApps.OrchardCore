
namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Provides a method to generate a path for a content item based on its identifier and metadata.
/// </summary>
public interface IAILinkGenerator
{
    /// <summary>
    /// Gets the path for the specified content item using its identifier and associated metadata.
    /// </summary>
    /// <param name="contentItemId">The unique identifier of the content item.</param>
    /// <param name="metadata">A dictionary containing metadata related to the content item.</param>
    /// <returns>The generated path for the content item.</returns>
    string GetContentItemPath(string contentItemId, IDictionary<string, object> metadata);
}
