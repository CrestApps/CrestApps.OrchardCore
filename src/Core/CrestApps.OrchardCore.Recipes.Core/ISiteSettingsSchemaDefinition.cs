using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Defines a site settings schema fragment that can be contributed to the generic
/// <c>Settings</c> recipe step.
/// </summary>
public interface ISiteSettingsSchemaDefinition
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the schema for the contributed settings property.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetSchemaAsync(CancellationToken cancellationToken = default);
}
