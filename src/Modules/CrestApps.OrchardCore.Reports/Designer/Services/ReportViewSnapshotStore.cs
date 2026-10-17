using CrestApps.OrchardCore.Reports.Designer.Indexes;
using CrestApps.OrchardCore.Reports.Designer.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Reads and writes the stored results of scheduled views. Each view's result is its own document, so refreshing one
/// view never rewrites another, and the refresh status is read from the index without loading the rows.
/// </summary>
public sealed class ReportViewSnapshotStore
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportViewSnapshotStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ReportViewSnapshotStore(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Finds the stored result of a view, with its rows.
    /// </summary>
    /// <param name="viewId">The view identifier.</param>
    /// <returns>The snapshot, or <see langword="null"/> when the view has none.</returns>
    public Task<ReportViewSnapshot> FindAsync(string viewId)
    {
        ArgumentException.ThrowIfNullOrEmpty(viewId);

        return _session.Query<ReportViewSnapshot, ReportViewSnapshotIndex>(index => index.ViewId == viewId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Reads the refresh status of a view without loading its rows.
    /// </summary>
    /// <param name="viewId">The view identifier.</param>
    /// <returns>The status, or <see langword="null"/> when the view has no stored result.</returns>
    public async Task<ReportViewSnapshotStatus> GetStatusAsync(string viewId)
    {
        ArgumentException.ThrowIfNullOrEmpty(viewId);

        var index = await _session.QueryIndex<ReportViewSnapshotIndex>(index => index.ViewId == viewId).FirstOrDefaultAsync();

        return ReportViewSnapshotStatus.From(index);
    }

    /// <summary>
    /// Reads the refresh status of every view that has a stored result, without loading the rows.
    /// </summary>
    /// <returns>The statuses, by view identifier.</returns>
    public async Task<IReadOnlyDictionary<string, ReportViewSnapshotStatus>> ListStatusesAsync()
    {
        var statuses = new Dictionary<string, ReportViewSnapshotStatus>(StringComparer.Ordinal);

        foreach (var index in await _session.QueryIndex<ReportViewSnapshotIndex>().ListAsync())
        {
            if (!string.IsNullOrEmpty(index.ViewId))
            {
                statuses[index.ViewId] = ReportViewSnapshotStatus.From(index);
            }
        }

        return statuses;
    }

    /// <summary>
    /// Saves a snapshot and commits it, so the reports that read the view see it at once.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SaveAsync(ReportViewSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await _session.SaveAsync(snapshot, cancellationToken: cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes the stored result of a view.
    /// </summary>
    /// <param name="viewId">The view identifier.</param>
    /// <returns><see langword="true"/> when a stored result was deleted.</returns>
    public async Task<bool> DeleteAsync(string viewId)
    {
        ArgumentException.ThrowIfNullOrEmpty(viewId);

        var deleted = false;

        foreach (var snapshot in await _session.Query<ReportViewSnapshot, ReportViewSnapshotIndex>(index => index.ViewId == viewId).ListAsync())
        {
            _session.Delete(snapshot);
            deleted = true;
        }

        return deleted;
    }
}

/// <summary>
/// The refresh status of a scheduled view, as the builder and the view list show it.
/// </summary>
public sealed class ReportViewSnapshotStatus
{
    /// <summary>
    /// Gets or sets when the stored rows were last refreshed, in UTC, or <see langword="null"/> when no refresh has
    /// succeeded yet.
    /// </summary>
    public DateTime? RefreshedUtc { get; set; }

    /// <summary>
    /// Gets or sets when a refresh was last attempted, in UTC.
    /// </summary>
    public DateTime AttemptedUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of stored rows.
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// Gets or sets the error of the last refresh, or <see langword="null"/> when it succeeded.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets when the last refresh failed, in UTC.
    /// </summary>
    public DateTime? LastErrorUtc { get; set; }

    /// <summary>
    /// Creates the status of an index entry.
    /// </summary>
    /// <param name="index">The index entry.</param>
    /// <returns>The status, or <see langword="null"/> when <paramref name="index"/> is <see langword="null"/>.</returns>
    public static ReportViewSnapshotStatus From(ReportViewSnapshotIndex index)
    {
        return index is null ? null : new ReportViewSnapshotStatus
        {
            RefreshedUtc = AsUtc(index.RefreshedUtc),
            AttemptedUtc = AsUtc(index.AttemptedUtc).Value,
            RowCount = index.RowCount,
            LastError = index.LastError,
            LastErrorUtc = AsUtc(index.LastErrorUtc),
        };
    }

    /// <summary>
    /// Creates the status of a snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The status, or <see langword="null"/> when <paramref name="snapshot"/> is <see langword="null"/>.</returns>
    public static ReportViewSnapshotStatus From(ReportViewSnapshot snapshot)
    {
        return snapshot is null ? null : new ReportViewSnapshotStatus
        {
            RefreshedUtc = AsUtc(snapshot.RefreshedUtc),
            AttemptedUtc = AsUtc(snapshot.AttemptedUtc).Value,
            RowCount = snapshot.RowCount,
            LastError = snapshot.LastError,
            LastErrorUtc = AsUtc(snapshot.LastErrorUtc),
        };
    }

    // Index columns come back from some databases without their kind; they are always stored in UTC.
    private static DateTime? AsUtc(DateTime? value)
    {
        return value is { Kind: not DateTimeKind.Utc } date ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : value;
    }
}
