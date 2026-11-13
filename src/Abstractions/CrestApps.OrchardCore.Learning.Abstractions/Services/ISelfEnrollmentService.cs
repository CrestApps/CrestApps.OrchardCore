namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Lets a signed-in learner enroll in a course on their own, as the course's enrollment mode allows: at once in an open
/// course, with the right key, or pending approval.
/// </summary>
public interface ISelfEnrollmentService
{
    /// <summary>
    /// Enrolls the learner as the course allows.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="courseId">The content item id of the course.</param>
    /// <param name="enrollmentKey">The enrollment key the learner typed, for a course that needs one.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome. A wrong key fails with <see cref="EnrollmentErrors.InvalidKey"/>.</returns>
    Task<EnrollmentResult> EnrollAsync(string userId, string courseId, string enrollmentKey, CancellationToken cancellationToken = default);
}
