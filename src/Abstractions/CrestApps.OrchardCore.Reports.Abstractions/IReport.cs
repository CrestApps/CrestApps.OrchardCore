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
}
