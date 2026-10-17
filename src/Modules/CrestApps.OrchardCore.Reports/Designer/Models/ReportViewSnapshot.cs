using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.Reports.Designer.Models;

/// <summary>
/// The stored result of a scheduled <see cref="ReportView"/>: the rows the view returned at its last successful
/// refresh, which the reports that read the view use instead of running it. A failed refresh keeps the previous rows
/// and records the error.
/// </summary>
public sealed class ReportViewSnapshot
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the view.
    /// </summary>
    public string ViewId { get; set; }

    /// <summary>
    /// Gets or sets when the rows were last refreshed successfully, in UTC, or <see langword="null"/> when no refresh
    /// has succeeded yet.
    /// </summary>
    public DateTime? RefreshedUtc { get; set; }

    /// <summary>
    /// Gets or sets when a refresh was last attempted, in UTC, whether it succeeded or not.
    /// </summary>
    public DateTime AttemptedUtc { get; set; }

    /// <summary>
    /// Gets or sets how long the last successful refresh took, in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the fields of the stored rows, in row order.
    /// </summary>
    public IList<ReportViewSnapshotField> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets the stored rows: one JSON array per row, with one value per field.
    /// </summary>
    public JsonArray Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of stored rows.
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the view returned more rows than were stored.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Gets or sets the warnings of the last successful refresh.
    /// </summary>
    public IList<string> Warnings { get; set; } = [];

    /// <summary>
    /// Gets or sets the error of the last refresh, or <see langword="null"/> when it succeeded.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets when the last refresh failed, in UTC, or <see langword="null"/> when it succeeded.
    /// </summary>
    public DateTime? LastErrorUtc { get; set; }
}

/// <summary>
/// A field of the rows a <see cref="ReportViewSnapshot"/> stores.
/// </summary>
public sealed class ReportViewSnapshotField
{
    /// <summary>
    /// Gets or sets the field name: the identifier of the view's result column.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the field label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the data type of the field's values.
    /// </summary>
    public ReportDataType DataType { get; set; }
}
