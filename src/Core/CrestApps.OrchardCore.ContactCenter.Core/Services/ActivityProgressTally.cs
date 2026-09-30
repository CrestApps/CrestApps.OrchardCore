using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Counts a set of CRM activities into the progress breakdown the campaign summary and subject inventory reports show.
/// </summary>
internal static class ActivityProgressTally
{
    /// <summary>
    /// Counts the activities by where they are in their life, and how many ended because the number is not in service.
    /// </summary>
    /// <param name="activities">The activities to count.</param>
    public static ActivityProgressCounts Count(IEnumerable<OmnichannelActivityIndex> activities)
    {
        var counts = new ActivityProgressCounts();

        foreach (var activity in activities)
        {
            counts.Total++;
            counts.TotalAttempts += Math.Max(0, activity.Attempts);

            if (string.Equals(activity.TerminalReasonCode, OmnichannelConstants.TerminalReasons.NumberNotInService, StringComparison.Ordinal))
            {
                counts.NotInService++;
            }

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

    /// <summary>
    /// Adds one group's counts to the running totals.
    /// </summary>
    /// <param name="totals">The running totals.</param>
    /// <param name="counts">The group's counts.</param>
    public static void Add(ActivityProgressCounts totals, ActivityProgressCounts counts)
    {
        totals.Total += counts.Total;
        totals.Completed += counts.Completed;
        totals.Pending += counts.Pending;
        totals.InProgress += counts.InProgress;
        totals.Failed += counts.Failed;
        totals.Cancelled += counts.Cancelled;
        totals.TotalAttempts += counts.TotalAttempts;
        totals.NotInService += counts.NotInService;
    }
}
