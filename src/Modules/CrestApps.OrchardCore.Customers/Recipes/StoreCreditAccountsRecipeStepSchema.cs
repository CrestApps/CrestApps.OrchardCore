using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Customers.Recipes;

/// <summary>
/// Describes the <c>StoreCreditAccounts</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class StoreCreditAccountsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => CustomersConstants.Recipes.StoreCreditAccounts;

    protected override string EntriesName => StoreCreditAccountsStep.EntriesName;

    protected override string Description => "Creates or updates customers' store credit, matched by ItemId, then by customer and currency.";

    protected override string EntryDescription => "A customer's store credit in one currency.";

    protected override IEnumerable<string> RequiredEntryProperties => ["CustomerId", "Currency"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("CustomerId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The id of the customer who holds the credit. Import the customers first.")),
            ("Currency", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The ISO 4217 currency code of the credit.")),
            ("Balance", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The credit left.")),
            ("Activity", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The changes to the balance, oldest first.")),
        ];
}
