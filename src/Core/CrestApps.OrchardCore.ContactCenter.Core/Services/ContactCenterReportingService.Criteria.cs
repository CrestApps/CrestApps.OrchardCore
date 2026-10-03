using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

// Resolves report criteria that name a group or an option to the concrete values the report filters match.
public sealed partial class ContactCenterReportingService
{
    /// <summary>
    /// Resolves a selected queue group to queue identifiers using the queues' current catalog membership.
    /// </summary>
    /// <param name="criteria">The optional report criteria to update.</param>
    /// <param name="queues">The current queue catalog.</param>
    public static void ApplyCurrentQueueGroupCriteria(
        ContactCenterReportCriteria criteria,
        IReadOnlyList<ActivityQueue> queues)
    {
        if (string.IsNullOrEmpty(criteria?.QueueGroupId))
        {
            return;
        }

        criteria.QueueIds = queues
            .Where(queue => string.Equals(queue.QueueGroupId, criteria.QueueGroupId, StringComparison.Ordinal))
            .Select(queue => queue.ItemId)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves a selected activity source to the stored source values it matches, such as every dialer mode for
    /// the Dialer source.
    /// </summary>
    /// <param name="criteria">The optional report criteria to update.</param>
    /// <param name="activitySourceOptions">The registered activity sources.</param>
    public static void ApplyActivitySourceCriteria(
        ContactCenterReportCriteria criteria,
        ActivitySourceOptions activitySourceOptions)
    {
        ArgumentNullException.ThrowIfNull(activitySourceOptions);

        if (string.IsNullOrEmpty(criteria?.ActivitySource))
        {
            return;
        }

        criteria.ActivitySources = activitySourceOptions.GetStoredValues(criteria.ActivitySource)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    internal static void ApplyCurrentCampaignGroupCriteria(
        ContactCenterReportCriteria criteria,
        IEnumerable<OmnichannelCampaign> campaigns)
    {
        if (string.IsNullOrEmpty(criteria?.CampaignGroupId))
        {
            return;
        }

        criteria.CampaignIds = campaigns
            .Where(campaign => campaign.CampaignGroupId == criteria.CampaignGroupId)
            .Select(campaign => campaign.ItemId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
