using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Rejects agents that do not have every skill required by the queue.
/// </summary>
public sealed class RequiredSkillsRoutingStrategy : IActivityRoutingStrategy
{
    /// <inheritdoc/>
    public int Order => 10;

    /// <inheritdoc/>
    public ValueTask ApplyAsync(ActivityRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Both sides are read through the same type, so the queue and the agent cannot disagree about what
        // counts as the same skill. Comparing the stored strings directly is what let an agent whose skill
        // was written with surrounding whitespace go unmatched without any sign that something was wrong.
        var requiredSkills = SkillTag.CreateAll(context.Queue.RequiredSkills);

        if (requiredSkills.Count == 0)
        {
            foreach (var candidate in context.Candidates)
            {
                candidate.AddReason("No queue skills are required.");
            }

            return ValueTask.CompletedTask;
        }

        foreach (var candidate in context.Candidates)
        {
            var agentSkills = new HashSet<SkillTag>(SkillTag.CreateAll(candidate.Agent.Skills));

            var missingSkills = requiredSkills
                .Where(skill => !agentSkills.Contains(skill))
                .ToArray();

            if (missingSkills.Length > 0)
            {
                candidate.IsEligible = false;
                candidate.AddReason($"Missing required skills: {string.Join(", ", missingSkills.Select(skill => skill.Value))}.");
            }
            else
            {
                candidate.AddReason("Matched every required queue skill.");
            }
        }

        return ValueTask.CompletedTask;
    }
}
