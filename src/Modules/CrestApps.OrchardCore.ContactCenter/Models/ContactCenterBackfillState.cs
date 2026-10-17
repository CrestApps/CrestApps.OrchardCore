using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Remembers how far each one-off Contact Center data backfill has got, so a backfill run by a background task resumes
/// where it stopped after a restart and is never run again once it has finished.
/// </summary>
public sealed class ContactCenterBackfillState : Document
{
    /// <summary>
    /// Gets the position of the last record each unfinished backfill has read, by backfill name.
    /// </summary>
    public Dictionary<string, ContactCenterBackfillCursor> Cursors { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the names of the backfills that have finished.
    /// </summary>
    public HashSet<string> Completed { get; init; } = new(StringComparer.Ordinal);
}
