using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Payroll.Recipes;

/// <summary>
/// Describes the "PayrollEmployers" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class EmployersRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PayrollConstants.EmployersRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var rate = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("EffectiveFrom", Text("The first day of the rate, yyyy-MM-dd.")),
                ("Rate", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The rate, as a percent such as 2.7.")))
            .Required("EffectiveFrom", "Rate");

        var registration = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Jurisdiction", Text("The jurisdiction, such as US or US-CA.")),
                ("Agency", Text("The tax agency.")),
                ("AccountId", Text("The employer's account id with the agency.")),
                ("DepositFrequency", Text("How often taxes are deposited, such as Monthly or SemiWeekly.")),
                ("Rates", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(rate).Description("The rates the agency assigned, each from a day on.")))
            .Required("Jurisdiction");

        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PayrollConstants.EmployersRecipeStepName).Description("Recipe step discriminator. Must be 'PayrollEmployers'.")),
                (EmployersRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The employer's 26-character id; omit it to match by name or create one.")),
                            ("Name", Text("The name the employer is known by, unique.")),
                            ("LegalName", Text("The legal name printed on statements, checks and forms.")),
                            ("TaxId", Text("The employer's own tax id, such as its employer identification number.")),
                            ("Address", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("The address printed on statements and checks: AddressLine1, AddressLine2, City, Region (the state or province code, such as TX), PostalCode, Country (two letters).")),
                            ("LogoPath", Text("The logo, a path in the media library.")),
                            ("CountryPack", Text("The country pack, such as US.")),
                            ("Currency", Text("The three-letter currency people are paid in.")),
                            ("FundingBankAccountId", Text("The id of the employer's bank account pay is sent from.")),
                            ("SignatoryName", Text("The name printed as the signatory of checks.")),
                            ("SignatoryTitle", Text("The signatory's title.")),
                            ("ContactName", Text("The payroll contact's name.")),
                            ("ContactEmail", Text("The payroll contact's email.")),
                            ("ContactPhone", Text("The payroll contact's phone.")),
                            ("Registrations", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(registration).Description("The employer's tax registrations.")),
                            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Active", "Inactive").Description("Whether the employer runs payroll.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The employers to create, or update by id or name.")))
            .Required("name", EmployersRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);
}
