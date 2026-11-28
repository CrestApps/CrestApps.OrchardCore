namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Where the orphan file sweep of survey responses goes on from: the upload time its last run stopped at, so each run
/// reaches files the runs before it didn't, however many files the site keeps.
/// </summary>
public sealed class SurveyOrphanFileSweepCursor
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the upload time, in UTC, the next run starts from, or <see langword="null"/> to start from the
    /// oldest file.
    /// </summary>
    public DateTime? NextFromUtc { get; set; }
}
