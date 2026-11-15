using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Stores pay statements in the Payroll collection. A statement that is not a draft is never deleted: a correction is a
/// reversing statement.
/// </summary>
public sealed class PayStatementStore : DocumentCatalog<PayStatement, PayStatementIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayStatementStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public PayStatementStore(ISession session)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    // Processing, voiding and a review must not write over each other's statement.
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Lists the statements matching a filter on the index.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The statements.</returns>
    public async Task<IReadOnlyList<PayStatement>> ListAsync(System.Linq.Expressions.Expression<Func<PayStatementIndex, bool>> filter, CancellationToken cancellationToken = default)
        => (await Session.Query<PayStatement, PayStatementIndex>(filter, collection: CollectionName).ListAsync(cancellationToken)).ToList();

    /// <inheritdoc />
    protected override ValueTask DeletingAsync(PayStatement entry)
    {
        if (entry.Status != PayStatementStatus.Draft)
        {
            throw new InvalidOperationException("A pay statement that is final can't be deleted: void it instead.");
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Reads the accumulators from the line rows of statements that are not drafts.
/// </summary>
public sealed class PayAccumulatorService : IPayAccumulatorService
{
    private readonly ISession _session;
    private readonly IEmployerManager _employers;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayAccumulatorService"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="employers">The employers, for the currency of the amounts.</param>
    public PayAccumulatorService(ISession session, IEmployerManager employers)
    {
        _session = session;
        _employers = employers;
    }

    /// <inheritdoc />
    public async Task<PayAccumulators> GetAsync(string staffMemberId, string employerId, DateOnly payDate, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken = default)
    {
        var payDay = PayStatementLineIndex.DayNumber(payDate);
        var periodStartDay = PayStatementLineIndex.DayNumber(periodStart);
        var periodEndDay = PayStatementLineIndex.DayNumber(periodEnd);
        var rows = await _session.QueryIndex<PayStatementLineIndex>(row => row.StaffMemberId == staffMemberId && row.EmployerId == employerId && row.PayDay <= payDay, collection: PayrollConstants.CollectionName)
            .ListAsync(cancellationToken);
        var currency = (await _employers.FindByIdAsync(employerId, cancellationToken))?.Currency;

        return Sum(rows, payDate, periodStartDay, periodEndDay, currency);
    }

    /// <summary>
    /// Sums line rows into accumulators.
    /// </summary>
    /// <param name="rows">The rows, of one person and employer, paid on or before the pay date.</param>
    /// <param name="payDate">The pay date.</param>
    /// <param name="periodStartDay">The first day of the period being calculated, as a number.</param>
    /// <param name="periodEndDay">The last day of the period being calculated, as a number.</param>
    /// <param name="currency">The currency of the amounts.</param>
    /// <returns>The accumulators.</returns>
    public static PayAccumulators Sum(IEnumerable<PayStatementLineIndex> rows, DateOnly payDate, int periodStartDay, int periodEndDay, string currency)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var accumulators = new PayAccumulators();
        var unit = 1m;

        for (var i = string.IsNullOrEmpty(currency) ? 2 : CurrencyScale.GetDecimalPlaces(currency); i > 0; i--)
        {
            unit /= 10;
        }

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.LineKey))
            {
                continue;
            }

            if (!accumulators.Totals.TryGetValue(row.LineKey, out var totals))
            {
                totals = new PayAccumulator();
                accumulators.Totals[row.LineKey] = totals;
            }

            var amount = row.AmountMinor * unit;
            var year = row.PayDay / 10000;
            var month = row.PayDay / 100 % 100;

            totals.Lifetime += amount;

            if (year != payDate.Year)
            {
                continue;
            }

            totals.Year += amount;
            totals.YearHours += row.HoursHundredths / 100m;
            totals.YearTaxableWages += row.TaxableWagesMinor * unit;

            if ((month - 1) / 3 == (payDate.Month - 1) / 3)
            {
                totals.Quarter += amount;
            }

            if (month == payDate.Month)
            {
                totals.Month += amount;
            }

            if (row.PeriodStartDay == periodStartDay && row.PeriodEndDay == periodEndDay)
            {
                totals.Period += amount;
                totals.PeriodTaxableWages += row.TaxableWagesMinor * unit;
                totals.PeriodFlatWages += row.FlatWagesMinor * unit;
            }
        }

        return accumulators;
    }
}
