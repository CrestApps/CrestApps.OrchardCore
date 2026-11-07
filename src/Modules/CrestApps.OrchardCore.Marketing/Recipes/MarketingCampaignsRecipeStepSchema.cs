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

    protected override string EntryDescription => "An email campaign to a segment.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name", "SegmentId", "Subject", "Body"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the campaign. Only staff see it.")),
            ("SegmentId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The ItemId of the segment the campaign goes to. Import the segments first.")),
            ("Subject", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The email's subject.")),
            ("Body", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The email's body.")),
        ];
}
