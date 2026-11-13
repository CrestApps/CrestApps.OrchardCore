using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Hears what happens to enrollments, after the change is saved: workflows, notifications, webhooks, the Audit Trail
/// and other Learning modules observe through it. Every member has an empty default.
/// </summary>
public interface IEnrollmentObserver
{
    /// <summary>
    /// A learner asked to enroll in a course that needs approval.
    /// </summary>
    /// <param name="enrollment">The pending enrollment.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task RequestedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// A learner was enrolled: a new active enrollment, or a pending one approved.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task EnrolledAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// An enrollment moved to another state.
    /// </summary>
    /// <param name="enrollment">The enrollment, in its new state.</param>
    /// <param name="previous">The state before.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task StatusChangedAsync(Enrollment enrollment, EnrollmentStatus previous, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// The learner opened the first course item.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task CourseStartedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// The learner completed a course item.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="item">The progress on the item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ItemCompletedAsync(Enrollment enrollment, CourseItemProgress item, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// The learner completed the course.
    /// </summary>
    /// <param name="enrollment">The completed enrollment, with its completion snapshot.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task CompletedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// The end of access or the due date of an enrollment changed.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="actor">Who changed it.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ExtendedAsync(Enrollment enrollment, string actor, string reason, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// The access of an open enrollment ends within the notice period. Raised once per enrollment.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task AccessEndingAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// The learner of an active enrollment has not worked on the course for the inactivity period. Raised once until the
    /// learner works on the course again.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task InactiveAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
