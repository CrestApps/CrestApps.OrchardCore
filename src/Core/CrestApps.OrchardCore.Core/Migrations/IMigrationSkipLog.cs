namespace CrestApps.OrchardCore.Core.Migrations;

/// <summary>
/// Keeps the summaries of data migrations that skipped records, until an administrator dismisses them, so a skip is
/// seen in the admin rather than only in the log.
/// </summary>
public interface IMigrationSkipLog
{
    /// <summary>
    /// Records the summary of a migration run that skipped records. A report with no skips records nothing.
    /// </summary>
    /// <param name="report">The migration's skip report.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RecordAsync(MigrationSkipReport report, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the summaries no administrator has dismissed yet, oldest first.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<MigrationSkipEntry>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Dismisses every summary.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DismissAllAsync(CancellationToken cancellationToken = default);
}
