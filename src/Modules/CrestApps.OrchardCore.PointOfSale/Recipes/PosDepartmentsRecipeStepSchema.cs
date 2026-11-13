using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.PointOfSale.Recipes;

/// <summary>
/// Describes the <c>PosDepartments</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class PosDepartmentsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => PosConstants.Recipes.Departments;

    protected override string EntriesName => PosDepartmentsStep.EntriesName;

    protected override string Description => "Creates or updates the departments registers ring amounts up in, matched by ItemId, then by name. Import them before the registers whose keys name them.";

    protected override string EntryDescription => "A department, such as Candy: amounts rung up in it are counted toward it and taxed by its tax category.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The department's name, such as Candy.")),
            ("Number", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The department's number, unique among departments, such as 4.")),
            ("TaxCategoryCode", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The code of the tax category its sales are taxed by, or none for the store's default rate.")),
            ("Color", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The color of its key on the register, a hex color such as #c2185b.")),
            ("MinimumAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The least amount one ring-up can be.")),
            ("MaximumAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The most one ring-up can be, so a mistyped amount is caught.")),
            ("Position", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Where it comes among the departments: a smaller number first.")),
            ("IsEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether registers can ring amounts up in it.")),
        ];
}
