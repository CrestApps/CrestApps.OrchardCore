using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "AIDeployment" recipe step — creates or updates AI model deployments.
/// </summary>
public sealed class AIDeploymentRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "AIDeployment";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var deploymentSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Deployment name as specified by the vendor.")),
                ("ProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Provider name (e.g., OpenAI, DeepSeek).")),
                ("ConnectionName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Connection name used to configure the provider.")),
                ("Type", new JsonSchemaBuilder().AnyOf(
                    new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The deployment type, or a comma-separated flag value such as 'Chat, Utility'. Defaults to Chat when not specified."),
                    new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(
                        new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Chat", "Utility", "Embedding", "Image", "SpeechToText", "TextToSpeech")).MinItems(1).UniqueItems(true).Description("The deployment types."))),
                ("IsDefault", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether this deployment is the default for its type and connection.")))
            .Required("Name")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AIDeployment")),
                ("Deployments", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(deploymentSchema)
                    .MinItems(1)
                    .Description("The AI deployments to create or update.")))
            .Required("name", "Deployments")
            .AdditionalProperties(true)
            .Build();
    }
}
