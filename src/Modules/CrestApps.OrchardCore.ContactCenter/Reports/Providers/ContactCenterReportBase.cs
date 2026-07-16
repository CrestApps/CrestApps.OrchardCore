using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// Provides the shared category, permission, and dependencies for the Contact Center reports contributed
/// to the admin Reports area.
/// </summary>
public abstract class ContactCenterReportBase : IReport, IReportFilterMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterReportBase"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service used to aggregate the data.</param>
    /// <param name="stringLocalizer">The string localizer used for the report labels.</param>
    protected ContactCenterReportBase(
        IContactCenterReportingService reportingService,
        IStringLocalizer stringLocalizer)
    {
        ReportingService = reportingService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the Contact Center reporting service used to aggregate the data.
    /// </summary>
    protected IContactCenterReportingService ReportingService { get; }

    /// <summary>
    /// Gets the string localizer used for the report labels.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract LocalizedString DisplayName { get; }

    /// <inheritdoc/>
    public abstract LocalizedString Description { get; }

    /// <inheritdoc/>
    public virtual string Category => ReportsConstants.Categories.Operations;

    /// <inheritdoc/>
    public Permission Permission => ContactCenterPermissions.ViewReports;

    /// <inheritdoc/>
    public abstract IReadOnlyCollection<string> FilterNames { get; }

    /// <inheritdoc/>
    public abstract Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default);
}
