using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Ranks candidates by the skills the queue prefers but does not demand, and by how far past the minimum an
/// agent's proficiency reaches. It never rejects anybody: a preference that eliminated candidates would be a
/// requirement, and the queue said it was not.
/// </summary>
public sealed class PreferredSkillsRoutingStrategy : IActivityRoutingStrategy
{
    private const double PreferredSkillWeight = 10d;
    private const double ProficiencyWeight = 1d;

    /// <inheritdoc/>
    public int Order => 40;

    /// <inheritdoc/>
    public ValueTask ApplyAsync(ActivityRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requirements = SkillMatching.GetRequirements(context.Queue);

        if (requirements.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        foreach (var candidate in context.Candidates)
        {
            if (!candidate.IsEligible)
            {
                continue;
            }

            var score = 0d;

            foreach (var requirement in requirements)
            {
                var proficiency = SkillMatching.GetProficiency(candidate.Agent, requirement.SkillId);

                if (proficiency < requirement.MinimumProficiency)
                {
                    continue;
                }

                // Holding the skill at all is worth more than holding it well, so an agent who qualifies always
                // outranks one who does not, however expert the second is in something else.
                score += PreferredSkillWeight;
                score += (proficiency - requirement.MinimumProficiency) * ProficiencyWeight;
            }

            if (score > 0)
            {
                candidate.Score += score;
                candidate.AddReason($"Preferred-skill score {score:0.##}.");
            }
        }

        return ValueTask.CompletedTask;
    }
}
