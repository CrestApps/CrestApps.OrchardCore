using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Rules;

/// <summary>
/// The quiz passed rule. Only an attempt the site scored counts, made in an enrollment that counts as evidence; a
/// preview attempt is made in no enrollment and never counts. When the attempt is voided or its score changes so that
/// it is no longer a pass, the credentials it awarded are revoked.
/// </summary>
public sealed class CredentialAttemptObserver : IAttemptObserver
{
    private readonly CredentialRuleTrigger _trigger;
    private readonly IEnrollmentStore _enrollments;
    private readonly CredentialSourceRevoker _revoker;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialAttemptObserver"/> class.
    /// </summary>
    /// <param name="trigger">The rule trigger.</param>
    /// <param name="enrollments">The enrollments.</param>
    /// <param name="revoker">Revokes the awards that rest on an attempt that is no longer a pass.</param>
    /// <param name="stringLocalizer">The localizer of the revocation reason.</param>
    public CredentialAttemptObserver(
        CredentialRuleTrigger trigger,
        IEnrollmentStore enrollments,
        CredentialSourceRevoker revoker,
        IStringLocalizer<CredentialAttemptObserver> stringLocalizer)
    {
        _trigger = trigger;
        _enrollments = enrollments;
        _revoker = revoker;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task GradedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => PassedAsync(attempt, cancellationToken);

    /// <inheritdoc/>
    public Task ScoreChangedAsync(AssessmentAttempt attempt, AttemptScoreChange change, CancellationToken cancellationToken = default)
    {
        if (attempt is null || string.IsNullOrEmpty(attempt.ItemId))
        {
            return Task.CompletedTask;
        }

        if (attempt.Status == AttemptStatus.Voided || attempt.Passed != true)
        {
            var reason = attempt.Status == AttemptStatus.Voided
                ? S["The quiz attempt that this credential rests on was voided."]
                : S["The quiz attempt that this credential rests on is no longer a pass."];

            return _revoker.RevokeAsync(CredentialsConstants.RuleKinds.QuizPassed, attempt.ItemId, change?.Actor, reason, cancellationToken);
        }

        return PassedAsync(attempt, cancellationToken);
    }

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
