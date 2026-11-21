using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.AuditTrail;

/// <summary>
/// Records grading, score changes and quiz overrides in the Audit Trail.
/// </summary>
internal sealed class AssessmentsAuditTrailObserver : IAttemptObserver
{
    private readonly IAuditTrailManager _auditTrailManager;

    public AssessmentsAuditTrailObserver(IAuditTrailManager auditTrailManager)
    {
        _auditTrailManager = auditTrailManager;
    }

    public Task GradedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
    {
        // An attempt scored automatically is not a person's act; only manual grading is audited.
        if (string.IsNullOrEmpty(attempt.GradedBy))
        {
            return Task.CompletedTask;
        }

        return RecordAsync(AssessmentsAuditTrailEvents.Graded, attempt.ItemId, attempt.GradedBy, new AssessmentsAuditData
        {
            AttemptId = attempt.ItemId,
            Number = attempt.Number,
            UserId = attempt.UserId,
            QuizId = attempt.QuizId,
            Change = "Graded",
            To = attempt.Score,
        });
    }

    public Task ScoreChangedAsync(AssessmentAttempt attempt, AttemptScoreChange change, CancellationToken cancellationToken = default)
        => RecordAsync(AssessmentsAuditTrailEvents.ScoreChanged, attempt.ItemId, change.Actor, new AssessmentsAuditData
        {
            AttemptId = attempt.ItemId,
            Number = attempt.Number,
            UserId = attempt.UserId,
            QuizId = attempt.QuizId,
            Change = change.Kind,
            From = change.From,
            To = change.To,
            Reason = change.Reason,
        });

    public Task OverrideGrantedAsync(AssessmentOverride entry, CancellationToken cancellationToken = default)
        => RecordAsync(AssessmentsAuditTrailEvents.OverrideGranted, entry.ItemId, entry.Actor, new AssessmentsAuditData
        {
            UserId = entry.UserId,
            QuizId = entry.QuizId,
            Change = $"+{entry.ExtraMinutes} min, +{entry.ExtraAttempts} attempts",
            Reason = entry.Reason,
        });

    private Task RecordAsync(string name, string correlationId, string actor, AssessmentsAuditData data)
        => _auditTrailManager.RecordEventAsync(new AuditTrailContext<AssessmentsAuditData>(name, AssessmentsConstants.AuditTrailCategory, correlationId, actor, actor, data));
}
