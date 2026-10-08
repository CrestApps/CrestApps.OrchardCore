using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides case-insensitive helpers that keep an agent's live queue and campaign membership constrained
/// to the manager-owned entitlements granted on the agent's profile. Every consumer that reads or writes
/// <see cref="AgentProfile.QueueIds"/> or <see cref="AgentProfile.CampaignIds"/> for authorization purposes
/// should use these helpers instead of comparing the raw lists directly, so entitlement enforcement stays
/// consistent across sign-in, membership updates, and routing.
/// </summary>
public static class AgentEntitlementUtilities
{
    /// <summary>
    /// Trims, removes blanks from, and de-duplicates a list of identifiers using a case-insensitive comparison.
    /// </summary>
    /// <param name="ids">The identifiers to normalize.</param>
    /// <returns>The normalized, de-duplicated list of identifiers.</returns>
    public static IList<string> NormalizeIds(IEnumerable<string> ids)
    {
        if (ids is null)
        {
            return [];
        }

        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Filters the requested identifiers down to the subset that is present in the allowed identifiers,
    /// using a case-insensitive comparison. Any requested identifier that is not entitled is silently
    /// dropped, so callers can rely on the result never exceeding the granted entitlements.
    /// </summary>
    /// <param name="requestedIds">The identifiers being requested, for example from a sign-in form.</param>
    /// <param name="allowedIds">The manager-owned entitlements the caller is allowed to select from.</param>
    /// <returns>The entitled subset of <paramref name="requestedIds"/>.</returns>
    /// <summary>
    /// Normalizes a proficiency list: one entry per skill, the name trimmed, and the proficiency clamped to the
    /// supported range. A blank skill is a row somebody added and never filled in.
    /// </summary>
    /// <param name="skills">The proficiencies as entered.</param>
    /// <returns>The normalized proficiencies.</returns>
    public static IList<AgentSkill> NormalizeSkillProficiencies(IEnumerable<AgentSkill> skills)
    {
        var result = new List<AgentSkill>();
        var seen = new HashSet<SkillTag>();

        foreach (var skill in skills ?? [])
        {
            if (skill is null || !SkillTag.TryCreate(skill.SkillId, out var tag) || !seen.Add(tag))
            {
                continue;
            }

            result.Add(new AgentSkill
            {
                SkillId = tag.Value,
                Proficiency = Math.Clamp(skill.Proficiency, AgentSkill.MinimumProficiency, AgentSkill.MaximumProficiency),
            });
        }

        return result;
    }

    /// <summary>
    /// Normalizes a queue-preference list: one entry per queue, the identifier trimmed, and no negative
    /// priority or delay.
    /// </summary>
    /// <param name="memberships">The preferences as entered.</param>
    /// <returns>The normalized preferences.</returns>
    public static IList<AgentQueueMembership> NormalizeQueueMemberships(IEnumerable<AgentQueueMembership> memberships)
    {
        var result = new List<AgentQueueMembership>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var membership in memberships ?? [])
        {
            var queueId = membership?.QueueId?.Trim();

            if (string.IsNullOrEmpty(queueId) || !seen.Add(queueId))
            {
                continue;
            }

            result.Add(new AgentQueueMembership
            {
                QueueId = queueId,
                Priority = Math.Max(0, membership.Priority),
                DelaySeconds = Math.Max(0, membership.DelaySeconds),
            });
        }

        return result;
    }

    /// <summary>
    /// Applies a skill set given as a plain tag list, a proficiency list, or both. The tag list is what every
    /// reader of "does this agent have the skill" sees, so it always includes every skill with a proficiency;
    /// a tag without a proficiency is held at the default. A null proficiency list leaves the agent's existing
    /// proficiencies alone, so a tag-only import does not wipe levels a manager has set.
    /// </summary>
    /// <param name="profile">The agent profile.</param>
    /// <param name="skills">The plain skill tags, or null for none.</param>
    /// <param name="proficiencies">The proficiencies, or null to leave the existing ones alone.</param>
    public static void ApplySkills(AgentProfile profile, IEnumerable<string> skills, IEnumerable<AgentSkill> proficiencies)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (proficiencies is null)
        {
            profile.Skills = NormalizeIds(skills);

            return;
        }

        var normalizedProficiencies = NormalizeSkillProficiencies(proficiencies);

        profile.SkillProficiencies = normalizedProficiencies;
        profile.Skills = NormalizeIds((skills ?? []).Concat(normalizedProficiencies.Select(proficiency => proficiency.SkillId)));
    }

    public static IList<string> FilterEntitled(IEnumerable<string> requestedIds, IEnumerable<string> allowedIds)
    {
        var allowed = new HashSet<string>(NormalizeIds(allowedIds), StringComparer.OrdinalIgnoreCase);

        return NormalizeIds(requestedIds)
            .Where(allowed.Contains)
            .ToList();
    }

    /// <summary>
    /// Determines, using a case-insensitive comparison, whether the agent is entitled to the queue and is
    /// currently signed in to it. Fails closed: an agent with no queue entitlements is never entitled,
    /// regardless of what <see cref="AgentProfile.QueueIds"/> contains.
    /// </summary>
    /// <param name="profile">The agent profile to check.</param>
    /// <param name="queueId">The queue identifier.</param>
    /// <returns><see langword="true"/> when the agent is entitled to and signed in to the queue.</returns>
    public static bool HasQueueEntitlement(AgentProfile profile, string queueId)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrEmpty(queueId))
        {
            return false;
        }

        return profile.QueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) == true &&
            profile.AllowedQueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Returns the queues the agent is both entitled to and currently signed in to, using a
    /// case-insensitive comparison. This is the effective routing membership: the intersection of
    /// <see cref="AgentProfile.QueueIds"/> and <see cref="AgentProfile.AllowedQueueIds"/>. Fails closed,
    /// so an agent with no queue entitlements produces an empty result regardless of live sign-in state.
    /// </summary>
    /// <param name="profile">The agent profile to inspect.</param>
    /// <returns>The normalized, de-duplicated set of entitled and signed-in queue identifiers.</returns>
    public static IList<string> GetEntitledQueueIds(AgentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return FilterEntitled(profile.QueueIds, profile.AllowedQueueIds);
    }

    /// <summary>
    /// Determines, using a case-insensitive comparison, whether the agent is entitled to the dialer
    /// campaign and is currently signed in to it. Fails closed: an agent with no campaign entitlements is
    /// never entitled, regardless of what <see cref="AgentProfile.CampaignIds"/> contains.
    /// </summary>
    /// <param name="profile">The agent profile to check.</param>
    /// <param name="campaignId">The dialer campaign identifier.</param>
    /// <returns><see langword="true"/> when the agent is entitled to and signed in to the campaign.</returns>
    public static bool HasCampaignEntitlement(AgentProfile profile, string campaignId)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrEmpty(campaignId))
        {
            return false;
        }

        return profile.CampaignIds?.Contains(campaignId, StringComparer.OrdinalIgnoreCase) == true &&
            profile.AllowedCampaignIds?.Contains(campaignId, StringComparer.OrdinalIgnoreCase) == true;
    }
}
