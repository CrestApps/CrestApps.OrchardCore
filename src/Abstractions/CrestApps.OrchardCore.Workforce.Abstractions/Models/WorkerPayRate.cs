namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A worker's hourly rate from a day on, for all their work or one activity.
/// </summary>
public sealed class WorkerPayRate
{
    /// <summary>
    /// Gets or sets the first day the rate applies.
    /// </summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the rate an hour.
    /// </summary>
    public decimal HourlyRate { get; set; }

    /// <summary>
    /// Gets or sets the activity the rate is for; empty for all work.
    /// </summary>
    public string ActivityCodeId { get; set; }
}
