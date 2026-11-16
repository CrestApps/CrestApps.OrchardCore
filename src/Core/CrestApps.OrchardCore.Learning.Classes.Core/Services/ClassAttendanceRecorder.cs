using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Classes.Core.Services;

/// <summary>
/// Brings an enrollment's class item in line with the attendance of its registration: attendance that counts completes
/// the item on the manual path, as a staff record, so the learner's access window and the release rules do not stop
/// it. A refusal that can pass (a busy enrollment, an enrollment that is suspended for now) is retried.
/// </summary>
internal sealed class ClassAttendanceRecorder
{
    /// <summary>
    /// The reason the class item records when attendance completed it.
    /// </summary>
    public const string CompletionReason = "Attended a class session.";

    private readonly IEnrollmentStore _enrollments;
    private readonly ICourseStructureService _courses;
    private readonly ILearnerProgressService _progress;

    public ClassAttendanceRecorder(IEnrollmentStore enrollments, ICourseStructureService courses, ILearnerProgressService progress)
    {
        _enrollments = enrollments;
        _courses = courses;
        _progress = progress;
    }

    public async Task<ClassFollowUpOutcome> ApplyAsync(Appointment appointment, string actor, CancellationToken cancellationToken = default)
    {
        if (appointment is not { Source: ClassesConstants.RegistrationSource } ||
            !ClassesConstants.TryReadRegistrationSourceId(appointment.SourceId, out var enrollmentId, out var classItemId))
        {
            return ClassFollowUpOutcome.Done;
        }

        var enrollment = await _enrollments.FindByIdAsync(enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return ClassFollowUpOutcome.Done;
        }

        var course = await _courses.GetCourseAsync(enrollment.CourseId);
        var item = course is null ? null : (await _courses.GetOutlineAsync(course)).Find(classItemId)?.Item;
        var part = item is not null && item.TryGet<ClassPart>(out var found) ? found : null;

        if (appointment.Status != AppointmentStatus.Completed || !ClassAttendance.Counts(appointment, part))
        {
            return ClassFollowUpOutcome.Done;
        }

        var update = await _progress.CompleteItemAsync(enrollment, classItemId, actor ?? ClassesConstants.RegistrationSource, CompletionReason, cancellationToken);

        return Settle(update, enrollment);
    }

    private static ClassFollowUpOutcome Settle(ProgressUpdate update, Enrollment enrollment)
    {
        if (update.RefusedReason is null)
        {
            return ClassFollowUpOutcome.Done;
        }

        // A busy enrollment, or one that is pending or suspended for now, can still take the attendance later.
        return update.RefusedReason == LearningRefusals.Busy || (update.RefusedReason == LearningRefusals.NoAccess && (update.Enrollment ?? enrollment).IsOpen)
            ? ClassFollowUpOutcome.Retry(update.RefusedReason)
            : ClassFollowUpOutcome.GiveUp(update.RefusedReason);
    }
}
