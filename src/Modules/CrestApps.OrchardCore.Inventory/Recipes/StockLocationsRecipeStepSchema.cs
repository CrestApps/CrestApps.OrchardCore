using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Inventory.Recipes;

/// <summary>
/// Describes the <c>StockLocations</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class StockLocationsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => InventoryConstants.StockLocationsRecipeStep;

    protected override string EntriesName => StockLocationsStep.EntriesName;

    protected override string Description => "Creates or updates the store's own stock locations. A location keeps the id it is given, such as default; one without an id is matched by its name.";

    protected override string EntryDescription => "A stock location the store keeps units at.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The location's name, such as Main warehouse.")),
            ("Address", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The location's address, as one line of text.")),
            ("Position", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The location's place in the list: orders take units from the first locations first.")),
        ];
}
