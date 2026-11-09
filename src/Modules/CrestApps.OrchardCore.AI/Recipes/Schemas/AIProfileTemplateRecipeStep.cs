using CrestApps.Core.AI;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Recipes.Schemas;

/// <summary>
/// Schema for the "AIProfileTemplate" recipe step — creates or updates AI profile templates.
/// </summary>
public sealed class AIProfileTemplateRecipeStep : IRecipeStep
{
    private readonly AIOptions _aiOptions;
    private JsonSchema _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateRecipeStep"/> class.
    /// </summary>
    /// <param name="aiOptions">The AI options.</param>
    public AIProfileTemplateRecipeStep(IOptions<AIOptions> aiOptions)
    {
        _aiOptions = aiOptions.Value;
    }

    public string Name => "AIProfileTemplate";

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
        var templateSources = _aiOptions.TemplateSources.Keys
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var templateSourceSchema = CreateTemplateSourceSchema(
            templateSources,
            "Template source identifier. Built-in values are 'Profile' for reusable AI profile templates and 'SystemPrompt' for reusable system prompt templates.");
        var templateSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier. When it matches a stored template, that template is updated; otherwise the template is matched by Name and Source.")),
                ("Source", templateSourceSchema),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique technical name for the template.")),
                ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Human-readable display name. Defaults to Name.")),
                ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Template description.")),
                ("Category", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Grouping category.")),
                ("IsListable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the template appears in the selection dropdown.")),
                ("CreatedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional creation timestamp in ISO 8601 format.")),
                ("OwnerId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional owner identifier.")),
                ("Author", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional author name.")),
                ("Properties", AIProfileRecipeSchemaBuilder.BuildTemplatePropertiesSchema("Extended template properties. A Profile template keeps its profile values (type, deployments, system message, model parameters, tools, agents) under ProfileTemplateMetadata and a SystemPrompt template its text under SystemPromptTemplateMetadata; the step reads nothing else from the template besides the members above. This includes the shared AI profile metadata objects plus template-specific metadata and template-stored settings objects, while still allowing additional feature-specific objects.")))
            .Required("Name", "Source")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AIProfileTemplate").Description("Recipe step discriminator. Must be 'AIProfileTemplate'.")),
                ("Templates", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(templateSchema)
                    .MinItems(1)
                    .Description("The AI profile templates to create or update.")))
            .Required("name", "Templates")
            .AdditionalProperties(true)
            .Build();
    }

    private static JsonSchemaBuilder CreateTemplateSourceSchema(
        IEnumerable<string> templateSources,
        string description)
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description(description);
        var values = templateSources.ToArray();

        if (values.Length > 0)
        {
            schema = schema.WithSuggestions(values);
        }

        return schema;
    }
}
