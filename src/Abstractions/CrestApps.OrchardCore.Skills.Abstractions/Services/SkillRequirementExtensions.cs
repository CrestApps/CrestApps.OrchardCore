using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Matches people against the skills a piece of work needs.
/// </summary>
public static class SkillRequirementExtensions
{
    /// <summary>
    /// Gets whether a person holds every required skill at its lowest proficiency on a day. Requirements without a
    /// skill are ignored, so no requirements means everybody qualifies.
    /// </summary>
    /// <param name="skillAssignmentService">The skill assignment service.</param>
    /// <param name="subject">The person.</param>
    /// <param name="requirements">The skills the work needs.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the person holds them all.</returns>
    public static async Task<bool> HoldsAllAsync(
        this ISkillAssignmentService skillAssignmentService,
        SkillSubject subject,
        IEnumerable<SkillRequirement> requirements,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillAssignmentService);

        foreach (var requirement in requirements ?? [])
        {
            if (string.IsNullOrWhiteSpace(requirement?.SkillId))
            {
                continue;
            }

            if (!await skillAssignmentService.HoldsAsync(subject, requirement.SkillId, Math.Max(requirement.MinProficiency, 1), date, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }
}
