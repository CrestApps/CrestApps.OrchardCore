namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Where the participation cleanup goes on from: the index id its last run stopped at. The records of surveys with a
/// total limit are kept and sit at the lowest ids; when too many surveys keep records to leave them out of the query,
/// each run reads past them in memory, so going on from the last run's stop is what lets the cleanup reach the records
/// after them, however many are kept.
/// </summary>
public sealed class SurveyParticipationCleanupCursor
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the index id of the last record the previous run read, or 0 to start from the oldest record.
    /// </summary>
    public long AfterId { get; set; }
}
