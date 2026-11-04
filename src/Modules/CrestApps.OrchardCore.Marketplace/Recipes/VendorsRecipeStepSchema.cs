using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Marketplace.Recipes;

/// <summary>
/// Describes the <c>Vendors</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class VendorsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => MarketplaceConstants.Recipes.Vendors;

    protected override string EntriesName => VendorsStep.EntriesName;

    protected override string Description => "Creates or updates marketplace vendors, matched by ItemId, then by name. Users are named by UserNames and must exist on the site. Payout accounts are never imported.";

    protected override string EntryDescription => "A vendor that sells on the marketplace.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The vendor's name.")),
            ("Email", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The vendor's email.")),
            ("UserNames", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The names of the users who manage the vendor. Each must exist on the site.")),
            ("CommissionPercent", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The commission the store keeps, from 0 to 100 percent, or none for the store's.")),
            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("The vendor's status.")),
            ("Country", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The vendor's two-letter country code.")),
        ];
}
