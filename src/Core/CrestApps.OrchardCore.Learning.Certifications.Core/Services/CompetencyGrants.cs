using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Services;

/// <summary>
/// Grants the competencies of a <see cref="CompetenciesPart"/> to a learner through Skills. A competency is a skill,
/// and its framework the skill's category.
/// </summary>
public static class CompetencyGrants
{
    /// <summary>
    /// The text that starts the <see cref="SkillAssignment.AssignedBy"/> of a skill granted by learning.
    /// </summary>
    public const string ActorPrefix = "Learning";

    /// <summary>
    /// Gets the <see cref="SkillAssignment.AssignedBy"/> of a skill granted by completing a course or a path.
    /// </summary>
    /// <param name="title">The title of the course or path.</param>
    /// <returns>The actor.</returns>
    public static string Actor(string title)
        => string.IsNullOrWhiteSpace(title) ? ActorPrefix : $"{ActorPrefix}: {title.Trim()}";

    /// <summary>
    /// Grants each skill of a part to a user at the part's level (kept on the Skills scale). A skill the user already
    /// holds at that level or above, on the day, is left as it is; one held lower is raised and keeps its start day;
    /// one that ended before the day starts again on the day, at the higher of the two levels. A disabled or deleted skill is skipped.
    /// </summary>
    /// <param name="skills">The skill assignment service.</param>
    /// <param name="part">The competencies.</param>
    /// <param name="userId">The learner.</param>
    /// <param name="earnedOn">The day the learner completed the course or path, in the site's time zone.</param>
    /// <param name="actor">Who grants the skills, from <see cref="Actor"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many skills were given or raised.</returns>
    public static async Task<int> GrantAsync(ISkillAssignmentService skills, CompetenciesPart part, string userId, DateOnly earnedOn, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skills);

        if (part?.SkillIds is not { Count: > 0 } || string.IsNullOrEmpty(userId))
        {
            return 0;
        }

        var subject = SkillSubject.User(userId);
        var level = Math.Clamp(part.Proficiency, SkillProficiency.Minimum, SkillProficiency.Maximum);
        var held = (await skills.GetAssignmentsAsync(subject, cancellationToken))
            .GroupBy(assignment => assignment.SkillId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var granted = 0;

        foreach (var skillId in part.SkillIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
        {
            var input = new SkillAssignmentInput { SkillId = skillId, Proficiency = level, EffectiveFrom = earnedOn };

            if (held.TryGetValue(skillId, out var existing))
            {
                if (existing.EffectiveTo is not null && existing.EffectiveTo < earnedOn)
                {
                    // It ended: it starts again on the day, at the higher of the two levels.
                    input.Proficiency = Math.Max(existing.Proficiency, level);
                }
                else if (existing.Proficiency >= level)
                {
                    continue;
                }
                else
                {
                    input.EffectiveFrom = existing.EffectiveFrom;
                }
            }

            var result = await skills.AssignAsync(subject, input, actor, cancellationToken);

            if (result is null || result.Succeeded)
            {
                granted++;
            }
        }

        return granted;
    }
}
