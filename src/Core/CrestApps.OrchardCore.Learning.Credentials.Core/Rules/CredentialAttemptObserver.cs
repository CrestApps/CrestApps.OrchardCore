using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Rules;

/// <summary>
/// The quiz passed rule. Only an attempt the site scored counts, made in an enrollment that counts as evidence; a
/// preview attempt is made in no enrollment and never counts.
/// </summary>
public sealed class CredentialAttemptObserver : IAttemptObserver
{
    private readonly CredentialRuleTrigger _trigger;
    private readonly IEnrollmentStore _enrollments;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialAttemptObserver"/> class.
    /// </summary>
    /// <param name="trigger">The rule trigger.</param>
    /// <param name="enrollments">The enrollments.</param>
    public CredentialAttemptObserver(CredentialRuleTrigger trigger, IEnrollmentStore enrollments)
    {
        _trigger = trigger;
        _enrollments = enrollments;
    }

    /// <inheritdoc/>
    public Task GradedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => PassedAsync(attempt, cancellationToken);

    /// <inheritdoc/>
    public Task ScoreChangedAsync(AssessmentAttempt attempt, AttemptScoreChange change, CancellationToken cancellationToken = default)
        => PassedAsync(attempt, cancellationToken);

    private async Task PassedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken)
    {
        if (attempt is not { Passed: true, Status: AttemptStatus.Submitted or AttemptStatus.Graded } || string.IsNullOrEmpty(attempt.EnrollmentId))
        {
            return;
        }

        if (!CompletionEvidence.CountsAsEvidence(await _enrollments.FindByIdAsync(attempt.EnrollmentId, cancellationToken)))
        {
            return;
        }

        await _trigger.TriggerAsync(CredentialsConstants.RuleKinds.QuizPassed, attempt.QuizId, attempt.UserId, attempt.ItemId, cancellationToken);
    }
}
