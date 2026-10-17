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
    /// Decides whether an activity in the supplied status is counted as pending: not finished, and not being worked yet.
    /// It is the same test <see cref="Count(IEnumerable{OmnichannelActivityIndex})"/> applies, so a report that splits the
    /// pending records further splits exactly the records counted as pending.
    /// </summary>
    /// <param name="status">The activity status.</param>
    /// <returns><see langword="true"/> when the activity is pending.</returns>
    public static bool IsPending(ActivityStatus status)
    {
        return status is not (ActivityStatus.Completed
            or ActivityStatus.Failed
            or ActivityStatus.Cancelled
            or ActivityStatus.Purged
            or ActivityStatus.AwaitingAgentResponse
            or ActivityStatus.AwaitingCustomerAnswer
            or ActivityStatus.Reserved
            or ActivityStatus.Dialing
            or ActivityStatus.InProgress);
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
