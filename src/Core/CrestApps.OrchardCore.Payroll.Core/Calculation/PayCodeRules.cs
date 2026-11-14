using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Core.Calculation;

/// <summary>
/// What a person already got or gave by a pay code, read from the accumulators, for the limits of a line.
/// </summary>
public sealed class PayLimitUsage
{
    /// <summary>
    /// Gets or sets what was already taken in this pay period (an earlier line of the same run, or an earlier run with
    /// the same period).
    /// </summary>
    public decimal ThisPeriod { get; set; }

    /// <summary>
    /// Gets or sets what was taken in the calendar year of the pay date, before this line.
    /// </summary>
    public decimal YearToDate { get; set; }

    /// <summary>
    /// Gets or sets what was ever taken, before this line.
    /// </summary>
    public decimal LifetimeToDate { get; set; }
}

/// <summary>
/// The limit that cut a line.
/// </summary>
public enum PayLimitKind
{
    /// <summary>
    /// No limit cut it.
    /// </summary>
    None,

    /// <summary>
    /// The code's or the item's limit for a period.
    /// </summary>
    Period,

    /// <summary>
    /// The code's or the item's limit for a year.
    /// </summary>
    Year,

    /// <summary>
    /// The code's lifetime limit.
    /// </summary>
    Lifetime,

    /// <summary>
    /// The item's goal (such as the amount of a loan).
    /// </summary>
    Goal,
}

/// <summary>
/// A line's amount after its limits.
/// </summary>
/// <param name="Amount">What is taken or paid.</param>
/// <param name="LimitedBy">The limit that cut it, if any.</param>
/// <param name="Cut">What the limits cut off.</param>
public sealed record PayLimitResult(decimal Amount, PayLimitKind LimitedBy, decimal Cut);

/// <summary>
/// The limits of a pay code and of a pay item, applied to the amount a line wants. Pure: what was already taken comes
/// from the accumulators.
/// </summary>
public static class PayLimits
{
    /// <summary>
    /// Applies the code's and the item's period, year, lifetime and goal limits to an amount: the lowest room left wins,
    /// and a limit already reached leaves nothing.
    /// </summary>
    /// <param name="wanted">The amount the line wants, zero or more.</param>
    /// <param name="code">The pay code.</param>
    /// <param name="item">The pay item the line comes from, or <see langword="null"/>.</param>
    /// <param name="usage">What was already taken.</param>
    /// <param name="payDate">The pay date, which picks the yearly limit in effect.</param>
    /// <returns>The amount, and the limit that cut it.</returns>
    public static PayLimitResult Apply(decimal wanted, PayCode code, EmployeePayItem item, PayLimitUsage usage, DateOnly payDate)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(usage);

        if (wanted <= 0)
        {
            return new PayLimitResult(0, PayLimitKind.None, 0);
        }

        var settings = code.GetPayrollSettings();
        var room = wanted;
        var limitedBy = PayLimitKind.None;

        void Limit(decimal? limit, decimal taken, PayLimitKind kind)
        {
            if (limit is not { } value)
            {
                return;
            }

            var left = Math.Max(0, value - taken);

            if (left < room)
            {
                room = left;
                limitedBy = kind;
            }
        }

        Limit(settings.PeriodLimit, usage.ThisPeriod, PayLimitKind.Period);
        Limit(item?.PeriodMaximum, usage.ThisPeriod, PayLimitKind.Period);
        Limit(settings.YearLimitOn(payDate), usage.YearToDate, PayLimitKind.Year);
        Limit(item?.YearMaximum, usage.YearToDate, PayLimitKind.Year);
        Limit(settings.LifetimeLimit, usage.LifetimeToDate, PayLimitKind.Lifetime);
        Limit(item?.Goal, item?.PaidTowardGoal ?? 0, PayLimitKind.Goal);

        return new PayLimitResult(room, limitedBy, wanted - room);
    }
}

/// <summary>
/// A line of pay with its code, for the wages of a tax category.
/// </summary>
/// <param name="Code">The pay code.</param>
/// <param name="Amount">The amount.</param>
public sealed record PayCodeAmount(PayCode Code, decimal Amount);

/// <summary>
/// The tax treatment matrix: which lines add to a tax category's wages and which lower them.
/// </summary>
public static class PayCodeTaxMatrix
{
    /// <summary>
    /// Gets the wages of a tax category: earnings and imputed income taxed for it, less deductions exempt from it
    /// (taken before the tax). Employer contributions, reimbursements and memo lines add only when a code says they
    /// are taxed. Never below zero.
    /// </summary>
    /// <param name="category">The tax category.</param>
    /// <param name="lines">The lines of the pay.</param>
    /// <returns>The wages.</returns>
    public static decimal WagesFor(string category, IEnumerable<PayCodeAmount> lines)
    {
        ArgumentException.ThrowIfNullOrEmpty(category);
        ArgumentNullException.ThrowIfNull(lines);

        var wages = 0m;

        foreach (var line in lines)
        {
            var treatment = line.Code.TreatmentFor(category);

            if (line.Code.Class == PayCodeClass.Deduction)
            {
                if (treatment == PayCodeTaxTreatment.Exempt)
                {
                    wages -= line.Amount;
                }
            }
            else if (treatment == PayCodeTaxTreatment.Taxed)
            {
                wages += line.Amount;
            }
        }

        return Math.Max(0, wages);
    }
}
