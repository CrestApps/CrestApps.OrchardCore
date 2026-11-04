using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Core.Migrations;

/// <summary>
/// Collects the records a data migration could not migrate and skipped, so the skips are visible: each skip is logged
/// with the record's identifier, and <see cref="LogSummary"/> writes one warning at the end with the count and the
/// identifiers, which an administrator can find without reading every line of the migration's log.
/// </summary>
public sealed class MigrationSkipReport
{
    /// <summary>
    /// The most identifiers the summary lists; the count always covers every skip.
    /// </summary>
    public const int MaxListedIds = 50;

    private readonly List<string> _skippedIds = [];
    private readonly string _migrationName;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationSkipReport"/> class.
    /// </summary>
    /// <param name="migrationName">The name of the migration step, shown in every log line.</param>
    /// <param name="logger">The logger the skips and the summary are written to.</param>
    public MigrationSkipReport(string migrationName, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(migrationName);
        ArgumentNullException.ThrowIfNull(logger);

        _migrationName = migrationName;
        _logger = logger;
    }

    /// <summary>
    /// Gets how many records were skipped.
    /// </summary>
    public int Count => _skippedIds.Count;

    /// <summary>
    /// Gets the identifiers of the skipped records, in the order they were skipped.
    /// </summary>
    public IReadOnlyList<string> SkippedIds => _skippedIds;

    /// <summary>
    /// Records a skipped record and logs it as a warning with its identifier and the reason.
    /// </summary>
    /// <param name="recordId">The identifier of the skipped record (an item ID, a name or a document key).</param>
    /// <param name="reason">Why the record was skipped.</param>
    /// <param name="exception">The exception that caused the skip, if any.</param>
    public void Skip(string recordId, string reason, Exception exception = null)
    {
        var id = string.IsNullOrWhiteSpace(recordId) ? "(no id)" : recordId;

        _skippedIds.Add(id);

        _logger.LogWarning(
            exception,
            "Migration '{MigrationName}' skipped record '{RecordId}': {Reason}",
            _migrationName,
            id.SanitizeLogValue(),
            reason.SanitizeLogValue());
    }

    /// <summary>
    /// Writes one warning that summarizes the skipped records, or nothing when no record was skipped.
    /// </summary>
    public void LogSummary()
    {
        if (_skippedIds.Count == 0)
        {
            return;
        }

        var listed = string.Join(", ", _skippedIds.Take(MaxListedIds));
        var more = _skippedIds.Count > MaxListedIds
            ? $" and {_skippedIds.Count - MaxListedIds} more"
            : string.Empty;

        _logger.LogWarning(
            "Migration '{MigrationName}' finished with {SkippedCount} skipped record(s) that were not migrated and need attention: {SkippedIds}{More}.",
            _migrationName,
            _skippedIds.Count,
            listed.SanitizeLogValue(),
            more);
    }
}
