using CrestApps.Core.AI;
using Json.Schema;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

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
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier.")),
                ("Source", templateSourceSchema),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique technical name for the template.")),
                ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Human-readable display name.")),
                ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Template description.")),
                ("Category", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Grouping category.")),
                ("IsListable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the template appears in the selection dropdown.")),
                ("ProfileType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Chat", "Utility", "TemplatePrompt", "Agent").Description("The profile type.")),
                ("OrchestratorName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Orchestrator name.")),
                ("SystemMessage", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("System prompt for the AI.")),
                ("WelcomeMessage", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Initial greeting.")),
                ("TitleType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("InitialPrompt", "Generated").Description("Session title type.")),
                ("PromptTemplate", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Liquid prompt template.")),
                ("PromptSubject", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Subject for prompt.")),
                ("Temperature", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Controls randomness (0.0 to 2.0).")),
                ("TopP", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Nucleus sampling threshold.")),
                ("FrequencyPenalty", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Reduces frequent token repetition.")),
                ("PresencePenalty", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Encourages topic diversity.")),
                ("MaxOutputTokens", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Maximum tokens in response.")),
                ("PastMessagesCount", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Number of history messages to include.")),
                ("ToolNames", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("AI tool names.")),
                ("AgentNames", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("Agent profile names to include.")),
                ("ProfileDescription", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Description of the profile's capabilities (used for Agent type).")),
                ("Properties", AIProfileRecipeSchemaBuilder.BuildTemplatePropertiesSchema("Extended template properties. This includes the shared AI profile metadata objects plus template-specific metadata and template-stored settings objects, while still allowing additional feature-specific objects.")))
            .Required("Name", "Source", "DisplayText")
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
            schema = schema.Enum(values);
        }

        return schema;
    }
}
