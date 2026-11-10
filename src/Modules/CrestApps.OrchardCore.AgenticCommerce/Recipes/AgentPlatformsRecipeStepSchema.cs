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
                            ("EmbeddedCheckoutOrigins", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The https origins whose pages may show the store's checkout in a frame (UCP embedded checkout).")),
                            ("AcpOrderCreatedEvent", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The ACP event name a new order sends; order_create when empty.")),
                            ("AcpOrderUpdatedEvent", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The ACP event name a changed order sends; order_update when empty.")),
                            ("AcpRequireSignedRequests", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether its ACP requests must carry a valid Signature and Timestamp.")),
                            ("AcpSignatureAlgorithm", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The algorithm its ACP requests are signed with; ES256 when empty.")),
                            ("AcpSignaturePublicKey", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The P-256 public key that verifies its signed ACP requests: an EC JSON Web Key or a PEM PUBLIC KEY block.")),
                            ("AcpSignatureClockSkewSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Minimum(1).Maximum(3600).Description("How many seconds a signed request's Timestamp may be from the store's clock; 300 when empty.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The AI agent platforms to create or update. API keys and webhook secrets are never imported; give them new ones in the admin.")))
            .Required("name", "Platforms")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
