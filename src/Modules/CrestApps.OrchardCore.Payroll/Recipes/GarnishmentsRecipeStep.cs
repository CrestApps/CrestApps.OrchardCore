using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Payroll.Recipes;

/// <summary>
/// Imports garnishment orders, matched by id. An order names its pay profile; its employer and staff member follow.
/// </summary>
internal sealed class GarnishmentsRecipeStep : CatalogRecipeStep<GarnishmentOrder>
{
    public const string EntriesName = "Orders";

    public GarnishmentsRecipeStep(IGarnishmentOrderManager manager)
        : base(PayrollConstants.GarnishmentsRecipeStepName, EntriesName, manager)
    {
    }
}

/// <summary>
/// Describes the "PayrollGarnishments" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class GarnishmentsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PayrollConstants.GarnishmentsRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        static JsonSchemaBuilder Text(string description)
            => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

        static JsonSchemaBuilder Number(string description)
            => new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description(description);

        static JsonSchemaBuilder Day(string description)
            => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.Date).Description(description);

        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PayrollConstants.GarnishmentsRecipeStepName).Description("Recipe step discriminator. Must be 'PayrollGarnishments'.")),
                (GarnishmentsRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Description("The garnishment orders to create, or update by id.")
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The order's 26-character id.")),
                            ("ProfileId", Text("The pay profile the order applies to.")),
                            ("Type", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Support", "FederalTaxLevy", "StateTaxLevy", "Bankruptcy", "StudentLoan", "Creditor", "Other")),
                            ("CaseNumber", Text("The case or docket number.")),
                            ("Agency", Text("The court or agency.")),
                            ("PayCodeId", Text("The deduction pay code.")),
                            ("Amount", Number("The amount each pay.")),
                            ("Percent", Number("The percent of disposable earnings each pay.")),
                            ("LimitPercent", Number("The most, as a percent of disposable earnings, the order may take with the orders before it of its limit group.")),
                            ("ProtectedWeeklyAmount", Number("The pay a week left to the person before a creditor order takes anything.")),
                            ("ExemptAmount", Number("The pay each period exempt from a tax levy.")),
                            ("TotalOwed", Number("The total owed, or null.")),
                            ("Priority", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The lowest number is withheld first.")),
                            ("ReceivedDate", Day("The day the order was received.")),
                            ("StartDate", Day("The first pay date the order applies to.")),
                            ("EndDate", Day("The last pay date, or null.")),
                            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Active", "Suspended", "Completed", "Released")),
                            ("PayeeName", Text("The payee's name.")),
                            ("PayeeAddress", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("The payee's mailing address.")),
                            ("PaymentMethod", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Check", "BankAccount", "Outside")),
                            ("PayeeBankAccountId", Text("The payee's bank account under Banking, for a deposit.")),
                            ("Notes", Text("Notes.")))
                        .Required("ProfileId", "PayCodeId", "StartDate")
                        .AdditionalProperties(true))))
            .Required("name", GarnishmentsRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
