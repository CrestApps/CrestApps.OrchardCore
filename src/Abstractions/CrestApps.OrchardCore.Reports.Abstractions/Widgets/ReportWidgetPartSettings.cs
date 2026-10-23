namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// The settings of a report dashboard widget type: what a widget of the type shows when its own settings leave a choice
/// open. A module that ships a ready-made widget sets the report here; a type with no report lets whoever adds the
/// widget pick any report they may view.
/// </summary>
public sealed class ReportWidgetPartSettings
{
    /// <summary>
    /// Gets or sets the technical name of the report the widgets of the type show, or <see langword="null"/> to let the
    /// widget choose.
    /// </summary>
    public string ReportName { get; set; }

    /// <summary>
    /// Gets or sets the period a new widget of the type covers.
    /// </summary>
    public ReportWidgetPeriod DefaultPeriod { get; set; } = ReportWidgetPeriod.Last30Days;

    /// <summary>
    /// Gets or sets what a new widget of the type shows of the report.
    /// </summary>
    public ReportWidgetContent DefaultContent { get; set; } = ReportWidgetContent.FiguresAndTables;

    /// <summary>
    /// Gets or sets how many rows of each table a new widget of the type shows.
    /// </summary>
    public int DefaultMaxRows { get; set; } = 5;
}
