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
/// A staff member's dated pay rates and the cost of an hour of their work, kept in the staff member's properties by
/// the Compensation feature.
/// </summary>
public sealed class StaffPayRates
{
    /// <summary>
    /// Gets or sets the pay rates.
    /// </summary>
    public IList<StaffPayRate> Rates { get; set; } = [];

    /// <summary>
    /// Gets or sets the annual salary, for salaried staff.
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
    /// Gets the cost of an hour of work on a day: the hourly cost when set, else the general pay rate.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The cost, or <see langword="null"/> when unknown.</returns>
    public decimal? CostOn(DateOnly date)
    {
        return HourlyCost ?? RateOn(date);
    }
}
