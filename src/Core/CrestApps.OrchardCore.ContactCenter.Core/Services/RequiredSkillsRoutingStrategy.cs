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

        var requiredSkills = context.Queue.RequiredSkills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requiredSkills.Length == 0)
        {
            foreach (var candidate in context.Candidates)
            {
                candidate.AddReason("No queue skills are required.");
            }

            return ValueTask.CompletedTask;
        }

        foreach (var candidate in context.Candidates)
        {
            var agentSkills = new HashSet<string>(
                candidate.Agent.Skills.Where(skill => !string.IsNullOrWhiteSpace(skill)),
                StringComparer.OrdinalIgnoreCase);

            var missingSkills = requiredSkills
                .Where(skill => !agentSkills.Contains(skill))
                .ToArray();

            if (missingSkills.Length > 0)
            {
                candidate.IsEligible = false;
                candidate.AddReason($"Missing required skills: {string.Join(", ", missingSkills)}.");
            }
            else
            {
                candidate.AddReason("Matched every required queue skill.");
            }
        }

        return ValueTask.CompletedTask;
    }
}
