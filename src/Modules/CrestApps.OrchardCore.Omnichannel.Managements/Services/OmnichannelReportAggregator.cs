using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models.Reports;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Provides pure aggregation over CRM activity index rows for the Omnichannel reports. Keeping the
/// aggregation separate from the queries makes it unit-testable without a live session.
/// </summary>
internal static class OmnichannelReportAggregator
{
    /// <summary>
    /// Builds the activity summary from the activities created in the reporting period.
    /// </summary>
    /// <param name="activities">The activities created in the period.</param>
    /// <returns>The aggregated activity summary.</returns>
    public static OmnichannelActivitySummaryData BuildActivitySummary(IReadOnlyList<OmnichannelActivityIndex> activities)
    {
        var data = new OmnichannelActivitySummaryData
        {
            Counts = BuildCounts(activities),
        };

        var bySource = new Dictionary<string, long>(StringComparer.Ordinal);
        var byChannel = new Dictionary<string, long>(StringComparer.Ordinal);
        var byStatus = new Dictionary<string, long>(StringComparer.Ordinal);
        var daily = new Dictionary<DateOnly, OmnichannelActivityDailyPoint>();

        foreach (var activity in activities)
        {
            var source = string.IsNullOrEmpty(activity.Source) ? ActivitySources.Manual : activity.Source;
            bySource[source] = bySource.GetValueOrDefault(source) + 1;

            var channel = string.IsNullOrEmpty(activity.Channel) ? "—" : activity.Channel;
            byChannel[channel] = byChannel.GetValueOrDefault(channel) + 1;

            var status = activity.Status.ToString();
            byStatus[status] = byStatus.GetValueOrDefault(status) + 1;

            var day = DateOnly.FromDateTime(activity.CreatedUtc);

            if (!daily.TryGetValue(day, out var point))
            {
                point = new OmnichannelActivityDailyPoint { Date = day };
                daily[day] = point;
            }

            point.Count++;
        }

        data.BySource = bySource;
        data.ByChannel = byChannel;
        data.ByStatus = byStatus;
        data.Daily = daily.Values.OrderBy(point => point.Date).ToList();

        return data;
    }

    /// <summary>
    /// Builds the per-campaign performance from the activities created in the reporting period.
    /// </summary>
    /// <param name="activities">The activities created in the period.</param>
    /// <returns>The aggregated campaign performance.</returns>
    public static OmnichannelCampaignPerformanceData BuildCampaignPerformance(IReadOnlyList<OmnichannelActivityIndex> activities)
    {
        var data = new OmnichannelCampaignPerformanceData();

        foreach (var group in activities.GroupBy(activity => activity.CampaignId ?? string.Empty, StringComparer.Ordinal))
        {
            var counts = BuildCounts(group.ToArray());

            data.Rows.Add(new OmnichannelCampaignRow
            {
                CampaignId = group.Key,
                Counts = counts,
            });

            Accumulate(data.Totals, counts);
        }

        data.Rows = data.Rows.OrderByDescending(row => row.Counts.Total).ToList();

        return data;
    }

    /// <summary>
    /// Builds campaign performance aggregated by campaign group.
    /// </summary>
    /// <param name="activities">The activities created in the period.</param>
    /// <param name="campaignGroupIds">The campaign-to-group mapping.</param>
    /// <returns>The aggregated campaign-group performance.</returns>
    public static OmnichannelCampaignGroupPerformanceData BuildCampaignGroupPerformance(
        IReadOnlyList<OmnichannelActivityIndex> activities,
        IReadOnlyDictionary<string, string> campaignGroupIds)
    {
        var data = new OmnichannelCampaignGroupPerformanceData();

        foreach (var group in activities.GroupBy(
            activity => ResolveCampaignGroupId(activity.CampaignId, campaignGroupIds),
            StringComparer.Ordinal))
        {
            var counts = BuildCounts(group.ToArray());

            data.Rows.Add(new OmnichannelCampaignGroupRow
            {
                CampaignGroupId = group.Key,
                Counts = counts,
            });

            Accumulate(data.Totals, counts);
        }

        data.Rows = data.Rows.OrderByDescending(row => row.Counts.Total).ToList();

        return data;
    }

    /// <summary>
    /// Counts the completed activities grouped by disposition.
    /// </summary>
    /// <param name="completedActivities">The activities completed in the period.</param>
    /// <returns>A dictionary of disposition identifier to completed count.</returns>
    public static IReadOnlyDictionary<string, long> CountByDisposition(IReadOnlyList<OmnichannelActivityIndex> completedActivities)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var activity in completedActivities)
        {
            var disposition = string.IsNullOrEmpty(activity.DispositionId) ? string.Empty : activity.DispositionId;
            counts[disposition] = counts.GetValueOrDefault(disposition) + 1;
        }

        return counts;
    }

    private static OmnichannelProgressCounts BuildCounts(IReadOnlyList<OmnichannelActivityIndex> activities)
    {
        var counts = new OmnichannelProgressCounts();

        foreach (var activity in activities)
        {
            counts.Total++;

            switch (activity.Status)
            {
                case ActivityStatus.Completed:
                    counts.Completed++;

                    break;
                case ActivityStatus.Failed:
                    counts.Failed++;

                    break;
                case ActivityStatus.Cancelled:
                case ActivityStatus.Purged:
                    counts.Cancelled++;

                    break;
                case ActivityStatus.AwaitingAgentResponse:
                case ActivityStatus.AwaitingCustomerAnswer:
                case ActivityStatus.Reserved:
                case ActivityStatus.Dialing:
                case ActivityStatus.InProgress:
                    counts.InProgress++;

                    break;
                default:
                    counts.Pending++;

                    break;
            }
        }

        return counts;
    }

    private static void Accumulate(OmnichannelProgressCounts totals, OmnichannelProgressCounts counts)
    {
        totals.Total += counts.Total;
        totals.Completed += counts.Completed;
        totals.Pending += counts.Pending;
        totals.InProgress += counts.InProgress;
        totals.Failed += counts.Failed;
        totals.Cancelled += counts.Cancelled;
    }

    private static string ResolveCampaignGroupId(
        string campaignId,
        IReadOnlyDictionary<string, string> campaignGroupIds)
    {
        return !string.IsNullOrEmpty(campaignId) &&
            campaignGroupIds.TryGetValue(campaignId, out var campaignGroupId)
            ? campaignGroupId ?? string.Empty
            : string.Empty;
    }
}
