using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Banking.Recipes;

/// <summary>
/// Describes the "BankAccounts" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class BankAccountsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => BankingConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(BankingConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'BankAccounts'.")),
                (BankAccountsRecipeStep.AccountsProperty, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The account's 26-character id; omit it to create a new one.")),
                            ("Name", Text("The name the account is listed by.")),
                            ("OwnerKind", Text("The party kind of the owner, such as Staff, User or Account; omit it for one of the business's own accounts.")),
                            ("OwnerId", Text("The owner's id in its kind.")),
                            ("HolderName", Text("The name on the account, as the bank knows it.")),
                            ("Kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Checking", "Savings").Description("The account type.")),
                            ("Country", Text("The bank's two-letter ISO country code.")),
                            ("Currency", Text("The three-letter ISO currency code.")),
                            ("BankName", Text("The bank's name.")),
                            ("RoutingNumber", Text("The bank's routing number; checked for the country.")),
                            ("AccountNumber", Text("The account number in clear text; protected with the importing site's keys. Omit it to import the account without numbers.")),
                            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Unverified", "VerificationSent", "Verified", "Failed", "Closed").Description("The status; kept only when the numbers are imported too.")),
                            ("StatusReason", Text("Why the account has that status.")),
                            ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).AdditionalProperties(true).Description("What other features keep on the account, by the name of the settings object; each one given replaces the stored one. NachaOriginSettings holds the Bank file (ACH) settings of one of the business's own accounts: Enabled, ImmediateDestination (a routing number, or empty for the account's bank), ImmediateDestinationName, ImmediateOrigin (ten characters at most), ImmediateOriginName, CompanyName (sixteen at most), CompanyIdentification (ten at most), CompanyEntryDescription (ten at most, such as PAYROLL) and IncludeOffset.")))
                        .Required("Name", "HolderName")
                        .AdditionalProperties(true))
                    .Description("The bank accounts to create, or update by id.")))
            .Required("name", BankAccountsRecipeStep.AccountsProperty)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);
}
