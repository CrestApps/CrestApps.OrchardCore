using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Counts the billable labor of the work orders done under an agreement against its hours: each completed work order
/// that names the agreement, its labor lines' used hours, at the time it was completed.
/// </summary>
internal sealed class WorkOrderAgreementUsageProvider : IAgreementUsageProvider
{
    private readonly ISession _session;

    public WorkOrderAgreementUsageProvider(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public string Name => "WorkOrders";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AgreementUsage>> ListAsync(Agreement agreement, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        if (string.IsNullOrEmpty(agreement.AccountId))
        {
            return [];
        }

        var accountId = agreement.AccountId;
        var workOrders = await _session.Query<WorkOrder, WorkOrderIndex>(index => index.AccountId == accountId, collection: WorkOrdersConstants.CollectionName).ListAsync(cancellationToken);

        return [.. workOrders
            .Where(workOrder => workOrder.AgreementId == agreement.ItemId && workOrder.CompletedUtc is { } completed && completed >= fromUtc && completed < toUtc)
            .Select(workOrder => new AgreementUsage
            {
                Source = Name,
                SourceRef = "WorkOrder:" + workOrder.ItemId,
                Description = string.IsNullOrEmpty(workOrder.Summary) ? workOrder.Number : workOrder.Number + " " + workOrder.Summary,
                StartUtc = workOrder.CompletedUtc.Value,
                Minutes = (double)(workOrder.Lines ?? []).Where(line => line.Kind == WorkOrderLineKind.Labor && line.Billable).Sum(line => line.QuantityUsed) * 60,
            })
            .Where(usage => usage.Minutes > 0)];
    }
}
