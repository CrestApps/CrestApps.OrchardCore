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
                                .Description("Skill identifiers granted to the agent.")),
                            ("SkillProficiencies", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("SkillId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The skill identifier.")),
                                        ("Proficiency", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Maximum(5).Description("How well the agent holds the skill, from 1 to 5.")))
                                    .AdditionalProperties(true))
                                .Description("How well the agent holds each skill. Skills listed only in 'Skills' are read at the default proficiency.")),
                            ("QueueMemberships", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("QueueId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The queue the membership is for.")),
                                        ("Priority", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How strongly the agent is pulled toward this queue; lower is served first.")),
                                        ("DelaySeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How long an item must have waited before this agent is offered it, which is how a backup (overflow) membership is expressed.")))
                                    .AdditionalProperties(true))
                                .Description("How the agent serves each queue: its priority and the wait before the agent is offered work from it.")))
                        .Required("UserName")
                        .AdditionalProperties(true))
                    .Description("The Contact Center agent entitlements to create or update.")))
            .Required("name", "Agents")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
