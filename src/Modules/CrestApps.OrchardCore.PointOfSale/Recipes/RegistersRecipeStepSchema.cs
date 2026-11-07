using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.PointOfSale.Recipes;

/// <summary>
/// Describes the <c>Registers</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class RegistersRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => PosConstants.Recipes.Registers;

    protected override string EntriesName => RegistersStep.EntriesName;

    protected override string Description => "Creates or updates point-of-sale registers, matched by ItemId, then by name. Import the stock locations first.";

    protected override string EntryDescription => "A register: a till at one of the store's locations.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name", "LocationId", "Address"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The register's name, such as the store and till it's at.")),
            ("LocationId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The id of the stock location the register sells from.")),
            ("Address", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The store's address, with AddressLine1, City and a two-letter Country: its sales are taxed by it.")),
            ("Currency", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The three-letter currency the register sells in, or none for the store's.")),
            ("Roles", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The roles whose users can sell on the register.")),
            ("IsEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the register is in use.")),
        ];
}
