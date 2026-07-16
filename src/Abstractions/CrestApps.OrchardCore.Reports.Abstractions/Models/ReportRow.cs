namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents a single row of a report table section. Cell values are pre-formatted strings so the
/// renderer and exporter stay format-agnostic.
/// </summary>
public sealed class ReportRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportRow"/> class.
    /// </summary>
    public ReportRow()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportRow"/> class.
    /// </summary>
    /// <param name="cells">The pre-formatted cell values.</param>
    /// <param name="emphasize">Whether the row should be visually emphasized (for example, a totals row).</param>
    public ReportRow(IList<string> cells, bool emphasize = false)
    {
        Cells = cells;
        Kind = emphasize
            ? ReportRowKind.GrandTotal
            : ReportRowKind.Detail;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportRow"/> class.
    /// </summary>
    /// <param name="cells">The pre-formatted cell values.</param>
    /// <param name="kind">The semantic purpose of the row.</param>
    public ReportRow(IList<string> cells, ReportRowKind kind)
    {
        Cells = cells;
        Kind = kind;
    }

    /// <summary>
    /// Gets or sets the pre-formatted cell values, one per column.
    /// </summary>
    public IList<string> Cells { get; set; } = [];

    /// <summary>
    /// Gets or sets the semantic purpose of the row.
    /// </summary>
    public ReportRowKind Kind { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the row should be visually emphasized.
    /// </summary>
    public bool Emphasize
    {
        get
        {
            return Kind != ReportRowKind.Detail;
        }
        set
        {
            Kind = value
                ? ReportRowKind.GrandTotal
                : ReportRowKind.Detail;
        }
    }
}
