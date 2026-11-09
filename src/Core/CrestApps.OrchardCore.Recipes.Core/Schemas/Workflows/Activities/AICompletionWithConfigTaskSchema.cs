using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AICompletionWithConfigTask</c> workflow task.
/// </summary>
public sealed class AICompletionWithConfigTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AICompletionWithConfigTask";

    /// <inheritdoc />
    protected override string Category => "Artificial Intelligence";

    /// <inheritdoc />
    protected override string DisplayText => "AI Completion using Direct Config";

    /// <inheritdoc />
    protected override string Description => "Performs AI completion using explicit deployment and configuration parameters";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Drew Blank", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["PromptTemplate", "ResultPropertyName"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Interaction", BuildInteractionSchema());
        yield return ("PromptTemplate", WorkflowActivitySchemaBuilders.String("The template used to generate the prompt for the AI model. Supports Liquid syntax."));
        yield return ("ResultPropertyName", WorkflowActivitySchemaBuilders.String("The workflow output property name where the AI response will be stored. Prefix with 'AI-' to avoid conflicts with other workflow properties."));
    }

    private static JsonSchemaBuilder BuildInteractionSchema()
    {
        static JsonSchemaBuilder Strings(string description)
            => new JsonSchemaBuilder()
                .Type(SchemaValueType.Array | SchemaValueType.Null)
                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                .Description(description);

        static JsonSchemaBuilder Nullable(SchemaValueType type, string description)
            => new JsonSchemaBuilder()
                .Type(type | SchemaValueType.Null)
                .Description(description);

        var properties = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object | SchemaValueType.Null)
            .Properties(
                ("AIToolInstanceMetadata", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(("ToolInstanceNames", Strings("The names of the AI tool instances the task may call.")))
                    .AdditionalProperties(true)
                    .Description("The AI tool instances the task may call.")),
                ("PromptTemplateMetadata", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)
                    .Description("The prompt templates applied with the system message, as Templates: [{ TemplateId, Parameters }].")),
                ("AIPersonaMetadata", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)
                    .Description("The AI persona the task speaks as, as { PersonaId, Parameters }.")))
            .AdditionalProperties(true)
            .Description("Extension data of the configuration, keyed by the name of the metadata object. AI Data Sources adds DataSourceMetadata ({ DataSourceId }) and AIDataSourceRagMetadata (Strictness, TopNDocuments, IsInScope, Filter).");

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ChatDeploymentName", Nullable(SchemaValueType.String, "Technical name of the chat deployment that runs the task. Empty uses the site's default chat deployment.")),
                ("UtilityDeploymentName", Nullable(SchemaValueType.String, "Technical name of the utility deployment used for background steps. Empty uses the site's default.")),
                ("OrchestratorName", Nullable(SchemaValueType.String, "Name of the orchestrator that runs the completion. Empty uses the default orchestrator.")),
                ("SystemMessage", Nullable(SchemaValueType.String, "The system instruction that sets the AI's behavior and response style.")),
                ("Temperature", Nullable(SchemaValueType.Number, "Controls randomness in responses. Lower values produce more deterministic output.")),
                ("TopP", Nullable(SchemaValueType.Number, "Controls randomness via nucleus sampling.")),
                ("FrequencyPenalty", Nullable(SchemaValueType.Number, "Reduces the chance of repeating tokens in proportion to how often they appeared.")),
                ("PresencePenalty", Nullable(SchemaValueType.Number, "Reduces the chance of repeating any token that appeared, encouraging new topics.")),
                ("MaxTokens", Nullable(SchemaValueType.Integer, "The maximum number of tokens in the response.")),
                ("ToolNames", Strings("The names of the AI tools to enable for this task.")),
                ("Properties", properties))
            .AdditionalProperties(true)
            .Description("The completion's configuration, stored as a chat interaction: the deployment, model parameters, tools and other settings.");
    }
}
