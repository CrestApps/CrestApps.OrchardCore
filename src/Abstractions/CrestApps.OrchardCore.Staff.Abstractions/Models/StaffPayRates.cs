namespace CrestApps.OrchardCore.Staff.Models;

/// <summary>
/// A staff member's pay rate from a day on, optionally for one kind of work.
/// </summary>
public sealed class StaffPayRate
{
    /// <summary>
    /// Gets or sets the first day the rate applies.
    /// </summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the hourly rate.
    /// </summary>
    public decimal HourlyRate { get; set; }

    /// <summary>
    /// Gets or sets the kind of work (such as a Workforce activity code) the rate is for, or <see langword="null"/> for all work.
    /// </summary>
    public string ActivityCodeId { get; set; }
}

/// <summary>
/// How a staff member is paid.
/// </summary>
public enum StaffPayBasis
{
    /// <summary>
    /// By the hour, at the dated rates.
    /// </summary>
    Hourly,

    /// <summary>
    /// A yearly salary, at the dated salary rows.
    /// </summary>
    Salary,

    /// <summary>
    /// By the piece or unit of work; the rates are per unit.
    /// </summary>
    PerUnit,
}

/// <summary>
/// A staff member's yearly salary from a day on.
/// </summary>
public sealed class StaffSalary
{
    /// <summary>
    /// Gets or sets the first day the salary applies.
    /// </summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the yearly amount.
    /// </summary>
    public decimal AnnualAmount { get; set; }
}

/// <summary>
/// A staff member's pay: the basis, the currency, dated rates and salaries, and the cost of an hour of their work,
/// kept in the staff member's properties by the Compensation feature. Every change is written to the staff member's
/// history.
/// </summary>
public sealed class StaffPayRates
{
    /// <summary>
    /// Gets or sets how the staff member is paid.
    /// </summary>
    public StaffPayBasis PayBasis { get; set; }

    /// <summary>
    /// Gets or sets the three-letter currency code of the amounts, or <see langword="null"/> for the site's currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the pay rates: per hour, or per unit for <see cref="StaffPayBasis.PerUnit"/>.
    /// </summary>
    public IList<StaffPayRate> Rates { get; set; } = [];

    /// <summary>
    /// Gets or sets the dated yearly salaries.
    /// </summary>
    public IList<StaffSalary> Salaries { get; set; } = [];

    /// <summary>
    /// Gets or sets the yearly salary kept before salaries were dated; <see cref="SalaryOn"/> falls back to it when no
    /// dated salary has started.
    /// </summary>
    public decimal? AnnualSalary { get; set; }

    /// <summary>
    /// Gets or sets the loaded cost of an hour of work (pay, taxes and benefits), used to price labor.
    /// </summary>
    public decimal? HourlyCost { get; set; }

    /// <summary>
    /// Gets the hourly rate on a day for a kind of work: the latest started rate for that work, else the latest started
    /// general rate.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <param name="activityCodeId">The kind of work, or <see langword="null"/>.</param>
    /// <returns>The rate, or <see langword="null"/> when none applies.</returns>
    public decimal? RateOn(DateOnly date, string activityCodeId = null)
    {
        var started = Rates.Where(rate => rate.EffectiveFrom <= date).ToArray();

        if (!string.IsNullOrEmpty(activityCodeId)
            && started.Where(rate => rate.ActivityCodeId == activityCodeId).MaxBy(rate => rate.EffectiveFrom) is { } specific)
        {
            return specific.HourlyRate;
        }

        return started.Where(rate => string.IsNullOrEmpty(rate.ActivityCodeId)).MaxBy(rate => rate.EffectiveFrom)?.HourlyRate;
    }

    /// <summary>
    /// Gets the yearly salary on a day: the latest started dated salary, else <see cref="AnnualSalary"/>.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The salary, or <see langword="null"/> when none applies.</returns>
    public decimal? SalaryOn(DateOnly date)
        => Salaries.Where(salary => salary.EffectiveFrom <= date).MaxBy(salary => salary.EffectiveFrom)?.AnnualAmount ?? AnnualSalary;

    /// <summary>
    /// Gets the loaded cost of an hour of work on a day: the hourly cost when set, else the hourly rate.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The cost, or <see langword="null"/>.</returns>
    public decimal? CostOn(DateOnly date)
    {
        return HourlyCost ?? RateOn(date);
    }
}
