using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Marketing.Recipes;

/// <summary>
/// Describes the <c>MarketingSegments</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class MarketingSegmentsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => MarketingSegmentsStep.StepKey;

    protected override string EntriesName => MarketingSegmentsStep.EntriesName;

    protected override string Description => "Creates or updates marketing segments, matched by ItemId.";

    protected override string EntryDescription => "A segment of customers, chosen by their orders, spending and country.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the segment.")),
            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the segment is for.")),
            ("Rules", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The rules a customer matches: MinOrders, MaxOrders, MinSpent, MaxSpent, LastOrderAtLeastDaysAgo, LastOrderWithinDays, FirstOrderWithinDays and Country (a two-letter code).")),
        ];
}
