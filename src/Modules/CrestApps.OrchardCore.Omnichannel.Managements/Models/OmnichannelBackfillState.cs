using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Models;

/// <summary>
/// Remembers how far each one-off data backfill has got, so a backfill run by a background task resumes where it
/// stopped after a restart and is never run again once it has finished.
/// </summary>
public sealed class OmnichannelBackfillState : Document
{
    /// <summary>
    /// Gets the document id of the last record each unfinished backfill has read, by backfill name.
    /// </summary>
    public Dictionary<string, long> Cursors { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the names of the backfills that have finished.
    /// </summary>
    public HashSet<string> Completed { get; init; } = new(StringComparer.Ordinal);
}
