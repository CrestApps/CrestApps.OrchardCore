using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports.Widgets.Services;

/// <summary>
/// Cuts a report document down to what fits a dashboard widget.
/// </summary>
public static class ReportWidgetTrimmer
{
    /// <summary>
    /// Keeps the sections the widget shows, and the first rows of each table.
    /// </summary>
    /// <param name="document">The document the report produced; it is changed in place.</param>
    /// <param name="content">What of the report the widget shows.</param>
    /// <param name="maxRows">How many detail rows of each table to keep, or 0 for all of them.</param>
    /// <returns>The document.</returns>
    public static ReportDocument Trim(ReportDocument document, ReportWidgetContent content, int maxRows)
    {
        ArgumentNullException.ThrowIfNull(document);

        var kept = document.Sections.Where(section => Shows(content, section.Kind)).ToList();

        foreach (var section in kept.Where(section => section.Kind == ReportSectionKind.Table && maxRows > 0))
        {
            section.Rows = section.Rows
                .Where(row => row.Kind == ReportRowKind.Detail)
                .Take(maxRows)
                .Concat(section.Rows.Where(row => row.Kind == ReportRowKind.GrandTotal))
                .ToList();
        }

        foreach (var section in kept.Where(section => section.Kind == ReportSectionKind.Bars && maxRows > 0))
        {
            section.Bars = [.. section.Bars.Take(maxRows)];
        }

        document.Sections = kept;

        return document;
    }

    private static bool Shows(ReportWidgetContent content, ReportSectionKind kind)
        => content switch
        {
            ReportWidgetContent.FiguresOnly => kind is ReportSectionKind.Metrics or ReportSectionKind.Bars,
            ReportWidgetContent.TablesOnly => kind == ReportSectionKind.Table,
            ReportWidgetContent.ChartsOnly => kind == ReportSectionKind.Chart,
            ReportWidgetContent.Everything => true,
            _ => kind != ReportSectionKind.Chart,
        };
}
