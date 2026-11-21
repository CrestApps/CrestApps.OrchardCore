using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.AuditTrail;

/// <summary>
/// The Audit Trail events of quiz grading.
/// </summary>
internal sealed class AssessmentsAuditTrailEvents : IConfigureOptions<AuditTrailOptions>
{
    public const string Graded = "LearningAttemptGraded";
    public const string ScoreChanged = "LearningAttemptScoreChanged";
    public const string OverrideGranted = "LearningQuizOverrideGranted";

    public void Configure(AuditTrailOptions options)
    {
        options.For<AssessmentsAuditTrailEvents>(AssessmentsConstants.AuditTrailCategory, S => S["Learning assessments"])
            .WithEvent(Graded, S => S["Quiz attempt graded"], S => S["A person graded the answers of a quiz attempt."], enableByDefault: true)
            .WithEvent(ScoreChanged, S => S["Quiz score changed"], S => S["A person or a workflow overrode, regraded or voided a quiz attempt, with a reason."], enableByDefault: true)
            .WithEvent(OverrideGranted, S => S["Quiz override given"], S => S["A person gave a learner extra time, extra attempts or another window for a quiz, with a reason."], enableByDefault: true);
    }
}
