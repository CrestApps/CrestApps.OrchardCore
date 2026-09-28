using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reads the skill requirements of a queue and the proficiencies of an agent, merging the structured values with
/// the bare tag lists that predate them. Both strategies read through here so a queue and an agent cannot
/// disagree about what counts as the same skill, or about what an untyped tag means.
/// </summary>
public static class SkillMatching
{
    /// <summary>
    /// Reads the queue's requirements, treating any bare tag with no structured requirement as required at the
    /// default proficiency and never relaxed — which is exactly how a tag behaved before this existed.
    /// </summary>
    /// <param name="queue">The queue.</param>
    public static IReadOnlyList<QueueSkillRequirement> GetRequirements(ActivityQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        var requirements = new Dictionary<string, QueueSkillRequirement>(StringComparer.OrdinalIgnoreCase);

        foreach (var requirement in queue.SkillRequirements)
        {
            var skill = SkillTag.Create(requirement?.SkillId);

            if (skill.HasValue)
            {
                requirements[skill.Value] = requirement;
            }
        }

        foreach (var tag in SkillTag.CreateAll(queue.RequiredSkills))
        {
            if (!requirements.ContainsKey(tag.Value))
            {
                requirements[tag.Value] = new QueueSkillRequirement { SkillId = tag.Value };
            }
        }

        return requirements.Values.ToArray();
    }

    /// <summary>
    /// Reads the agent's proficiency in a skill, or zero when they do not hold it. A skill present only as a
    /// bare tag reads as <see cref="AgentSkill.DefaultProficiency"/>.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="skillId">The skill identifier.</param>
    public static int GetProficiency(AgentProfile agent, string skillId)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var skill = SkillTag.Create(skillId);

        if (!skill.HasValue)
        {
            return 0;
        }

        foreach (var proficiency in agent.SkillProficiencies)
        {
            var candidate = SkillTag.Create(proficiency?.SkillId);

            if (candidate.HasValue && candidate.Equals(skill))
            {
                return proficiency.Proficiency;
            }
        }

        return SkillTag.CreateAll(agent.Skills).Contains(skill)
            ? AgentSkill.DefaultProficiency
            : 0;
    }

    /// <summary>
    /// Determines whether a requirement has been waiting long enough to be dropped.
    /// </summary>
    /// <param name="requirement">The requirement.</param>
    /// <param name="waited">How long the contact has waited.</param>
    public static bool IsRelaxed(QueueSkillRequirement requirement, TimeSpan waited)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        return requirement.RelaxAfterSeconds is > 0
            && waited.TotalSeconds > requirement.RelaxAfterSeconds.Value;
    }
}
