using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Notifications;

/// <summary>
/// Tells learners, through Orchard Core notifications, that they are enrolled (also when a request is approved), that a
/// request was declined, that a course is due soon and that it is overdue, tells instructors a completion needs review,
/// and tells the people who may approve it that someone asked to enroll. Each notice is a registered notification type, so a learner or the site can switch it off; it is sent after
/// the change is committed.
/// </summary>
internal sealed class LearningNotificationObserver : IEnrollmentObserver
{
    private readonly ICourseStructureService _courseStructure;
    private readonly ILearningNotificationSender _sender;
    private readonly EnrollmentApprovers _approvers;

    internal readonly IStringLocalizer S;

    public LearningNotificationObserver(
        ICourseStructureService courseStructure,
        ILearningNotificationSender sender,
        EnrollmentApprovers approvers,
        IStringLocalizer<LearningNotificationObserver> stringLocalizer)
    {
        _courseStructure = courseStructure;
        _sender = sender;
        _approvers = approvers;
        S = stringLocalizer;
    }

    public async Task RequestedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);
        var title = course?.DisplayText ?? S["a course"].Value;

        foreach (var approver in await _approvers.ListAsync(course))
        {
            if (string.Equals(approver, enrollment.UserId, StringComparison.Ordinal))
            {
                continue;
            }

            Send(enrollment, LearningConstants.NotificationTypes.EnrollmentRequested, S["A request to enroll in {0} waits for approval", title], S["Someone asked to enroll in the course {0}. Open the enrollment to approve or decline it.", title], approver, $"~/Admin/learning/enrollments/{enrollment.ItemId}");
        }
    }

    public async Task EnrolledAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        var title = await TitleAsync(enrollment);

        Send(enrollment, LearningConstants.NotificationTypes.Enrolled, S["You are enrolled in {0}", title], S["You can start the course {0} now.", title]);
    }

    public async Task StatusChangedAsync(Enrollment enrollment, EnrollmentStatus previous, CancellationToken cancellationToken = default)
    {
        // Only a declined request is told here; an approved one is told as an enrollment.
        if (previous != EnrollmentStatus.Pending || enrollment.Status != EnrollmentStatus.Withdrawn)
        {
            return;
        }

        var title = await TitleAsync(enrollment);

        Send(enrollment, LearningConstants.NotificationTypes.EnrollmentDeclined, S["Your request to enroll in {0} was declined", title], S["Your request to enroll in the course {0} was declined. Open My learning to see your courses.", title], enrollment.UserId, "~/learning");
    }

    public async Task DueSoonAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        var title = await TitleAsync(enrollment);

        Send(enrollment, LearningConstants.NotificationTypes.DueSoon, S["{0} is due soon", title], S["Complete the course {0} before its due date.", title]);
    }

    public async Task OverdueAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        var title = await TitleAsync(enrollment);

        Send(enrollment, LearningConstants.NotificationTypes.Overdue, S["{0} is overdue", title], S["The course {0} has passed its due date. Complete it as soon as you can.", title]);
    }

    public async Task ReviewNeededAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);
        var title = course?.DisplayText ?? enrollment.Completion?.CourseTitle ?? S["your course"].Value;
        var instructors = (course?.Get<CoursePart>(nameof(CoursePart))?.Staff ?? [])
            .Where(member => member.Role == LearningConstants.CourseStaffRoles.Instructor && !string.IsNullOrEmpty(member.UserId))
            .Select(member => member.UserId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var instructor in instructors)
        {
            Send(enrollment, LearningConstants.NotificationTypes.ReviewNeeded, S["A completion in {0} needs review", title], S["A result a completed enrollment in {0} rests on changed after completion. Open the enrollment to decide what follows.", title], instructor, $"~/Admin/learning/enrollments/{enrollment.ItemId}");
        }
    }

    private async Task<string> TitleAsync(Enrollment enrollment)
        => (await _courseStructure.GetCourseAsync(enrollment.CourseId))?.DisplayText ?? enrollment.Completion?.CourseTitle ?? S["your course"].Value;

    private void Send(Enrollment enrollment, string type, string subject, string body)
        => Send(enrollment, type, subject, body, enrollment.UserId, $"~/learning/courses/{enrollment.CourseId}/play");

    private void Send(Enrollment enrollment, string type, string subject, string body, string userId, string url)
        => _sender.Send(userId, new LearningNotificationMessage
        {
            Subject = subject,
            Summary = body,
            TextBody = body,
            Type = type,
            ReferenceId = enrollment.ItemId,
            Url = url,
        });
}
