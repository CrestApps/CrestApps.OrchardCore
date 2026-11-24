using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Enrolls learners and moves enrollments through their states. Every change keeps who made it and why, and raises the
/// observers after the change is saved.
/// </summary>
public interface IEnrollmentService
{
    /// <summary>
    /// Enrolls a learner, or adds a grant to the learner's open enrollment in the course. A request with a source and
    /// source id already granted changes nothing.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<EnrollmentResult> EnrollAsync(EnrollmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a pending enrollment.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="actor">The user id of the person approving.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<EnrollmentResult> ApproveAsync(string enrollmentId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a pending enrollment: it is withdrawn with the reason.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="actor">The user id of the person rejecting.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<EnrollmentResult> RejectAsync(string enrollmentId, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an enrollment to another state, such as suspended, active again or withdrawn. Completing by hand needs a reason.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="status">The new state.</param>
    /// <param name="actor">The user id of the person, or the name of the process.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<EnrollmentResult> ChangeStatusAsync(string enrollmentId, EnrollmentStatus status, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes when access ends and when the course is due. Access is extended by adding a manual grant.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="accessEndsUtc">The new end of access, or <see langword="null"/> to keep it.</param>
    /// <param name="dueUtc">The new due date, or <see langword="null"/> to keep it.</param>
    /// <param name="actor">The user id of the person.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<EnrollmentResult> ExtendAsync(string enrollmentId, DateTime? accessEndsUtc, DateTime? dueUtc, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the grants of a source record, such as a refunded order line. An open enrollment left with no grant is withdrawn.
    /// </summary>
    /// <param name="source">The enrollment source.</param>
    /// <param name="sourceId">The id of the record in its source.</param>
    /// <param name="actor">The name of the process or person.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enrollments that changed.</returns>
    Task<IReadOnlyList<Enrollment>> RevokeGrantAsync(string source, string sourceId, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires the open enrollments whose access has ended.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The number of enrollments expired.</returns>
    Task<int> ExpireEndedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the open review of an enrollment, recording who decided what.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="actor">Who cleared it.</param>
    /// <param name="note">What they decided; required.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<EnrollmentResult> ClearReviewAsync(string enrollmentId, string actor, string note, CancellationToken cancellationToken = default);
}
