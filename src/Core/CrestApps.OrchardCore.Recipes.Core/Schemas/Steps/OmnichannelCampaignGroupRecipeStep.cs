using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "OmnichannelCampaignGroup" recipe step — imports reporting groups that aggregate omnichannel campaigns.
/// </summary>
public sealed class OmnichannelCampaignGroupRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "OmnichannelCampaignGroup";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("OmnichannelCampaignGroup").Description("Recipe step discriminator. Must be 'OmnichannelCampaignGroup'.")),
                ("CampaignGroups", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Stable identifier of the campaign group. When it matches an existing group the entry is updated; otherwise a new group is created.")),
                            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Human-readable name of the campaign group.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Administrative description of the campaign group.")))
                        .AdditionalProperties(true))
                    .Description("Campaign groups to create or update.")))
            .Required("name", "CampaignGroups")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
