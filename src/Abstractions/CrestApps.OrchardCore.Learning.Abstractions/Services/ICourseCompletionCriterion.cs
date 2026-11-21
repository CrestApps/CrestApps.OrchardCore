namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// A course completion criterion beyond "all required items": a minimum course grade, a minimum time, approval by the
/// course staff. Registered with <c>AddCourseCompletionCriterion</c>.
/// </summary>
public interface ICourseCompletionCriterion
{
    /// <summary>
    /// Gets the stable name of the criterion, stored in <c>LearningRuleSettings.Name</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Decides whether the criterion is met.
    /// </summary>
    /// <param name="context">The criterion's settings, the enrollment, its progress and the outline.</param>
    /// <returns><see langword="true"/> when the criterion is met.</returns>
    Task<bool> IsMetAsync(CompletionCriterionContext context);
}
