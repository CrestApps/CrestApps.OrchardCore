namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents a single section of a report document. A section renders headline metrics, a table,
/// bars, or a chart, allowing reports to combine summary and detail views uniformly.
/// </summary>
public sealed class ReportSection
{
    /// <summary>
    /// Gets or sets the optional section title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the optional section description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the kind of content the section renders.
    /// </summary>
    public ReportSectionKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the metric cards, when <see cref="Kind"/> is <see cref="ReportSectionKind.Metrics"/>.
    /// </summary>
    public IList<ReportMetric> Metrics { get; set; } = [];

    /// <summary>
    /// Gets or sets the table columns, when <see cref="Kind"/> is <see cref="ReportSectionKind.Table"/>.
    /// </summary>
    public IList<ReportColumn> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the table rows, when <see cref="Kind"/> is <see cref="ReportSectionKind.Table"/>.
    /// </summary>
    public IList<ReportRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the bars, when <see cref="Kind"/> is <see cref="ReportSectionKind.Bars"/>.
    /// </summary>
    public IList<ReportBar> Bars { get; set; } = [];

    /// <summary>
    /// Gets or sets the chart, when <see cref="Kind"/> is <see cref="ReportSectionKind.Chart"/>.
    /// </summary>
    public ReportChart Chart { get; set; }

    /// <summary>
    /// Gets or sets the responsive section width on a twelve-column grid.
    /// </summary>
    public int Width { get; set; } = 12;

    /// <summary>
    /// Creates a metrics section.
    /// </summary>
    /// <param name="title">The section title.</param>
    /// <param name="metrics">The metric cards.</param>
    /// <returns>The metrics section.</returns>
    public static ReportSection ForMetrics(string title, IEnumerable<ReportMetric> metrics)
    {
        return new ReportSection
        {
            Title = title,
            Kind = ReportSectionKind.Metrics,
            Metrics = metrics is null ? [] : [.. metrics],
        };
    }

    /// <summary>
    /// Creates a table section.
    /// </summary>
    /// <param name="title">The section title.</param>
    /// <param name="columns">The table columns.</param>
    /// <param name="rows">The table rows.</param>
    /// <returns>The table section.</returns>
    public static ReportSection ForTable(string title, IEnumerable<ReportColumn> columns, IEnumerable<ReportRow> rows)
    {
        return new ReportSection
        {
            Title = title,
            Kind = ReportSectionKind.Table,
            Columns = columns is null ? [] : [.. columns],
            Rows = rows is null ? [] : [.. rows],
        };
    }

    /// <summary>
    /// Creates a bars section.
    /// </summary>
    /// <param name="title">The section title.</param>
    /// <param name="bars">The bars.</param>
    /// <returns>The bars section.</returns>
    public static ReportSection ForBars(string title, IEnumerable<ReportBar> bars)
    {
        return new ReportSection
        {
            Title = title,
            Kind = ReportSectionKind.Bars,
            Bars = bars is null ? [] : [.. bars],
        };
    }

    /// <summary>
    /// Creates a chart section.
    /// </summary>
    /// <param name="title">The section title.</param>
    /// <param name="chart">The chart configuration and data.</param>
    /// <param name="width">The responsive section width on a twelve-column grid.</param>
    /// <returns>The chart section.</returns>
    public static ReportSection ForChart(string title, ReportChart chart, int width = 6)
    {
        return new ReportSection
        {
            Title = title,
            Kind = ReportSectionKind.Chart,
            Chart = chart,
            Width = Math.Clamp(width, 1, 12),
        };
    }
}
