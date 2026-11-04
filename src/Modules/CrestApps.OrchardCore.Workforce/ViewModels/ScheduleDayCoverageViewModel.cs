namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The coverage of one day of a schedule.
/// </summary>
public class ScheduleDayCoverageViewModel
{
    /// <summary>
    /// Gets or sets whether anyone is needed that day.
    /// </summary>
    public bool HasRequirement { get; set; }

    /// <summary>
    /// Gets or sets how many intervals have fewer people than the minimum.
    /// </summary>
    public int ShortIntervals { get; set; }

    /// <summary>
    /// Gets or sets how many intervals have fewer people than the target.
    /// </summary>
    public int UnderTargetIntervals { get; set; }

    /// <summary>
    /// Gets or sets the most people missing from the minimum in one interval.
    /// </summary>
    public int WorstShort { get; set; }

    /// <summary>
    /// Gets or sets when that interval starts.
    /// </summary>
    public TimeOnly? WorstShortAt { get; set; }

    /// <summary>
    /// Gets or sets the most people missing from the target in one interval.
    /// </summary>
    public int WorstGap { get; set; }

    /// <summary>
    /// Gets or sets when that interval starts.
    /// </summary>
    public TimeOnly? WorstAt { get; set; }
}
