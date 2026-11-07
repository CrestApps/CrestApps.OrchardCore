using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Describes a recipe step that carries records in one array, each matched by its <c>ItemId</c> so a second run updates
/// them instead of adding them again, such as the steps built on the catalog store recipe step.
/// </summary>
public abstract class RecordsRecipeStepSchemaBase : RecipeStepSchemaBase
{
    /// <summary>
    /// Gets the name of the step member that holds the array of records.
    /// </summary>
    protected abstract string EntriesName { get; }

    /// <summary>
    /// Gets the description of the step.
    /// </summary>
    protected abstract string Description { get; }

    /// <summary>
    /// Gets the description of one record.
    /// </summary>
    protected abstract string EntryDescription { get; }

    /// <summary>
    /// Gets the members of a record that must be present.
    /// </summary>
    protected virtual IEnumerable<string> RequiredEntryProperties => [];

    /// <summary>
    /// Describes the main members of a record. A record may carry more members.
    /// </summary>
    /// <returns>The members and their schemas.</returns>
    protected virtual IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        => [];

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
    {
        (string, JsonSchemaBuilder)[] properties =
        [
            ("ItemId", CatalogRecipeStepSchemas.ItemId("record")),
            .. GetEntryProperties(),
        ];

        var entry = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description(EntryDescription)
            .Properties(properties)
            .AdditionalProperties(true);

        var required = RequiredEntryProperties.ToArray();

        if (required.Length > 0)
        {
            entry.Required(required);
        }

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description(Description)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(Name).Description($"Recipe step discriminator. Must be '{Name}'.")),
                (EntriesName, new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(entry).Description($"The records to create, or to update when their ItemId matches a stored record.")))
            .Required("name", EntriesName)
            .AdditionalProperties(true)
            .Build();
    }
}
