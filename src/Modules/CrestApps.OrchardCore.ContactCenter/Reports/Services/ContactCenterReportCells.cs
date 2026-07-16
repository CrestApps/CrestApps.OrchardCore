using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Builds the shared columns and cells used by the campaign summary and subject inventory reports so
/// their completed-versus-pending progress tables stay consistent.
/// </summary>
internal static class ContactCenterReportCells
{
    /// <summary>
    /// Builds the progress table columns.
    /// </summary>
    /// <param name="localizer">The string localizer.</param>
    /// <param name="firstColumnLabel">The label of the first (grouping) column.</param>
    /// <returns>The progress columns.</returns>
    public static ReportColumn[] ProgressColumns(IStringLocalizer localizer, string firstColumnLabel)
    {
        return
        [
            new ReportColumn(firstColumnLabel),
            new ReportColumn(localizer["Total"].Value, ReportColumnAlign.End),
            new ReportColumn(localizer["Completed"].Value, ReportColumnAlign.End),
            new ReportColumn(localizer["Pending"].Value, ReportColumnAlign.End),
            new ReportColumn(localizer["In progress"].Value, ReportColumnAlign.End),
            new ReportColumn(localizer["Failed"].Value, ReportColumnAlign.End),
            new ReportColumn(localizer["Cancelled"].Value, ReportColumnAlign.End),
            new ReportColumn(localizer["Attempts"].Value, ReportColumnAlign.End),
            new ReportColumn(localizer["Completion"].Value, ReportColumnAlign.End),
        ];
    }

    /// <summary>
    /// Builds the progress cells for a single group.
    /// </summary>
    /// <param name="label">The group label.</param>
    /// <param name="counts">The progress counts.</param>
    /// <returns>The formatted cell values.</returns>
    public static string[] Progress(string label, ActivityProgressCounts counts)
    {
        return
        [
            label,
            ReportFormat.Number(counts.Total),
            ReportFormat.Number(counts.Completed),
            ReportFormat.Number(counts.Pending),
            ReportFormat.Number(counts.InProgress),
            ReportFormat.Number(counts.Failed),
            ReportFormat.Number(counts.Cancelled),
            ReportFormat.Number(counts.TotalAttempts),
            ReportFormat.Percent(counts.CompletionRate),
        ];
    }

    /// <summary>
    /// Builds a progress row with explicit semantic row-kind metadata.
    /// </summary>
    /// <param name="label">The group label.</param>
    /// <param name="counts">The progress counts.</param>
    /// <param name="kind">The semantic purpose of the row.</param>
    /// <returns>The report row.</returns>
    public static ReportRow ProgressRow(
        string label,
        ActivityProgressCounts counts,
        ReportRowKind kind = ReportRowKind.Detail)
    {
        return new ReportRow(Progress(label, counts), kind);
    }
}
