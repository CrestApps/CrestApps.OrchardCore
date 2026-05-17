using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for default orchestrator settings.
/// </summary>
public sealed class DefaultOrchestratorSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "DefaultOrchestratorSettings";

    /// <summary>
    /// Builds the schema for default orchestrator settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Default configuration for the AI chat orchestrator.")
            .Properties(
                ("EnablePreemptiveRag", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to enable preemptive retrieval-augmented generation (RAG) in chat sessions.")))
            .AdditionalProperties(false);
}
