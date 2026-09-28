using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ContactCenterAgentEntitlement" recipe step — imports manager-owned agent entitlements matched to Orchard users by user name.
/// </summary>
public sealed class ContactCenterAgentEntitlementRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "ContactCenterAgentEntitlement";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ContactCenterAgentEntitlement").Description("Recipe step discriminator. Must be 'ContactCenterAgentEntitlement'.")),
                ("Agents", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("UserName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("User name of the target Orchard user the entitlement is applied to. Entries without a user name are skipped.")),
                            ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Display name for the agent. When empty, the resolved user name is used.")),
                            ("MaxConcurrentInteractions", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Maximum number of interactions the agent may handle at once. Values below 1 are raised to 1.")),
                            ("AllowedQueueIds", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A queue identifier the agent is entitled to. Identifiers that no longer exist are filtered out."))
                                .Description("Queue identifiers the agent is entitled to work.")),
                            ("AllowedCampaignIds", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A campaign identifier the agent is entitled to. Identifiers that no longer exist are filtered out."))
                                .Description("Campaign identifiers the agent is entitled to work.")),
                            ("Skills", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A skill identifier granted to the agent."))
                                .Description("Skill identifiers granted to the agent.")))
                        .Required("UserName")
                        .AdditionalProperties(true))
                    .Description("The Contact Center agent entitlements to create or update.")))
            .Required("name", "Agents")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
