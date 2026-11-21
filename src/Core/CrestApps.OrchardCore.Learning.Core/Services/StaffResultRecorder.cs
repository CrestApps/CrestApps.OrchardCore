using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Records results of course items for the modules that score work (coursework, quiz attempts), trying again while the
/// enrollment is busy so a result is not lost to another request writing the same enrollment. A result a member of the
/// course staff gave, such as a grade, goes on the manual path (<see cref="CourseItemResult.RecordedBy"/>).
/// </summary>
public static class StaffResultRecorder
{
    /// <summary>
    /// How many times a result is offered while the enrollment is busy.
    /// </summary>
    public const int MaxTries = 3;

    /// <summary>
    /// Records a result, trying again while the enrollment is busy.
    /// </summary>
    /// <param name="progress">The progress service.</param>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="result">The result.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What changed; <see cref="ProgressUpdate.RefusedReason"/> says why nothing did.</returns>
    public static async Task<ProgressUpdate> RecordWithRetryAsync(this ILearnerProgressService progress, Enrollment enrollment, CourseItemResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(result);

        ProgressUpdate update = null;

        for (var tries = 0; tries < MaxTries; tries++)
        {
            update = await progress.RecordResultAsync(enrollment, result, cancellationToken);

            if (update?.RefusedReason != LearningRefusals.Busy)
            {
                break;
            }
        }

        return update;
    }

    /// <summary>
    /// Records a result a member of the course staff gave, such as a grade, on the manual path, trying again while the
    /// enrollment is busy.
    /// </summary>
    /// <param name="progress">The progress service.</param>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="result">The result.</param>
    /// <param name="actor">The user id of the staff member; the Learning system actor when there is none, such as a workflow.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What changed; <see cref="ProgressUpdate.RefusedReason"/> says why nothing did, such as an enrollment that is not active.</returns>
    public static Task<ProgressUpdate> RecordStaffResultAsync(this ILearnerProgressService progress, Enrollment enrollment, CourseItemResult result, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        result.RecordedBy = string.IsNullOrEmpty(actor) ? DefaultEnrollmentService.SystemActor : actor;

        return progress.RecordWithRetryAsync(enrollment, result, cancellationToken);
    }

    /// <summary>
    /// Tells whether a refusal is about the enrollment no longer being active, so a person can reactivate it.
    /// </summary>
    /// <param name="refusedReason">The refusal, one of <see cref="LearningRefusals"/>.</param>
    /// <returns><see langword="true"/> for an enrollment that is suspended, ended or withdrawn.</returns>
    public static bool IsEnrollmentNotActive(string refusedReason)
        => refusedReason is LearningRefusals.NoAccess or LearningRefusals.NotActive;
}
