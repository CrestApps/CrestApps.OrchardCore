using CrestApps.Core.AI.Models;
using Json.Schema;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "AIDataSource" recipe step — creates or updates AI data source records.
/// </summary>
public sealed class AIDataSourceRecipeStep : IRecipeStep
{
    private readonly AIDataSourceSourceOptions _sourceOptions;
    private JsonSchema _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceRecipeStep"/> class.
    /// </summary>
    /// <param name="sourceOptions">The source options.</param>
    public AIDataSourceRecipeStep(IOptions<AIDataSourceSourceOptions> sourceOptions)
    {
        _sourceOptions = sourceOptions.Value;
    }

    public string Name => "AIDataSource";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private JsonSchema CreateSchema()
    {
        var sourceTypes = GetRegisteredSourceTypes();
        var sourceSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("Optional data source type. When omitted, the import defaults to SearchIndexProfile for backward compatibility.");

        if (sourceTypes.Length > 0)
        {
            sourceSchema = sourceSchema.Enum(sourceTypes);
        }

        var dataSourceSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", RecipeStepSchemaBuilders.String().Description("Optional unique identifier. When supplied and found, the existing data source is updated.")),
                ("DisplayText", RecipeStepSchemaBuilders.String().Description("Human-readable name for the data source.")),
                ("Source", sourceSchema),
                ("SourceIndexProfileName", RecipeStepSchemaBuilders.String().Description("Index profile that provides the raw source documents to ingest.")),
                ("AIKnowledgeBaseIndexProfileName", RecipeStepSchemaBuilders.String().Description("Knowledge-base index profile that stores the normalized AI-ready documents.")),
                ("KeyFieldName", RecipeStepSchemaBuilders.String().Description("Field name that uniquely identifies each source record.")),
                ("TitleFieldName", RecipeStepSchemaBuilders.String().Description("Field name that contains the record title or summary label.")),
                ("ContentFieldName", RecipeStepSchemaBuilders.String().Description("Field name that contains the main textual content used for AI retrieval.")),
                ("CreatedUtc", RecipeStepSchemaBuilders.String().Description("Optional creation timestamp to preserve during import.")),
                ("ModifiedUtc", RecipeStepSchemaBuilders.String().Description("Optional last-modified timestamp to preserve during import.")),
                ("OwnerId", RecipeStepSchemaBuilders.String().Description("Optional owner user identifier.")),
                ("Author", RecipeStepSchemaBuilders.String().Description("Optional author name recorded with the data source.")),
                ("Properties", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Description("Additional provider-specific data source metadata. No built-in metadata objects are currently exported here, but extra keys are allowed for future extensions.")
                    .AdditionalProperties(true)))
            .Required("DisplayText")
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            "AIDataSource",
            [("DataSources", RecipeStepSchemaBuilders.Array(dataSourceSchema, 1).Description("The AI data sources to create or update."))],
            ["DataSources"]);
    }

    private string[] GetRegisteredSourceTypes()
        => _sourceOptions.Sources
            .Select(source => source.SourceType)
            .OrderBy(static sourceType => sourceType, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
