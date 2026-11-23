using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Marketing.Recipes;

/// <summary>
/// Describes the <c>MarketingCampaigns</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class MarketingCampaignsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => MarketingCampaignsStep.StepKey;

    protected override string EntriesName => MarketingCampaignsStep.EntriesName;

    protected override string Description => "Creates or updates marketing campaigns, matched by ItemId. A new campaign is always a draft, and a campaign that is scheduled or sending is left as it is.";

    protected override string EntryDescription => "An email campaign to a segment, or to a mailing list.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name", "Subject", "Body"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the campaign. Only staff see it.")),
            ("AudienceKind", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Default("Segment").Description("Who the campaign goes to: \"Segment\", a segment's customers (also when empty), or \"MailingList\", a mailing list's subscribed members, which needs the Mailing Lists feature.")),
            ("SegmentId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The ItemId of the segment the campaign goes to, when its audience is a segment. Import the segments first.")),
            ("MailingListId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The ItemId of the mailing list the campaign goes to, when its audience is a mailing list. Import the mailing lists first.")),
            ("Subject", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The email's subject.")),
            ("Body", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The email's body.")),
        ];
}
