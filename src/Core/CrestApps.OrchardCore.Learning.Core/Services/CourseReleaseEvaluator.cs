using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Decides whether a course item is open to a learner: every rule of its section and every rule of the item must be met.
/// A rule whose name no registered rule answers keeps the item locked.
/// </summary>
public sealed class CourseReleaseEvaluator
{
    private readonly IEnumerable<ICourseItemReleaseRule> _rules;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseReleaseEvaluator"/> class.
    /// </summary>
    /// <param name="rules">The registered release rules.</param>
    /// <param name="clock">The clock.</param>
    public CourseReleaseEvaluator(
        IEnumerable<ICourseItemReleaseRule> rules,
        IClock clock)
    {
        _rules = rules;
        _clock = clock;
    }

    /// <summary>
    /// Decides whether an outline entry is open to a learner.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="progress">The progress of the enrollment.</param>
    /// <returns>The decision of the first rule not met, or <see cref="ReleaseDecision.Released"/>.</returns>
    public async Task<ReleaseDecision> EvaluateAsync(CourseOutlineEntry entry, Enrollment enrollment, EnrollmentProgress progress)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var settings = (entry.Section?.ReleaseRules ?? []).Concat(entry.Part?.ReleaseRules ?? []);

        foreach (var setting in settings)
        {
            if (setting is null || string.IsNullOrEmpty(setting.Name))
            {
                continue;
            }

            var rule = _rules.FirstOrDefault(candidate => string.Equals(candidate.Name, setting.Name, StringComparison.OrdinalIgnoreCase));

            if (rule is null)
            {
                return ReleaseDecision.Locked(LearningRefusals.NotAvailable);
            }

            var decision = await rule.EvaluateAsync(new ReleaseRuleContext
            {
                Settings = setting,
                Enrollment = enrollment,
                Progress = progress ?? new EnrollmentProgress(),
                Item = entry.Item,
                UtcNow = _clock.UtcNow,
            });

            if (!decision.IsReleased)
            {
                return decision;
            }
        }

        return ReleaseDecision.Released;
    }
}
