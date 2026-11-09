using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Schemas;

/// <summary>
/// Describes the <c>OpportunityPart</c> settings and content to CrestApps Recipes.
/// </summary>
internal sealed class OpportunityPartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => OmnichannelConstants.ContentParts.Opportunity;

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (nameof(OpportunityPartSettings), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Description("The pipeline of an opportunity type.")
                    .Properties(
                        (nameof(OpportunityPartSettings.StageIds), new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                            .Description("The ItemIds of the stages (see the OmnichannelOpportunityStage step) an opportunity of this type moves through. Empty uses every stage.")))
                    .AdditionalProperties(true)))
            .AdditionalProperties(true);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("An opportunity. Amount, CloseDate, Owner, Source and PrimaryContact are content fields of the part, described by their field schemas.")
            .Properties(
                (nameof(OpportunityPart.StageId), NullableString("The ItemId of the opportunity's stage. When it is empty or unknown, the type's first open stage is used.")),
                (nameof(OpportunityPart.IsClosed), Flag("Whether the stage is closed. The site sets it from the stage whenever the opportunity is saved or imported.")),
                (nameof(OpportunityPart.IsWon), Flag("Whether the stage is a won stage. The site sets it from the stage whenever the opportunity is saved or imported.")),
                (nameof(OpportunityPart.Probability), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer | SchemaValueType.Null)
                    .Minimum(0)
                    .Maximum(100)
                    .Description("The chance, from 0 to 100, that the opportunity is won. When it is null, or the stage is closed, the stage's probability is used.")),
                (nameof(OpportunityPart.CampaignId), NullableString("The ItemId of the campaign the opportunity came from.")),
                (nameof(OpportunityPart.ConvertedFromLeadItemId), NullableString("The content item identifier of the lead the opportunity was created from.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder NullableString(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description(description);

    private static JsonSchemaBuilder Flag(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description(description);
}
