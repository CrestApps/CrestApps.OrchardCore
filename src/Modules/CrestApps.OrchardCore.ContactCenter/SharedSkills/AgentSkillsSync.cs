using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Keeps an agent's routing proficiencies and the user's skills in the shared catalog in step. Contact Center names a
/// skill by its name, the catalog by its id; a Contact Center skill name the catalog doesn't know stays only on the
/// agent, untouched.
/// </summary>
internal sealed class AgentSkillsSync
{
    private readonly ISkillManager _skillManager;
    private readonly ISkillAssignmentService _assignmentService;
    private readonly IClock _clock;

    public AgentSkillsSync(
        ISkillManager skillManager,
        ISkillAssignmentService assignmentService,
        IClock clock)
    {
        _skillManager = skillManager;
        _assignmentService = assignmentService;
        _clock = clock;
    }

    /// <summary>
    /// Gets whether a sync is writing right now, so the handlers of the other side don't answer their own echo.
    /// </summary>
    public bool Syncing { get; private set; }

    public static string Fingerprint(IEnumerable<AgentSkill> skills)
    {
        return string.Join('|', (skills ?? [])
            .Where(skill => !string.IsNullOrWhiteSpace(skill?.SkillId))
            .Select(skill => $"{SharedSkillsMapper.NormalizeName(skill.SkillId)}:{skill.Proficiency}")
            .Order(StringComparer.Ordinal));
    }

    public static bool IsInStep(AgentProfile profile)
    {
        return profile.TryGet<SharedSkillsSyncState>(out var state) && state.Fingerprint == Fingerprint(profile.SkillProficiencies);
    }

    public static bool HasSynced(AgentProfile profile)
    {
        return profile.TryGet<SharedSkillsSyncState>(out var state) && state.Fingerprint is not null;
    }

    /// <summary>
    /// Writes the agent's proficiencies to the user's shared skills: adds and changes them, and, when
    /// <paramref name="removeMissing"/> is set, takes away the catalog skills the agent no longer holds.
    /// </summary>
    public async Task PushAsync(AgentProfile profile, bool removeMissing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrEmpty(profile.UserId))
        {
            return;
        }

        var subject = SkillSubject.User(profile.UserId);
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var catalog = await _skillManager.GetAllAsync(cancellationToken);
        var byName = catalog
            .Where(skill => SharedSkillsMapper.NormalizeName(skill.Name) is not null)
            .GroupBy(skill => SharedSkillsMapper.NormalizeName(skill.Name), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var current = (await _assignmentService.GetAssignmentsAsync(subject, cancellationToken)).ToDictionary(assignment => assignment.SkillId, StringComparer.Ordinal);
        var wanted = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var agentSkill in profile.SkillProficiencies ?? [])
        {
            if (byName.TryGetValue(SharedSkillsMapper.NormalizeName(agentSkill?.SkillId) ?? string.Empty, out var skill) && skill.Enabled)
            {
                wanted[skill.ItemId] = Math.Clamp(agentSkill.Proficiency, SkillProficiency.Minimum, SkillProficiency.Maximum);
            }
        }

        Syncing = true;

        try
        {
            foreach (var (skillId, proficiency) in wanted)
            {
                current.TryGetValue(skillId, out var existing);

                if (existing is not null && existing.Proficiency == proficiency)
                {
                    continue;
                }

                await _assignmentService.AssignAsync(subject, new SkillAssignmentInput
                {
                    SkillId = skillId,
                    Proficiency = proficiency,
                    EffectiveFrom = existing?.EffectiveFrom,
                    EffectiveTo = existing?.EffectiveTo,
                }, "Contact Center", cancellationToken);
            }

            if (removeMissing)
            {
                // Only skills that count today are the agent's to take away; ones that start later or have ended
                // were set in the catalog and aren't on the agent.
                foreach (var assignment in current.Values.Where(assignment => !wanted.ContainsKey(assignment.SkillId) && assignment.IsEffectiveOn(today)))
                {
                    await _assignmentService.RemoveAsync(subject, assignment.SkillId, "Contact Center", cancellationToken);
                }
            }
        }
        finally
        {
            Syncing = false;
        }

        profile.Put(new SharedSkillsSyncState { Fingerprint = Fingerprint(profile.SkillProficiencies) });
    }

    /// <summary>
    /// Sets the agent's proficiencies from the user's shared skills that count today, keeping the Contact Center
    /// skill names the catalog doesn't know.
    /// </summary>
    public async Task PullAsync(AgentProfile profile, IEnumerable<SkillAssignment> assignments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(assignments);

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var catalog = (await _skillManager.GetAllAsync(cancellationToken)).ToDictionary(skill => skill.ItemId, StringComparer.Ordinal);
        var catalogNames = catalog.Values.Select(skill => SharedSkillsMapper.NormalizeName(skill.Name)).Where(name => name is not null).ToHashSet(StringComparer.Ordinal);
        var skills = (profile.SkillProficiencies ?? [])
            .Where(skill => skill is not null && !catalogNames.Contains(SharedSkillsMapper.NormalizeName(skill.SkillId) ?? string.Empty))
            .ToList();

        foreach (var assignment in assignments.Where(assignment => assignment.IsEffectiveOn(today)))
        {
            if (catalog.TryGetValue(assignment.SkillId, out var skill) && skill.Enabled)
            {
                skills.Add(new AgentSkill { SkillId = skill.Name, Proficiency = assignment.Proficiency });
            }
        }

        profile.SkillProficiencies = AgentEntitlementUtilities.NormalizeSkillProficiencies(skills);
        profile.Put(new SharedSkillsSyncState { Fingerprint = Fingerprint(profile.SkillProficiencies) });
    }

    /// <summary>
    /// Runs work during which the other side's handlers stay quiet.
    /// </summary>
    public async Task RunSilentlyAsync(Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        var previous = Syncing;

        Syncing = true;

        try
        {
            await work();
        }
        finally
        {
            Syncing = previous;
        }
    }
}
