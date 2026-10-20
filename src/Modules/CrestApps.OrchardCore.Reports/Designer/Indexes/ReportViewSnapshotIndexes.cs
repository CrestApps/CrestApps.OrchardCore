using CrestApps.OrchardCore.Reports.Designer.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Reports.Designer.Indexes;

/// <summary>
/// Finds the stored result of a view, and tells when it was refreshed without loading its rows.
/// </summary>
public sealed class ReportViewSnapshotIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the identifier of the view.
    /// </summary>
    public string ViewId { get; set; }

    /// <summary>
    /// Gets or sets when the rows were last refreshed successfully, in UTC.
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
    /// Gets or sets the error of the last refresh, shortened, or <see langword="null"/> when it succeeded.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets when the last refresh failed, in UTC.
    /// </summary>
    public DateTime? LastErrorUtc { get; set; }
}

/// <summary>
/// Maps view snapshots to <see cref="ReportViewSnapshotIndex"/>.
/// </summary>
public sealed class ReportViewSnapshotIndexProvider : IndexProvider<ReportViewSnapshot>
{
    /// <summary>
    /// The longest error kept in the index.
    /// </summary>
    public const int MaxErrorLength = 500;

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ReportViewSnapshot> context)
    {
        context
            .For<ReportViewSnapshotIndex>()
            .Map(snapshot => new ReportViewSnapshotIndex
            {
                ViewId = snapshot.ViewId,
                RefreshedUtc = snapshot.RefreshedUtc,
                AttemptedUtc = snapshot.AttemptedUtc,
                RowCount = snapshot.RowCount,
                LastError = snapshot.LastError is { Length: > MaxErrorLength } error ? error[..MaxErrorLength] : snapshot.LastError,
                LastErrorUtc = snapshot.LastErrorUtc,
            });
    }
}
