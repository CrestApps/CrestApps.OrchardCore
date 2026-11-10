using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.AgenticCommerce.Deployments;
using Json.Schema;

namespace CrestApps.OrchardCore.AgenticCommerce.Recipes;

/// <summary>
/// Describes the "AgentPlatforms" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class AgentPlatformsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => AgentPlatformsDeploymentStep.StepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(AgentPlatformsDeploymentStep.StepName).Description("Recipe step discriminator. Must be 'AgentPlatforms'.")),
                ("Platforms", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The platform's 26-character id; omit it to create a new one.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The business whose AI agents buy from the store.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Notes about the platform.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether its agents may buy.")),
                            ("TestMode", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether its agents may only pay with test credentials.")),
                            ("Protocols", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("ucp", "acp")).Description("The protocols its agents use.")),
                            ("OAuthClientId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The client ID of its OAuth client on the store's OpenID server.")),
                            ("ProfileUrl", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The address of its UCP profile, whose keys verify its signed requests.")),
                            ("WebhookUrl", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Where its ACP order updates go.")),
                            ("AllowedPaymentHandlers", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The payment handlers its agents may pay through; empty allows all.")),
                            ("AllowedIpRanges", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The IP addresses and CIDR ranges its requests come from; empty allows any.")),
                            ("AcpOrderCreatedEvent", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The ACP event name a new order sends; order_create when empty.")),
                            ("AcpOrderUpdatedEvent", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The ACP event name a changed order sends; order_update when empty.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The AI agent platforms to create or update. API keys and webhook secrets are never imported; give them new ones in the admin.")))
            .Required("name", "Platforms")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
