using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Keeps a campaign's waiting dialer records on a single dialer profile.
/// </summary>
/// <remarks>
/// Every record queued for a campaign waits in the campaign's one dialer queue, and that queue is worked under the
/// profile of the record at its head: the pacer dials only when that profile is automated, and agents are offered
/// records only when it is agent-driven. Records queued under another profile behind it were never dialed, or were
/// dialed with the head's settings, so a load or a profile change that would mix profiles is refused instead.
/// </remarks>
internal static class DialerCampaignProfileGuard
{
    /// <summary>
    /// Finds the other dialer profiles the campaign's waiting records are queued under.
    /// </summary>
    /// <param name="dialerContributor">The dialer contributor that reads the campaign's queue.</param>
    /// <param name="campaignId">The campaign identifier.</param>
    /// <param name="profileId">The dialer profile the records are about to be queued under.</param>
    /// <param name="movingActivityIds">The activities that are being moved to <paramref name="profileId"/>, which
    /// therefore do not count against it; <see langword="null"/> when none are.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Each other profile's display name and how many waiting records it holds.</returns>
    public static async Task<IReadOnlyList<DialerCampaignProfileConflict>> FindConflictsAsync(
        IActivityDialerContributor dialerContributor,
        string campaignId,
        string profileId,
        IReadOnlySet<string> movingActivityIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dialerContributor);

        if (string.IsNullOrWhiteSpace(campaignId) || string.IsNullOrWhiteSpace(profileId))
        {
            return [];
        }

        var waiting = await dialerContributor.GetWaitingRecordsAsync(campaignId, cancellationToken);
        var counts = waiting
            .Where(record => !string.IsNullOrEmpty(record.ProfileId) &&
                !string.Equals(record.ProfileId, profileId, StringComparison.Ordinal) &&
                movingActivityIds?.Contains(record.ActivityId) != true)
            .GroupBy(record => record.ProfileId, StringComparer.Ordinal)
            .Select(group => (ProfileId: group.Key, Count: group.Count()))
            .ToList();

        var conflicts = new List<DialerCampaignProfileConflict>(counts.Count);

        foreach (var (otherProfileId, count) in counts)
        {
            var profile = await dialerContributor.FindByIdAsync(otherProfileId, cancellationToken);

            conflicts.Add(new DialerCampaignProfileConflict(profile?.DisplayName ?? otherProfileId, count));
        }

        return conflicts;
    }

    /// <summary>
    /// Formats the conflicting profiles for a message, such as <c>'Leads Preview dial' (30)</c>.
    /// </summary>
    /// <param name="conflicts">The conflicts.</param>
    /// <returns>The formatted list.</returns>
    public static string Describe(IEnumerable<DialerCampaignProfileConflict> conflicts)
    {
        ArgumentNullException.ThrowIfNull(conflicts);

        return string.Join(", ", conflicts.Select(conflict => $"'{conflict.ProfileName}' ({conflict.Count})"));
    }
}

/// <summary>
/// A dialer profile that a campaign's waiting records are already queued under.
/// </summary>
/// <param name="ProfileName">The profile's display name.</param>
/// <param name="Count">How many waiting records it holds.</param>
internal sealed record DialerCampaignProfileConflict(string ProfileName, int Count);
