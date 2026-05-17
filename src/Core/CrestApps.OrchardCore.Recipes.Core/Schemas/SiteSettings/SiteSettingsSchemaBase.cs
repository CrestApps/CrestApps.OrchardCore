using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Base class for site settings schema definitions that caches the built schema.
/// </summary>
public abstract class SiteSettingsSchemaBase : ISiteSettingsSchemaDefinition
{
    private JsonSchemaBuilder _cache;

    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Builds and caches the schema for the contributed settings property.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public ValueTask<JsonSchemaBuilder> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cache ??= BuildSchemaCore();

        return ValueTask.FromResult(_cache);
    }

    /// <summary>
    /// Builds the schema for this site settings definition.
    /// Override to provide a detailed schema with property definitions.
    /// </summary>
    protected virtual JsonSchemaBuilder BuildSchemaCore()
        => OpenObject();

    /// <summary>
    /// Creates an open object schema that allows additional properties.
    /// </summary>
    protected static JsonSchemaBuilder OpenObject()
        => RecipeStepSchemaBuilders.Object(additionalProperties: true);
}
