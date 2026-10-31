namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// How many people are needed and scheduled during one interval.
/// </summary>
public sealed class CoverageInterval
{
    /// <summary>
    /// Gets or sets when the interval starts.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ends.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the fewest people needed.
    /// </summary>
    public int Minimum { get; set; }

    /// <summary>
    /// Gets or sets the people wanted.
    /// </summary>
    public int Target { get; set; }

    /// <summary>
    /// Gets or sets the most people useful.
    /// </summary>
    public int? Maximum { get; set; }

    /// <summary>
    /// Gets or sets the people scheduled and counting toward coverage for the whole interval.
    /// </summary>
    public int Scheduled { get; set; }

    /// <summary>
    /// Gets how many people short of the target the interval is; negative when there are more than wanted.
    /// </summary>
    public int Gap => Target - Scheduled;
}
