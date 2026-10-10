using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// One visual of a designed report: a table, a chart, a row of headline metrics, or a pivot table, each drawn from the
/// result columns of the report query.
/// </summary>
public sealed class ReportVisualDefinition
{
    /// <summary>
    /// Gets or sets the identifier of the visual, unique within the report.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the kind of visual.
    /// </summary>
    public ReportVisualType Type { get; set; }

    /// <summary>
    /// Gets or sets the optional title shown above the visual.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the width of the visual on a twelve-column grid.
    /// </summary>
    public int Width { get; set; } = 12;

    /// <summary>
    /// Gets or sets the chart type, when <see cref="Type"/> is <see cref="ReportVisualType.Chart"/>.
    /// </summary>
    public ReportChartType ChartType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the column that labels a chart's categories.
    /// </summary>
    public string CategoryColumnId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of a dimension column whose values become a chart's series or a pivot table's
    /// columns.
    /// </summary>
    public string SeriesColumnId { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of the measure columns a chart plots, a metrics row shows, or a pivot table sums.
    /// </summary>
    public IList<string> ValueColumnIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifiers of the columns a table shows, in order (all visible columns when empty), or the
    /// row columns of a pivot table.
    /// </summary>
    public IList<string> ColumnIds { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether a table or pivot table ends with a totals row.
    /// </summary>
    public bool ShowTotals { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a table of a grouped report shows a subtotal row after each group of its
    /// leading dimensions: with dimensions Role, User and Status, a subtotal per user within each role, then per role.
    /// Rows are kept together by those dimensions, in their order within each group.
    /// </summary>
    public bool ShowSubtotals { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a bar or area chart stacks its series.
    /// </summary>
    public bool Stacked { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a chart shows its legend.
    /// </summary>
    public bool ShowLegend { get; set; } = true;
}
