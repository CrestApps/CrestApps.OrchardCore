using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// Provides the shared category, permission, and dependencies for the Contact Center reports contributed
/// to the admin Reports area.
/// </summary>
public abstract class ContactCenterReportBase : IReport, IReportFilterMetadata, IContactCenterCapabilityDependentReport
{
    private readonly IContactCenterReportCapabilityGuard _capabilityGuard;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterReportBase"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service used to aggregate the data.</param>
    /// <param name="capabilityGuard">The guard that decides whether the producing capabilities are enabled.</param>
    /// <param name="stringLocalizer">The string localizer used for the report labels.</param>
    protected ContactCenterReportBase(
        IContactCenterReportingService reportingService,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IStringLocalizer stringLocalizer)
    {
        ReportingService = reportingService;
        _capabilityGuard = capabilityGuard;
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
    public abstract IReadOnlyCollection<string> RequiredFeatureIds { get; }

    /// <inheritdoc/>
    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var missing = await _capabilityGuard.GetMissingFeaturesAsync(RequiredFeatureIds, cancellationToken);

        if (missing.Count > 0)
        {
            return _capabilityGuard.DescribeUnavailable(missing);
        }

        return await RunCoreAsync(context, cancellationToken);
    }

    /// <summary>
    /// Runs the report once its producing capabilities are known to be enabled.
    /// </summary>
    /// <param name="context">The report context, including the resolved period and filter.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The report document to render and export.</returns>
    protected abstract Task<ReportDocument> RunCoreAsync(ReportContext context, CancellationToken cancellationToken = default);
}
