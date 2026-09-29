using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Builds the schema fragments every catalog-backed configuration recipe step shares, so the identifier and extension
/// data of an entry are described the same way wherever an entry travels between environments.
/// </summary>
internal static class CatalogRecipeStepSchemas
{
    /// <summary>
    /// Describes the identifier an imported entry is matched and stored by.
    /// </summary>
    /// <param name="noun">The singular name of the entry, used in the description.</param>
    public static JsonSchemaBuilder ItemId(string noun)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description($"Optional unique identifier of the {noun}. When it matches a stored {noun}, that {noun} is updated. Otherwise a new {noun} is created under this identifier, so references to it from other entries and steps keep resolving.");

    /// <summary>
    /// Describes the extension data other features attach to an entry.
    /// </summary>
    /// <param name="noun">The singular name of the entry, used in the description.</param>
    public static JsonSchemaBuilder Properties(string noun)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object | SchemaValueType.Null)
            .AdditionalProperties(true)
            .Description($"Extension data other features attach to the {noun}, keyed by the name of the settings object (for example channel-specific routing settings). When present, it replaces the stored extension data as a whole.");
}
