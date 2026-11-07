using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Customers.Recipes;

/// <summary>
/// Describes the <c>ReferralProfiles</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ReferralProfilesRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => CustomersConstants.Recipes.ReferralProfiles;

    protected override string EntriesName => ReferralProfilesStep.EntriesName;

    protected override string Description => "Creates or updates customers' referral codes, matched by ItemId, then by customer. A code another customer uses is refused.";

    protected override string EntryDescription => "A customer's referral code and the referrals it earned.";

    protected override IEnumerable<string> RequiredEntryProperties => ["CustomerId", "Code"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("CustomerId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The id of the customer who shares the code. Import the customers first.")),
            ("Code", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The referral code, unique across customers.")),
            ("Referrals", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The friends who ordered with the code and the rewards the customer earned.")),
        ];
}
