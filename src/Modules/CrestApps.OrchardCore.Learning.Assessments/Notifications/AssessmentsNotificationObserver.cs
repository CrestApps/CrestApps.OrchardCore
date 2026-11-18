using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments.Notifications;

/// <summary>
/// Tells a learner when an attempt that needed a person to grade it is graded. An attempt scored automatically shows its
/// result at once, so it sends nothing.
/// </summary>
internal sealed class AssessmentsNotificationObserver : IAttemptObserver
{
    private readonly ILearningNotificationSender _sender;

    internal readonly IStringLocalizer S;

    public AssessmentsNotificationObserver(ILearningNotificationSender sender, IStringLocalizer<AssessmentsNotificationObserver> stringLocalizer)
    {
        _sender = sender;
        S = stringLocalizer;
    }

    public Task GradedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(attempt.GradedBy))
        {
            return Task.CompletedTask;
        }

        var title = string.IsNullOrWhiteSpace(attempt.QuizTitle) ? S["your quiz"].Value : attempt.QuizTitle;

        _sender.Send(attempt.UserId, new LearningNotificationMessage
        {
            Subject = S["{0} is graded", title],
            Summary = S["Your attempt at {0} is graded. Open it to see your result.", title],
            TextBody = S["Your attempt at {0} is graded. Open it to see your result.", title],
            Type = AssessmentsConstants.NotificationTypes.Graded,
            ReferenceId = attempt.ItemId,
            Url = $"~/learning/attempts/{attempt.ItemId}",
        });

        return Task.CompletedTask;
    }
}
