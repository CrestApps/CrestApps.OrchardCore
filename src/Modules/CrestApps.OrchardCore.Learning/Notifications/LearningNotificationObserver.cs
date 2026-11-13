using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
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

    private async Task<string> TitleAsync(Enrollment enrollment)
        => (await _courseStructure.GetCourseAsync(enrollment.CourseId))?.DisplayText ?? enrollment.Completion?.CourseTitle ?? S["your course"].Value;

    private void Send(Enrollment enrollment, string type, string subject, string body)
    {
        var prefix = string.IsNullOrEmpty(_shellSettings.RequestUrlPrefix) ? string.Empty : "/" + _shellSettings.RequestUrlPrefix;
        var message = new LearningNotificationMessage
        {
            Subject = subject,
            Summary = body,
            TextBody = body,
            Type = type,
            ReferenceId = enrollment.ItemId,
            Url = $"{prefix}/learning/courses/{enrollment.CourseId}/play",
        };

        var userId = enrollment.UserId;

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
