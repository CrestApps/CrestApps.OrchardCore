using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Decides whether a report's producing capabilities are present and, when they are not, produces the document that
/// says so.
/// </summary>
public interface IContactCenterReportCapabilityGuard
{
    /// <summary>
    /// Reports which of the supplied features are not enabled in the current tenant.
    /// </summary>
    /// <param name="requiredFeatureIds">The features that write the data the report reads.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The identifiers of the required features the tenant has not enabled, in the order supplied.</returns>
    ValueTask<IReadOnlyCollection<string>> GetMissingFeaturesAsync(
        IReadOnlyCollection<string> requiredFeatureIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the document a report returns when the capability that produces its data is absent.
    /// </summary>
    /// <param name="missingFeatureIds">The required features the tenant has not enabled.</param>
    /// <returns>A document that names the missing capabilities and carries no measurements.</returns>
    ReportDocument DescribeUnavailable(IReadOnlyCollection<string> missingFeatureIds);
}
