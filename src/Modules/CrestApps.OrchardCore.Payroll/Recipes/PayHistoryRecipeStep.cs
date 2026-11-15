using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Staff.Services;
using Json.Schema;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Payroll.Recipes;

/// <summary>
/// Imports pay statements paid elsewhere as history, so the year's totals start right: each lands, final and paid, in a
/// historical pay run of its employer and pay date, which can't be calculated, processed or voided. A statement already
/// there (same id, or same employer and number) is skipped, so the step can run again.
/// </summary>
internal sealed class PayHistoryRecipeStep : NamedRecipeStepHandler
{
    public const string EntriesName = "Statements";

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly PayStatementStore _statements;
    private readonly IPayRunManager _runs;
    private readonly EmployerStore _employers;
    private readonly IStaffMemberManager _staff;
    private readonly INumberSequence _numbers;
    private readonly IClock _clock;

    public PayHistoryRecipeStep(PayStatementStore statements, IPayRunManager runs, EmployerStore employers, IStaffMemberManager staff, INumberSequence numbers, IClock clock)
        : base(PayrollConstants.PayHistoryRecipeStepName)
    {
        _statements = statements;
        _runs = runs;
        _employers = employers;
        _staff = staff;
        _numbers = numbers;
        _clock = clock;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step[EntriesName] is not JsonArray entries)
        {
            return;
        }

        var runs = new Dictionary<(string EmployerId, DateOnly PayDate), PayRun>();
        var imported = new List<PayStatement>();

        foreach (var entry in entries.OfType<JsonObject>())
        {
            var statement = entry.Deserialize<PayStatement>(_options) ?? new PayStatement();

            if (string.IsNullOrEmpty(statement.StaffMemberId) && entry["EmployeeNumber"]?.GetValue<string>() is { Length: > 0 } number)
            {
                statement.StaffMemberId = (await _staff.FindByEmployeeNumberAsync(number))?.ItemId;
            }

            if (string.IsNullOrEmpty(statement.EmployerId) || await _employers.FindByIdAsync(statement.EmployerId) is null)
            {
                context.Errors.Add($"A pay statement in the {PayrollConstants.PayHistoryRecipeStepName} step names no employer of the site.");

                continue;
            }

            if (string.IsNullOrEmpty(statement.StaffMemberId) || await _staff.FindByIdAsync(statement.StaffMemberId) is null)
            {
                context.Errors.Add($"A pay statement in the {PayrollConstants.PayHistoryRecipeStepName} step names no staff member of the site (StaffMemberId or EmployeeNumber).");

                continue;
            }

            if (statement.PayDate == default || statement.Status == PayStatementStatus.Draft)
            {
                context.Errors.Add($"A pay statement in the {PayrollConstants.PayHistoryRecipeStepName} step has no pay date or is a draft: only final statements are history.");

                continue;
            }

            // A tax line without its taxable wages would leave the year's Social Security and Medicare wages short.
            if (statement.Lines.Any(line => line.Type == PayLineType.Tax && line.TaxableWages is null))
            {
                context.Errors.Add($"A pay statement in the {PayrollConstants.PayHistoryRecipeStepName} step has a tax line without TaxableWages: give the wages each tax was worked out on.");

                continue;
            }

            // Already there: the step can run again.
            if (!string.IsNullOrEmpty(statement.ItemId) && await _statements.FindByIdAsync(statement.ItemId) is not null)
            {
                continue;
            }

            var employerId = statement.EmployerId;
            var statementNumber = statement.Number;

            if (!string.IsNullOrEmpty(statementNumber) && (await _statements.ListAsync(index => index.EmployerId == employerId && index.Number == statementNumber)).Count > 0)
            {
                continue;
            }

            var run = await HistoricalRunAsync(runs, statement);

            statement.ItemId = string.IsNullOrEmpty(statement.ItemId) ? UniqueId.GenerateId() : statement.ItemId;
            statement.RunId = run.ItemId;
            statement.Number = string.IsNullOrEmpty(statement.Number)
                ? (await _numbers.NextAsync(PayrollConstants.PayStatementNumberSeries)).ToString(CultureInfo.InvariantCulture)
                : statement.Number;
            statement.Status = statement.Status == PayStatementStatus.Final ? PayStatementStatus.Paid : statement.Status;
            statement.CreatedUtc = statement.CreatedUtc == default ? _clock.UtcNow : statement.CreatedUtc;
            statement.ProcessedUtc ??= statement.CreatedUtc;

            // References to disbursements and checks of the other site mean nothing here.
            foreach (var payment in statement.Payments)
            {
                payment.ReferenceId = null;
            }

            await _statements.CreateAsync(statement);
            imported.Add(statement);

            if (!run.StaffMemberIds.Contains(statement.StaffMemberId))
            {
                run.StaffMemberIds.Add(statement.StaffMemberId);
            }
        }

        foreach (var run in runs.Values)
        {
            var statements = await _statements.ListAsync(index => index.RunId == run.ItemId);

            run.Totals = DefaultPayRunService.Totals(statements.Concat(imported.Where(statement => statement.RunId == run.ItemId)).DistinctBy(statement => statement.ItemId));
            await _runs.UpdateAsync(run);
        }
    }

    // The historical run of an employer and a pay date: found, or added paid.
    private async Task<PayRun> HistoricalRunAsync(Dictionary<(string EmployerId, DateOnly PayDate), PayRun> runs, PayStatement statement)
    {
        var key = (statement.EmployerId, statement.PayDate);

        if (runs.TryGetValue(key, out var run))
        {
            return run;
        }

        run = (await _runs.ListByEmployersAsync([statement.EmployerId])).FirstOrDefault(candidate => candidate.Type == PayRunType.Historical && candidate.PayDate == statement.PayDate);

        if (run is null)
        {
            run = await _runs.NewAsync();
            run.EmployerId = statement.EmployerId;
            run.Type = PayRunType.Historical;
            run.PeriodStart = statement.PeriodStart == default ? statement.PayDate : statement.PeriodStart;
            run.PeriodEnd = statement.PeriodEnd == default ? statement.PayDate : statement.PeriodEnd;
            run.PayDate = statement.PayDate;
            run.StaffMemberIds = [statement.StaffMemberId];
            run.Status = PayRunStatus.Paid;
            run.ProcessedUtc = _clock.UtcNow;
            run.ProcessedBy = "Recipe";
            await _runs.CreateAsync(run);
        }

        runs[key] = run;

        return run;
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
