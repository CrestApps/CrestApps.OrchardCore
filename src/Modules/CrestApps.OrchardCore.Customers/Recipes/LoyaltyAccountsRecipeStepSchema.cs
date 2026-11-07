using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Customers.Recipes;

/// <summary>
/// Describes the <c>LoyaltyAccounts</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class LoyaltyAccountsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => CustomersConstants.Recipes.LoyaltyAccounts;

    protected override string EntriesName => LoyaltyAccountsStep.EntriesName;

    protected override string Description => "Creates or updates customers' loyalty points, matched by ItemId, then by customer.";

    protected override string EntryDescription => "A customer's loyalty points.";

    protected override IEnumerable<string> RequiredEntryProperties => ["CustomerId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("CustomerId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The id of the customer who holds the points. Import the customers first.")),
            ("Points", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The points the customer has.")),
            ("Activity", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The points earned, spent, taken back and adjusted, oldest first.")),
        ];
}
