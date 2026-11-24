using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Adds links to the panel a learner sees when they complete a course, such as the certificate or the digital credential
/// the completion earned. Modules that issue something on completion register one; Learning names none of them.
/// </summary>
public interface ICourseCompletionLinkProvider
{
    /// <summary>
    /// Gets the links for a completed enrollment. Only the learner's own enrollment is passed.
    /// </summary>
    /// <param name="enrollment">The completed enrollment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The links, or none.</returns>
    Task<IEnumerable<CourseCompletionLink>> GetLinksAsync(Enrollment enrollment, CancellationToken cancellationToken = default);
}
