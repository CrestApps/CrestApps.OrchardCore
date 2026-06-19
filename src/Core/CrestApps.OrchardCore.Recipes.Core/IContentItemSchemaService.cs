using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Builds JSON schemas that describe Orchard Core content item payloads.
/// </summary>
public interface IContentItemSchemaService
{
    /// <summary>
    /// Builds a generic content item schema with optional content type values.
    /// </summary>
    /// <param name="contentTypes">Optional list of available content type names to enumerate.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetGenericSchemaAsync(IEnumerable<string> contentTypes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a content item schema for all known content types.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a content item schema for a specific content type.
    /// </summary>
    /// <param name="contentType">The Orchard Core content type name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetSchemaAsync(string contentType, CancellationToken cancellationToken = default);
}
