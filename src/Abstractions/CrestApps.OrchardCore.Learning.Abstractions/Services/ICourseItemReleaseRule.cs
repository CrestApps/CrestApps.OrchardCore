namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// A rule that decides whether a course item or section is open to a learner yet: another item completed, a date, a
/// number of days after enrollment. Registered with <c>AddCourseItemReleaseRule</c>.
/// </summary>
public interface ICourseItemReleaseRule
{
    /// <summary>
    /// Gets the stable name of the rule, stored in <c>LearningRuleSettings.Name</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Decides whether the rule is met for a learner.
    /// </summary>
    /// <param name="context">The rule's settings, the enrollment, its progress and the item.</param>
    /// <returns>The decision.</returns>
    Task<ReleaseDecision> EvaluateAsync(ReleaseRuleContext context);
}
