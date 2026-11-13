using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Decides whether a learner holds one kind of course prerequisite. Learning answers for completed courses; the Skills
/// integration answers for certifications and skills. A prerequisite whose kind no evaluator answers is not met.
/// </summary>
public interface ICoursePrerequisiteEvaluator
{
    /// <summary>
    /// Gets the kind of prerequisite this evaluator answers, such as <c>Course</c>.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Decides whether a learner holds a prerequisite.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="prerequisite">The prerequisite.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the learner holds it.</returns>
    Task<bool> IsMetAsync(string userId, CoursePrerequisite prerequisite, CancellationToken cancellationToken = default);
}
