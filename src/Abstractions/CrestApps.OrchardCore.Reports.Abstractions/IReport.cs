using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Defines a report that can be surfaced under the admin Reports area. A module contributes a report by
/// registering an implementation of this interface; the Reports framework handles navigation, filtering,
/// rendering, and export uniformly.
/// </summary>
public interface IReport
{
    /// <summary>
    /// Gets the stable, unique technical name used to resolve and route to the report.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the localized, human-readable name of the report.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets the localized description of what the report shows.
    /// </summary>
    LocalizedString Description { get; }

    /// <summary>
    /// Gets the category the report is grouped under in the admin navigation.
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Gets the permission required to view and export the report.
    /// </summary>
    Permission Permission { get; }

    /// <summary>
    /// Runs the report for the supplied filter and returns the resulting document.
    /// </summary>
    /// <param name="context">The report context, including the resolved period and filter.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The report document to render and export.</returns>
    Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default);
}
