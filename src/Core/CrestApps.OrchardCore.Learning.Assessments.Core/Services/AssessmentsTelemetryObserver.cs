using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Core.Telemetry;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Counts quiz attempts on the Learning meter: started, submitted (by the learner or the deadline), graded
/// (automatically or by staff) and score changes.
/// </summary>
internal sealed class AssessmentsTelemetryObserver : IAttemptObserver
{
    public Task StartedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
    {
        LearningTelemetry.Add(LearningTelemetry.AttemptsStarted, LearningTelemetry.ByTag, "learner");

        return Task.CompletedTask;
    }

    public Task SubmittedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
    {
        LearningTelemetry.Add(LearningTelemetry.AttemptsSubmitted, LearningTelemetry.ByTag, attempt.AutoSubmitted ? "deadline" : "learner");

        return Task.CompletedTask;
    }

    public Task GradedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
    {
        LearningTelemetry.Add(LearningTelemetry.AttemptsGraded, LearningTelemetry.ByTag, string.IsNullOrEmpty(attempt.GradedBy) ? "automatic" : "staff");

        return Task.CompletedTask;
    }

    public Task ScoreChangedAsync(AssessmentAttempt attempt, AttemptScoreChange change, CancellationToken cancellationToken = default)
    {
        LearningTelemetry.Add(LearningTelemetry.ScoreChanges, LearningTelemetry.ChangeTag, change.Kind);

        return Task.CompletedTask;
    }
}
