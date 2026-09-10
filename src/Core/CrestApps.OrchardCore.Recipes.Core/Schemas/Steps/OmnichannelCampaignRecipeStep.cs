using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "OmnichannelCampaign" recipe step — imports omnichannel campaigns and their automation settings.
/// </summary>
public sealed class OmnichannelCampaignRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "OmnichannelCampaign";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("OmnichannelCampaign").Description("Recipe step discriminator. Must be 'OmnichannelCampaign'.")),
                ("Campaigns", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Stable identifier of the campaign. When it matches an existing campaign the entry is updated; otherwise a new campaign is created.")),
                            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Human-readable name of the campaign.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Administrative description of the campaign.")),
                            ("CampaignGroupId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the campaign group this campaign is reported under.")),
                            ("InteractionType", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Enum("Manual", "Automated")
                                .Description("Whether the campaign is handled manually by an agent or automated by AI.")),
                            ("Channel", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Communication channel used by the campaign, for example 'SMS', 'Chat', or 'Email'.")),
                            ("ChannelEndpointId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the channel endpoint used to reach out to the contact.")),
                            ("InitialOutboundPromptPattern", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("For automated campaigns, the initial message used to start the conversation with the customer.")),
                            ("CampaignGoal", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Description of what success looks like, used by the AI to decide when the conversation can end.")),
                            ("ProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Name of the AI provider used for automation.")),
                            ("ConnectionName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Name of the provider connection used for automation.")),
                            ("DeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Name of the model deployment used for automation.")),
                            ("SystemMessage", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("System message that steers the automated conversation.")),
                            ("Temperature", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("Sampling temperature applied to the automated completions.")),
                            ("TopP", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("Nucleus sampling probability applied to the automated completions.")),
                            ("FrequencyPenalty", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("Frequency penalty applied to the automated completions.")),
                            ("PresencePenalty", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("Presence penalty applied to the automated completions.")),
                            ("MaxTokens", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Description("Maximum number of tokens the automated completions may generate.")),
                            ("ToolNames", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array | SchemaValueType.Null)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                                .Description("Names of the tools the automation is allowed to invoke.")),
                            ("AllowAIToUpdateContact", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the AI is allowed to update the contact during an automated conversation.")),
                            ("AllowAIToUpdateSubject", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the AI is allowed to update the subject during an automated conversation.")))
                        .AdditionalProperties(true))
                    .Description("Campaigns to create or update.")))
            .Required("name", "Campaigns")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
