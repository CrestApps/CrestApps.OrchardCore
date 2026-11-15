using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Notifications;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Assessments.Notifications;

/// <summary>
/// Tells a learner when an attempt that needed a person to grade it is graded. An attempt scored automatically shows its
/// result at once, so it sends nothing.
/// </summary>
internal sealed class AssessmentsNotificationObserver : IAttemptObserver
{
    private readonly ShellSettings _shellSettings;

    internal readonly IStringLocalizer S;

    public AssessmentsNotificationObserver(ShellSettings shellSettings, IStringLocalizer<AssessmentsNotificationObserver> stringLocalizer)
    {
        _shellSettings = shellSettings;
        S = stringLocalizer;
    }

    public Task GradedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(attempt.GradedBy))
        {
            return Task.CompletedTask;
        }

        var prefix = string.IsNullOrEmpty(_shellSettings.RequestUrlPrefix) ? string.Empty : "/" + _shellSettings.RequestUrlPrefix;
        var title = string.IsNullOrWhiteSpace(attempt.QuizTitle) ? S["your quiz"].Value : attempt.QuizTitle;
        var message = new LearningNotificationMessage
        {
            Subject = S["{0} is graded", title],
            Summary = S["Your attempt at {0} is graded. Open it to see your result.", title],
            TextBody = S["Your attempt at {0} is graded. Open it to see your result.", title],
            Type = AssessmentsConstants.NotificationTypes.Graded,
            ReferenceId = attempt.ItemId,
            Url = $"{prefix}/learning/attempts/{attempt.ItemId}",
        };
        var userId = attempt.UserId;

        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var user = await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId);

            if (user is not null)
            {
                await services.GetRequiredService<INotificationService>().SendAsync(user, message);
            }
        });

        return Task.CompletedTask;
    }
}
