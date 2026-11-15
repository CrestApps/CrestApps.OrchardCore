using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Notifications;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Notifications;

/// <summary>
/// Tells learners, through Orchard Core notifications, that they are enrolled, that a course is due soon and that it is
/// overdue. Each notice is a registered notification type, so a learner or the site can switch it off; it is sent after
/// the change is committed.
/// </summary>
internal sealed class LearningNotificationObserver : IEnrollmentObserver
{
    private readonly ICourseStructureService _courseStructure;
    private readonly ShellSettings _shellSettings;

    internal readonly IStringLocalizer S;

    public LearningNotificationObserver(
        ICourseStructureService courseStructure,
        ShellSettings shellSettings,
        IStringLocalizer<LearningNotificationObserver> stringLocalizer)
    {
        _courseStructure = courseStructure;
        _shellSettings = shellSettings;
        S = stringLocalizer;
    }

    public async Task EnrolledAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        var title = await TitleAsync(enrollment);

        Send(enrollment, LearningConstants.NotificationTypes.Enrolled, S["You are enrolled in {0}", title], S["You can start the course {0} now.", title]);
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
            Send(enrollment, LearningConstants.NotificationTypes.ReviewNeeded, S["A completion in {0} needs review", title], S["A result a completed enrollment in {0} rests on changed after completion. Open the enrollment to decide what follows.", title], instructor, $"/Admin/learning/enrollments/{enrollment.ItemId}");
        }
    }

    private async Task<string> TitleAsync(Enrollment enrollment)
        => (await _courseStructure.GetCourseAsync(enrollment.CourseId))?.DisplayText ?? enrollment.Completion?.CourseTitle ?? S["your course"].Value;

    private void Send(Enrollment enrollment, string type, string subject, string body)
        => Send(enrollment, type, subject, body, enrollment.UserId, $"/learning/courses/{enrollment.CourseId}/play");

    private void Send(Enrollment enrollment, string type, string subject, string body, string userId, string path)
    {
        var prefix = string.IsNullOrEmpty(_shellSettings.RequestUrlPrefix) ? string.Empty : "/" + _shellSettings.RequestUrlPrefix;
        var message = new LearningNotificationMessage
        {
            Subject = subject,
            Summary = body,
            TextBody = body,
            Type = type,
            ReferenceId = enrollment.ItemId,
            Url = prefix + path,
        };

        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var user = await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId);

            if (user is not null)
            {
                await services.GetRequiredService<INotificationService>().SendAsync(user, message);
            }
        });
    }
}
