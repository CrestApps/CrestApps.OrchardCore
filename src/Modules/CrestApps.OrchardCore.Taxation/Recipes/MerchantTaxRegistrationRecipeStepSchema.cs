using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Taxation.Deployments;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Describes the <c>MerchantTaxRegistration</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class MerchantTaxRegistrationRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => TaxationDeploymentSteps.MerchantTaxRegistration;

    protected override string EntriesName => MerchantTaxRegistrationStep.EntriesName;

    protected override string Description => "Creates or updates the store's own tax registrations, matched by ItemId, then by name.";

    protected override string EntryDescription => "A tax registration: where the store collects a tax, under which number.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name", "JurisdictionId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The registration's name.")),
            ("JurisdictionId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The id of the tax jurisdiction the store is registered in.")),
            ("TaxType", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The tax type the registration covers, or none for every type.")),
            ("RegistrationNumber", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The number the tax authority gave the store.")),
            ("IsActive", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the store collects the tax under this registration.")),
            ("ThresholdAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The sales above which the store must collect the tax, or none.")),
            ("ThresholdAccumulatedAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The sales recorded into the jurisdiction so far, measured against ThresholdAmount. It is kept up to date as sales are recorded, so it is usually left out.")),
            ("RegistrationDateUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("When the tax authority granted the registration.")),
            ("EffectiveFromUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("When the registration takes effect.")),
            ("EffectiveToUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("When the registration ends.")),
            ("Filing", new JsonSchemaBuilder()
                .Type(SchemaValueType.Object | SchemaValueType.Null)
                .Description("How often the registration files its returns and when they are due (Tax Returns), or none.")
                .Properties(
                    ("Frequency", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Monthly", "Quarterly", "Annual").Description("How often a return is filed.")),
                    ("DueMonthsAfterPeriod", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(12).Description("How many months after the period's last month a return is due. Default 1.")),
                    ("DueDay", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Maximum(31).Description("The day of that month a return is due; a day past the month's end is its last day. Default 20.")),
                    ("MoveToNextBusinessDay", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a due date on a weekend moves to the Monday after. Default true.")),
                    ("Form", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The return form of the periodic returns, or none for the generic form.")),
                    ("AnnualForm", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The form of a yearly reconciliation return filed besides the periodic ones, or none.")),
                    ("AnnualDueMonthsAfterYear", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(12).Description("The month after the year the annual return is due in. Default 1.")),
                    ("AnnualDueDay", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Maximum(31).Description("The day of that month the annual return is due. Default 31.")))),
        ];
}
