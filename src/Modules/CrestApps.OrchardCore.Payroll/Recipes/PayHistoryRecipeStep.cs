using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Payroll.Recipes;

/// <summary>
/// Imports pay statements paid elsewhere as history, so the year's totals start right, through
/// <see cref="IPayHistoryImporter"/>: each lands, final and paid, in a historical pay run of its employer and pay date,
/// which can't be calculated, processed or voided. A statement already there (same id, or same employer and number) is
/// skipped, so the step can run again.
/// </summary>
internal sealed class PayHistoryRecipeStep : NamedRecipeStepHandler
{
    public const string EntriesName = "Statements";

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly IPayHistoryImporter _importer;

    public PayHistoryRecipeStep(IPayHistoryImporter importer)
        : base(PayrollConstants.PayHistoryRecipeStepName)
    {
        _importer = importer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step[EntriesName] is not JsonArray entries)
        {
            return;
        }

        var result = await _importer.ImportAsync(
            [.. entries.OfType<JsonObject>().Select(entry => new PayHistoryEntry
            {
                Statement = entry.Deserialize<PayStatement>(_options) ?? new PayStatement(),
                EmployeeNumber = entry["EmployeeNumber"]?.GetValue<string>(),
            })],
            "Recipe");

        foreach (var error in result.Errors)
        {
            context.Errors.Add($"{PayrollConstants.PayHistoryRecipeStepName}: {error}");
        }
    }
}

/// <summary>
/// The schema of the pay history step.
/// </summary>
internal sealed class PayHistoryRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PayrollConstants.PayHistoryRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        static JsonSchemaBuilder Text(string description)
            => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

        static JsonSchemaBuilder Number(string description)
            => new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description(description);

        var line = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Type", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Earning", "Deduction", "EmployerContribution", "ImputedIncome", "Reimbursement", "Memo", "Tax")),
                ("PayCodeId", Text("The pay code's id, for a line that is not a tax.")),
                ("TaxCode", Text("The tax's code, for a tax.")),
                ("TaxCategory", Text("The tax category, such as IncomeTax.")),
                ("Description", Text("What the line is.")),
                ("Jurisdiction", Text("The tax's jurisdiction, such as US.")),
                ("Payer", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Employee", "Employer")),
                ("Hours", Number("The hours.")),
                ("Rate", Number("The rate.")),
                ("Amount", Number("The amount, positive.")),
                ("TaxableWages", Number("The wages the tax was worked out on; required on a tax line.")))
            .Required("Type", "Amount");

        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PayrollConstants.PayHistoryRecipeStepName).Description("Recipe step discriminator. Must be 'PayrollPayHistory'.")),
                (PayHistoryRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Description("Final pay statements paid elsewhere, imported as history into a historical pay run per employer and pay date. Statements already there are skipped.")
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The statement's id; kept, so the step can run again.")),
                            ("Number", Text("The statement's number; given from the site's sequence when empty.")),
                            ("EmployerId", Text("The employer that paid.")),
                            ("StaffMemberId", Text("The staff member paid.")),
                            ("EmployeeNumber", Text("The employee number, when the staff member id is not known.")),
                            ("PayDate", Text("The pay date, yyyy-MM-dd.")),
                            ("PeriodStart", Text("The first day of the period, yyyy-MM-dd.")),
                            ("PeriodEnd", Text("The last day of the period, yyyy-MM-dd.")),
                            ("Currency", Text("The three-letter currency code.")),
                            ("Gross", Number("The gross pay.")),
                            ("Net", Number("The net pay.")),
                            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Final", "Paid", "Voided")),
                            ("Lines", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(line)),
                            ("Payments", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object))),
                            ("Snapshot", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The names and addresses as printed.")))
                        .Required("EmployerId", "PayDate"))))
            .Required("name", PayHistoryRecipeStep.EntriesName)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
