namespace CrestApps.OrchardCore.BackgroundWork;

/// <summary>
/// Limits how much of the database a long-running background job may use, so a bulk import or a bulk delete
/// cannot starve the requests people are making at the same time.
/// </summary>
/// <remarks>
/// Bound from the <c>CrestApps:BackgroundWork:Pacing</c> section of the tenant configuration. A job paces itself
/// by pausing after each batch for a time proportional to how long the batch took, so it adapts to the size of
/// the database and to how busy it is: when other work slows the batches down, the pauses grow with them.
/// </remarks>
public sealed class BackgroundWorkPacingOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "CrestApps:BackgroundWork:Pacing";

    /// <summary>
    /// The smallest share a job may be given. Below it a job of millions of rows would take days.
    /// </summary>
    public const double MinimumDatabaseShare = 0.05;

    /// <summary>
    /// Gets or sets the share of time, from 0.05 to 1, a background job spends working against the database.
    /// </summary>
    /// <remarks>
    /// Defaults to 0.25: after a batch that took one second the job waits three seconds, so it uses about a
    /// quarter of the database and leaves the rest for everything else. 1 turns pacing off and runs batches
    /// back to back.
    /// </remarks>
    public double DatabaseShare { get; set; } = 0.25;

    /// <summary>
    /// Gets or sets the longest a job waits after one batch, however slow the batch was.
    /// </summary>
    public TimeSpan MaxPause { get; set; } = TimeSpan.FromSeconds(30);
}
