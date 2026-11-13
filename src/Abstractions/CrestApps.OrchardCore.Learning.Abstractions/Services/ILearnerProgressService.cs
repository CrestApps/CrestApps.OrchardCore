using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Records a learner's work on course items and completes the course when its criteria are met. Every decision is made
/// on the server; the caller only says what the learner did.
/// </summary>
public interface ILearnerProgressService
{
    /// <summary>
    /// Gets the progress of an enrollment.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The progress, empty when the learner has not opened any item.</returns>
    Task<EnrollmentProgress> GetAsync(string enrollmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the learner opened a course item and how long they stayed since the last call.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="itemId">The content item id of the course item.</param>
    /// <param name="secondsSpent">The seconds spent since the last call, capped by the service.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What changed.</returns>
    Task<ProgressUpdate> RecordAccessAsync(Enrollment enrollment, string itemId, int secondsSpent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records how much of a video the learner watched.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="itemId">The content item id of the video lesson.</param>
    /// <param name="percentViewed">The share watched, from 0 to 100.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What changed.</returns>
    Task<ProgressUpdate> RecordViewedAsync(Enrollment enrollment, string itemId, int percentViewed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the learner marked an item done, for the <c>MarkedDone</c> completion mode.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="itemId">The content item id of the course item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What changed.</returns>
    Task<ProgressUpdate> MarkDoneAsync(Enrollment enrollment, string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the learner confirmed the statement of an acknowledgement, as shown. The confirmation never changes.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="itemId">The content item id of the acknowledgement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What changed.</returns>
    Task<ProgressUpdate> AcknowledgeAsync(Enrollment enrollment, string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a scored result of a course item, such as a graded quiz or submission.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="result">The result.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What changed.</returns>
    Task<ProgressUpdate> RecordResultAsync(Enrollment enrollment, CourseItemResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a course item complete by hand, with an actor and a reason.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="itemId">The content item id of the course item.</param>
    /// <param name="actor">The user id of the person.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What changed.</returns>
    Task<ProgressUpdate> CompleteItemAsync(Enrollment enrollment, string itemId, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a member of the course staff approves the learner's completion, for a course whose completion
    /// criteria include staff approval, and completes the course when every other criterion is met.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="actor">The user id of the person approving.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What changed.</returns>
    Task<ProgressUpdate> ApproveCompletionAsync(Enrollment enrollment, string actor, CancellationToken cancellationToken = default);
}
