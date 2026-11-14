using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Payroll.Core.Indexes;

/// <summary>
/// Indexes pay statements.
/// </summary>
public sealed class PayStatementIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the pay run.</summary>
    public string RunId { get; set; }

    /// <summary>Gets or sets the staff member.</summary>
    public string StaffMemberId { get; set; }

    /// <summary>Gets or sets the employer.</summary>
    public string EmployerId { get; set; }

    /// <summary>Gets or sets the pay date as a number, such as 20261115.</summary>
    public int PayDay { get; set; }

    /// <summary>Gets or sets the number.</summary>
    public string Number { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public string Status { get; set; }

    /// <summary>Gets or sets the statement reversed by this one.</summary>
    public string VoidOf { get; set; }
}

/// <summary>
/// One row per line of a statement that is not a draft: the accumulators are the sums of these rows. Amounts are whole
/// minor units and hours are hundredths, in integer columns: on SQLite a decimal column keeps whole values as integers
/// and the others as reals, and reading rows that mix them fails.
/// </summary>
public sealed class PayStatementLineIndex : MapIndex
{
    /// <summary>Gets or sets the statement.</summary>
    public string StatementId { get; set; }

    /// <summary>Gets or sets the staff member.</summary>
    public string StaffMemberId { get; set; }

    /// <summary>Gets or sets the employer.</summary>
    public string EmployerId { get; set; }

    /// <summary>Gets or sets the pay date as a number, such as 20261115.</summary>
    public int PayDay { get; set; }

    /// <summary>Gets or sets the first day of the period as a number.</summary>
    public int PeriodStartDay { get; set; }

    /// <summary>Gets or sets the last day of the period as a number.</summary>
    public int PeriodEndDay { get; set; }

    /// <summary>Gets or sets the key the line sums under (<see cref="PayAccumulators.CodeKey"/> or <see cref="PayAccumulators.TaxKey"/>).</summary>
    public string LineKey { get; set; }

    /// <summary>Gets or sets the line type.</summary>
    public string LineType { get; set; }

    /// <summary>Gets or sets the jurisdiction of a tax line.</summary>
    public string Jurisdiction { get; set; }

    /// <summary>Gets or sets the amount in minor units.</summary>
    public long AmountMinor { get; set; }

    /// <summary>Gets or sets the hours, in hundredths.</summary>
    public long HoursHundredths { get; set; }

    /// <summary>Gets or sets the taxable wages of a tax line, in minor units.</summary>
    public long TaxableWagesMinor { get; set; }

    /// <summary>
    /// Turns a day into the number the index keeps.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>The number, such as 20261115.</returns>
    public static int DayNumber(DateOnly day)
        => (day.Year * 10000) + (day.Month * 100) + day.Day;
}

/// <summary>
/// Maps pay statements to <see cref="PayStatementIndex"/> rows.
/// </summary>
public sealed class PayStatementIndexProvider : IndexProvider<PayStatement>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayStatementIndexProvider"/> class.
    /// </summary>
    public PayStatementIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PayStatement> context)
    {
        context
            .For<PayStatementIndex>()
            .Map(statement => new PayStatementIndex
            {
                ItemId = statement.ItemId,
                RunId = statement.RunId,
                StaffMemberId = statement.StaffMemberId,
                EmployerId = statement.EmployerId,
                PayDay = PayStatementLineIndex.DayNumber(statement.PayDate),
                Number = statement.Number,
                Status = statement.Status.ToString(),
                VoidOf = statement.VoidOf,
            });
    }
}

/// <summary>
/// Maps the lines of statements that are not drafts to <see cref="PayStatementLineIndex"/> rows.
/// </summary>
public sealed class PayStatementLineIndexProvider : IndexProvider<PayStatement>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayStatementLineIndexProvider"/> class.
    /// </summary>
    public PayStatementLineIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PayStatement> context)
    {
        context
            .For<PayStatementLineIndex>()
            .Map(statement => statement.Status == PayStatementStatus.Draft
                ? []
                : statement.Lines.Select(line => new PayStatementLineIndex
                {
                    StatementId = statement.ItemId,
                    StaffMemberId = statement.StaffMemberId,
                    EmployerId = statement.EmployerId,
                    PayDay = PayStatementLineIndex.DayNumber(statement.PayDate),
                    PeriodStartDay = PayStatementLineIndex.DayNumber(statement.PeriodStart),
                    PeriodEndDay = PayStatementLineIndex.DayNumber(statement.PeriodEnd),
                    LineKey = line.Type == PayLineType.Tax ? PayAccumulators.TaxKey(line.TaxCode) : PayAccumulators.CodeKey(line.PayCodeId),
                    LineType = line.Type.ToString(),
                    Jurisdiction = line.Jurisdiction,
                    AmountMinor = Minor(line.Amount, statement.Currency),
                    HoursHundredths = (long)Math.Round((line.Hours ?? 0) * 100, MidpointRounding.AwayFromZero),
                    TaxableWagesMinor = Minor(line.TaxableWages ?? 0, statement.Currency),
                }));
    }

    private static long Minor(decimal amount, string currency)
        => string.IsNullOrEmpty(currency) ? (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero) : CurrencyScale.ToMinorUnits(amount, currency);
}

/// <summary>
/// Creates the tables of pay statements and their lines.
/// </summary>
public static class PayStatementIndexSchema
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = PayrollConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<PayStatementIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("RunId", column => column.Nullable().WithLength(26))
            .Column<string>("StaffMemberId", column => column.Nullable().WithLength(26))
            .Column<string>("EmployerId", column => column.Nullable().WithLength(26))
            .Column<int>("PayDay")
            .Column<string>("Number", column => column.Nullable().WithLength(40))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<string>("VoidOf", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayStatementIndex>(table =>
        {
            table.CreateIndex("IDX_PayStatementIndex_ItemId", "ItemId", "DocumentId");
            table.CreateIndex("IDX_PayStatementIndex_Run", "RunId", "DocumentId");
            table.CreateIndex("IDX_PayStatementIndex_Staff", "StaffMemberId", "EmployerId", "PayDay", "DocumentId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<PayStatementLineIndex>(table => table
            .Column<string>("StatementId", column => column.WithLength(26))
            .Column<string>("StaffMemberId", column => column.Nullable().WithLength(26))
            .Column<string>("EmployerId", column => column.Nullable().WithLength(26))
            .Column<int>("PayDay")
            .Column<int>("PeriodStartDay")
            .Column<int>("PeriodEndDay")
            .Column<string>("LineKey", column => column.Nullable().WithLength(80))
            .Column<string>("LineType", column => column.Nullable().WithLength(30))
            .Column<string>("Jurisdiction", column => column.Nullable().WithLength(50))
            .Column<long>("AmountMinor")
            .Column<long>("HoursHundredths")
            .Column<long>("TaxableWagesMinor"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PayStatementLineIndex>(table =>
        {
            table.CreateIndex("IDX_PayStatementLineIndex_Staff", "StaffMemberId", "EmployerId", "PayDay", "DocumentId");
            table.CreateIndex("IDX_PayStatementLineIndex_Statement", "StatementId", "DocumentId");
        }, collection: collection);
    }
}
