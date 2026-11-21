using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Learning.Assessments.Webhooks;

/// <summary>
/// Publishes quiz attempt webhook events after the request, with stable event ids. The payload carries scores, never
/// the answers or the key.
/// </summary>
internal sealed class AssessmentsWebhookObserver : IAttemptObserver
{
    public const string AttemptSubmitted = "learning.attempt.submitted";

    public const string AttemptGraded = "learning.attempt.graded";

    public Task SubmittedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => Publish(AttemptSubmitted, attempt.ItemId + ":submitted", attempt);

    public Task GradedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => Publish(AttemptGraded, attempt.ItemId + ":graded:" + attempt.ScoreHistory.Count, attempt);

    private static Task Publish(string eventName, string eventId, AssessmentAttempt attempt)
    {
        var payload = new
        {
            attemptId = attempt.ItemId,
            number = attempt.Number,
            userId = attempt.UserId,
            quizId = attempt.QuizId,
            courseId = attempt.CourseId,
            enrollmentId = attempt.EnrollmentId,
            status = attempt.Status.ToString(),
            score = attempt.Score,
            maxScore = attempt.MaxScore,
            percent = attempt.Percent,
            passed = attempt.Passed,
            autoSubmitted = attempt.AutoSubmitted,
            submittedUtc = attempt.SubmittedUtc,
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWebhookPublisher>().PublishAsync(eventName, payload, eventId));

        return Task.CompletedTask;
    }
}
