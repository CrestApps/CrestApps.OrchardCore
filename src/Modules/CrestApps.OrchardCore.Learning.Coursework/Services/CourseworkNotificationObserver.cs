using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Coursework.Services;

/// <summary>
/// Tells a learner, through the Notifications module, when their coursework is graded, graded again or returned for
/// another submission. Each notice links to the coursework in the course player.
/// </summary>
internal sealed class CourseworkNotificationObserver : ISubmissionObserver
{
    private readonly ILearningNotificationSender _sender;

    internal readonly IStringLocalizer S;

    public CourseworkNotificationObserver(ILearningNotificationSender sender, IStringLocalizer<CourseworkNotificationObserver> stringLocalizer)
    {
        _sender = sender;
        S = stringLocalizer;
    }

    public Task GradedAsync(Submission submission, SubmissionGrade previous, CancellationToken cancellationToken = default)
    {
        var title = Title(submission);

        Send(submission, CourseworkConstants.NotificationTypes.Graded,
            previous is null ? S["{0} is graded", title] : S["The grade of {0} changed", title],
            previous is null
                ? S["Your submission of {0} is graded. Open it to see your grade and feedback.", title]
                : S["Your submission of {0} was graded again. Open it to see your grade and feedback.", title]);

        return Task.CompletedTask;
    }

    public Task ReturnedAsync(Submission submission, CancellationToken cancellationToken = default)
    {
        var title = Title(submission);

        Send(submission, CourseworkConstants.NotificationTypes.Returned,
            S["{0} was returned to you", title],
            S["Your submission of {0} was returned so you can submit it again. Open it to see the feedback.", title]);

        return Task.CompletedTask;
    }

    private string Title(Submission submission)
        => string.IsNullOrWhiteSpace(submission?.CourseworkTitle) ? S["your coursework"].Value : submission.CourseworkTitle;

    private void Send(Submission submission, string type, string subject, string body)
    {
        if (string.IsNullOrEmpty(submission?.UserId))
        {
            return;
        }

        _sender.Send(submission.UserId, new LearningNotificationMessage
        {
            Subject = subject,
            Summary = body,
            TextBody = body,
            Type = type,
            ReferenceId = submission.ItemId,
            Url = $"~/learning/courses/{submission.CourseId}/play/{submission.CourseworkId}",
        });
    }
}
