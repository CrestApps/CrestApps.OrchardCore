using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// A report surfaced under the admin Reports area. A module contributes one by implementing this interface with the
/// static members that describe the report and registering it with <c>services.AddReport&lt;TReport&gt;()</c>; the
/// Reports framework lists it from its descriptor without creating it, and handles navigation, filtering, rendering
/// and export uniformly.
/// </summary>
public interface IReport : IReportRunner
{
    /// <summary>
    /// Gets the stable technical name of the report, used to resolve and route to it.
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Gets the label shown to users.
    /// </summary>
    static abstract LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets the description shown to users.
    /// </summary>
    static abstract LocalizationSource Description { get; }

    /// <summary>
    /// Gets the category the report is grouped under in the admin navigation, one of
    /// <see cref="ReportsConstants.Categories"/>.
    /// </summary>
    static abstract string Category { get; }

    /// <summary>
    /// Gets the permission required to view and export the report.
    /// </summary>
    static abstract Permission Permission { get; }

    /// <summary>
    /// Gets the permission required, beside <see cref="Permission"/>, to export the report, or
    /// <see langword="null"/> when <see cref="Permission"/> is enough. The report page hides its export buttons from a
    /// person without it, the export refuses them, and a report subscription cannot attach the export for them.
    /// </summary>
    static virtual Permission ExportPermission => null;

    /// <summary>
    /// Gets the date range the report starts on before a person picks one, or <see langword="null"/> for today. A
    /// report that declares one also accepts open-ended ranges; see <see cref="ReportDefaultDateRange"/>.
    /// </summary>
    static virtual ReportDefaultDateRange DefaultDateRange => null;
}
