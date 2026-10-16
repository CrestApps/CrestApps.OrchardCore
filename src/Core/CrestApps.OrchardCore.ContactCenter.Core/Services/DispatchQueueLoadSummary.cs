using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// A pending campaign activity as the diagnostics read it from the activity index.
/// </summary>
/// <param name="ActivityId">The activity identifier.</param>
/// <param name="Source">The activity source.</param>
/// <param name="InteractionType">The interaction type name.</param>
/// <param name="Channel">The channel.</param>
/// <param name="DialerProfileId">The dialer profile the activity itself carries, if any.</param>
internal readonly record struct PendingRecord(
    string ActivityId,
    string Source,
    string InteractionType,
    string Channel,
    string DialerProfileId);

/// <summary>
/// Summarizes what is loaded in a campaign's queue: which dialer profiles its items are stamped with, what kind of
/// activities are pending, and where the two disagree.
/// </summary>
internal static class DispatchQueueLoadSummary
{
    /// <summary>
    /// Groups the dialer profiles stamped on the waiting items.
    /// </summary>
    /// <param name="waiting">The waiting queue items.</param>
    /// <param name="findProfile">Reads a profile by identifier, or returns <see langword="null"/> when it cannot be read.</param>
    /// <returns>One entry per stamped profile, the most-stamped first.</returns>
    public static IList<DispatchProfileStamp> Stamps(IEnumerable<QueueItem> waiting, Func<string, DialerProfile> findProfile)
    {
        ArgumentNullException.ThrowIfNull(waiting);
        ArgumentNullException.ThrowIfNull(findProfile);

        return waiting
            .GroupBy(item => string.IsNullOrEmpty(item.DialerProfileId) ? string.Empty : item.DialerProfileId, StringComparer.Ordinal)
            .Select(group =>
            {
                var stamp = new DispatchProfileStamp
                {
                    ProfileId = group.Key.Length == 0 ? null : group.Key,
                    ItemCount = group.Count(),
                };

                if (group.Key.Length > 0 && !QueueCallbackDialerProfile.IsCallbackProfile(group.Key) && findProfile(group.Key) is { } profile)
                {
                    stamp.ProfileName = profile.Name;
                    stamp.Mode = profile.Mode.ToString();
                    stamp.Enabled = profile.Enabled;
                }

                return stamp;
            })
            .OrderByDescending(stamp => stamp.ItemCount)
            .ThenBy(stamp => stamp.ProfileId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Groups the pending activities by source, interaction type and channel.
    /// </summary>
    /// <param name="records">The pending activities.</param>
    /// <returns>One entry per combination, the largest first.</returns>
    public static IList<DispatchActivityGroup> Mix(IEnumerable<PendingRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records
            .GroupBy(record => (record.Source ?? string.Empty, record.InteractionType ?? string.Empty, record.Channel ?? string.Empty))
            .Select(group => new DispatchActivityGroup
            {
                Source = group.Key.Item1,
                InteractionType = group.Key.Item2,
                Channel = group.Key.Item3,
                Count = group.Count(),
            })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Source, StringComparer.Ordinal)
            .ThenBy(group => group.Channel, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Counts where a waiting item's stamp disagrees with its activity's own dialer profile, and the activities that carry
    /// none.
    /// </summary>
    /// <param name="waitingByActivity">The waiting items, by the activity they carry.</param>
    /// <param name="records">The pending activities.</param>
    /// <returns>How many waiting items are stamped differently from their activity, and how many pending activities carry no
    /// profile.</returns>
    public static (int Mismatched, int WithoutProfile) Compare(
        IReadOnlyDictionary<string, QueueItem> waitingByActivity,
        IEnumerable<PendingRecord> records)
    {
        ArgumentNullException.ThrowIfNull(waitingByActivity);
        ArgumentNullException.ThrowIfNull(records);

        var mismatched = 0;
        var withoutProfile = 0;

        foreach (var record in records)
        {
            var own = record.DialerProfileId ?? string.Empty;

            if (own.Length == 0)
            {
                withoutProfile++;
            }

            if (waitingByActivity.TryGetValue(record.ActivityId, out var item) &&
                !string.Equals(item.DialerProfileId ?? string.Empty, own, StringComparison.Ordinal))
            {
                mismatched++;
            }
        }

        return (mismatched, withoutProfile);
    }

    /// <summary>
    /// Decides how the queue is paced from the profile stamped on its head item, the way the pacer does.
    /// </summary>
    /// <param name="head">The next waiting item, or <see langword="null"/> when none waits.</param>
    /// <param name="headProfile">The profile stamped on the head item, or <see langword="null"/> when it cannot be read.</param>
    /// <returns>How the queue is paced.</returns>
    public static DispatchPacing Pacing(QueueItem head, DialerProfile headProfile)
    {
        if (head is null)
        {
            return DispatchPacing.NothingWaiting;
        }

        if (QueueCallbackDialerProfile.IsCallbackProfile(head.DialerProfileId))
        {
            return DispatchPacing.AgentDriven;
        }

        if (headProfile is null)
        {
            return DispatchPacing.ProfileMissing;
        }

        if (!headProfile.Enabled)
        {
            return DispatchPacing.ProfileDisabled;
        }

        // An over-dialing Predictive profile is paced by its own scheduler, so it is paced too.
        return DialerPacingScheduler.IsPacedHere(headProfile) || headProfile.Mode == DialerMode.Predictive
            ? DispatchPacing.Paced
            : DispatchPacing.AgentDriven;
    }
}
