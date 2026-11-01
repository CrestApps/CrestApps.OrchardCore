namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A worker's pay rates, kept in the worker's properties.
/// </summary>
public sealed class WorkerPayRates
{
    /// <summary>
    /// Gets or sets the rates.
    /// </summary>
    public IList<WorkerPayRate> Rates { get; set; } = [];

    /// <summary>
    /// Returns the rate on a day, for an activity when it has its own.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <param name="activityCodeId">The activity, or <see langword="null"/>.</param>
    /// <returns>The rate, or <see langword="null"/> when none applies.</returns>
    public decimal? RateOn(DateOnly date, string activityCodeId)
    {
        var applicable = Rates.Where(rate => rate.EffectiveFrom <= date).ToArray();
        var forActivity = string.IsNullOrEmpty(activityCodeId)
            ? null
            : applicable.Where(rate => string.Equals(rate.ActivityCodeId, activityCodeId, StringComparison.Ordinal)).MaxBy(rate => rate.EffectiveFrom);

        return (forActivity ?? applicable.Where(rate => string.IsNullOrEmpty(rate.ActivityCodeId)).MaxBy(rate => rate.EffectiveFrom))?.HourlyRate;
    }
}
