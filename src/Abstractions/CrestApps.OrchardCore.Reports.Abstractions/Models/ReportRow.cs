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
    /// Gets or sets the optional style applied to every cell in the row (for example to color-code a
    /// subtotal or grand-total row). A per-cell style set through <see cref="CellStyles"/> takes
    /// precedence. Ignored by export formats that do not support styling.
    /// </summary>
    public ReportStyle Style { get; set; }

    /// <summary>
    /// Gets or sets the optional per-cell styles, aligned by index with <see cref="Cells"/>. An entry may
    /// be <see langword="null"/> to fall back to the row-level <see cref="Style"/>. Ignored by export
    /// formats that do not support styling.
    /// </summary>
    public IList<ReportStyle> CellStyles { get; set; } = [];

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

    /// <summary>
    /// Resolves the effective style for the cell at the supplied index, preferring a per-cell style over
    /// the row-level <see cref="Style"/>.
    /// </summary>
    /// <param name="index">The zero-based cell index.</param>
    /// <returns>The effective style, or <see langword="null"/> when the cell has no style.</returns>
    public ReportStyle GetCellStyle(int index)
    {
        if (CellStyles is not null && index >= 0 && index < CellStyles.Count && CellStyles[index] is not null)
        {
            return CellStyles[index];
        }

        return Style;
    }

    /// <summary>
    /// Sets the row-level style applied to every cell that has no per-cell override, and returns the same
    /// row for chaining.
    /// </summary>
    /// <param name="style">The style to apply to the row.</param>
    /// <returns>The current row.</returns>
    public ReportRow WithStyle(ReportStyle style)
    {
        Style = style;

        return this;
    }

    /// <summary>
    /// Sets the style for a single cell, aligned by index with <see cref="Cells"/>, and returns the same
    /// row for chaining.
    /// </summary>
    /// <param name="index">The zero-based cell index.</param>
    /// <param name="style">The style to apply to the cell.</param>
    /// <returns>The current row.</returns>
    public ReportRow WithCellStyle(int index, ReportStyle style)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        CellStyles ??= [];

        while (CellStyles.Count <= index)
        {
            CellStyles.Add(null);
        }

        CellStyles[index] = style;

        return this;
    }
}
