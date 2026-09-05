using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Rejects agents who do not meet the queue's required skills at the proficiency it asked for — unless the
/// contact has waited past the requirement's relaxation window, at which point reaching a generalist beats
/// waiting indefinitely for a specialist who is not there. A requirement with no window is one the queue meant
/// absolutely and is never dropped.
/// <para>
/// Which requirements were relaxed is recorded on the candidate's reasons, so a supervisor looking at a routing
/// decision can see why an under-skilled agent got the call rather than having to guess.
/// </para>
/// </summary>
public sealed class RequiredSkillsRoutingStrategy : IActivityRoutingStrategy
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequiredSkillsRoutingStrategy"/> class.
    /// </summary>
    /// <param name="clock">The clock used to measure how long the contact has waited.</param>
    public RequiredSkillsRoutingStrategy(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public int Order => 10;

    /// <inheritdoc/>
    public ValueTask ApplyAsync(ActivityRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var required = SkillMatching.GetRequirements(context.Queue)
            .Where(requirement => requirement.Required)
            .ToArray();

        if (required.Length == 0)
        {
            foreach (var candidate in context.Candidates)
            {
                candidate.AddReason("No queue skills are required.");
            }

            return ValueTask.CompletedTask;
        }

        var waited = context.QueueItem is null
            ? TimeSpan.Zero
            : _clock.UtcNow - context.QueueItem.EnqueuedUtc;

        var relaxed = required.Where(requirement => SkillMatching.IsRelaxed(requirement, waited)).ToArray();
        var enforced = required.Except(relaxed).ToArray();

        foreach (var candidate in context.Candidates)
        {
            var missing = enforced
                .Where(requirement => SkillMatching.GetProficiency(candidate.Agent, requirement.SkillId) < requirement.MinimumProficiency)
                .Select(requirement => $"{requirement.SkillId} (needs {requirement.MinimumProficiency})")
                .ToArray();

            if (missing.Length > 0)
            {
                candidate.IsEligible = false;
                candidate.AddReason($"Missing required skills: {string.Join(", ", missing)}.");

                continue;
            }

            candidate.AddReason("Matched every enforced queue skill.");

            if (relaxed.Length > 0)
            {
                candidate.AddReason(
                    $"Relaxed after {(int)waited.TotalSeconds}s of waiting: {string.Join(", ", relaxed.Select(requirement => requirement.SkillId))}.");
            }
        }

        return ValueTask.CompletedTask;
    }
}
