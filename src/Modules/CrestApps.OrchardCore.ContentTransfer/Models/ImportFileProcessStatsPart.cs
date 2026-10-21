using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.ContentTransfer.Models;

public sealed class ImportFileProcessStatsPart : ContentPart
{
    /// <summary>
    /// The most rows that keep their error message. The part is saved with the entry after every batch, so a file
    /// where most rows fail would otherwise rewrite every message each time; <see cref="Errors"/> still lists every
    /// failed row.
    /// </summary>
    public const int MaxStoredErrorMessages = 1000;

    /// <summary>
    /// Current row being processed.
    /// </summary>
    public int CurrentRow { get; set; }

    /// <summary>
    /// Total processes records.
    /// </summary>

    public int TotalProcessed { get; set; }

    /// <summary>
    /// Total successfully imported records.
    /// </summary>
    public int ImportedCount { get; set; }

    /// <summary>
    /// The index of each row that failed validation.
    /// </summary>
    public HashSet<int> Errors { get; set; }

    /// <summary>
    /// The error message for each failed row, kept for at most <see cref="MaxStoredErrorMessages"/> rows.
    /// </summary>
    public Dictionary<int, string> ErrorMessages { get; set; }

    /// <summary>
    /// The failed rows for each kind of error, keyed by the kind's display name, such as "On a national do-not-call
    /// registry". Unlike <see cref="ErrorMessages"/> it is kept for every failed row, as it costs a row number per row,
    /// so it counts the rows that failed for each reason and names the reason of a row whose message was not kept.
    /// </summary>
    public Dictionary<string, HashSet<int>> ErrorCategories { get; set; }

    /// <summary>
    /// Total records available if the file.
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// The row the last run that stopped responding was stuck on; 0 when no run has.
    /// </summary>
    public int StalledRow { get; set; }

    /// <summary>
    /// How many runs in a row got stuck on <see cref="StalledRow"/>. Once it reaches the limit the row is skipped
    /// and recorded as an error.
    /// </summary>
    public int StalledRowAttempts { get; set; }
}
