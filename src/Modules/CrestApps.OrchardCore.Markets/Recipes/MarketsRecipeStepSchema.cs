using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Markets.Recipes;

/// <summary>
/// Describes the <c>Markets</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class MarketsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => MarketsConstants.Recipes.Markets;

    protected override string EntriesName => MarketsStep.EntriesName;

    protected override string Description => "Creates or updates markets, matched by ItemId, then by name. A country can be in one market only.";

    protected override string EntryDescription => "A market: the countries it sells to, in one currency.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name", "Countries", "Currency"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The market's name, such as Europe.")),
            ("Countries", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The two-letter country codes the market covers. A country is in one market only.")),
            ("Currency", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The three-letter currency code prices are shown and charged in.")),
            ("ExchangeRate", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("How much of the market's currency one unit of the store's currency is worth.")),
            ("Rounding", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("How converted prices are rounded: None, WholeUnits, EndIn99 or EndIn95.")),
            ("Culture", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The language shoppers in the market see, or none to keep theirs.")),
            ("PricesIncludeTax", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the market's prices include tax.")),
            ("IsEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the market is in use.")),
        ];
}
