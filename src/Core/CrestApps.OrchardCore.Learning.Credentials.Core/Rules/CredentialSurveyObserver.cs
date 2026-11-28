using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Rules;

/// <summary>
/// The quiz passed rule. Only an attempt the site scored counts (a completed response to the quiz's survey that passed),
/// made in an enrollment that counts as evidence; a preview attempt is made in no enrollment and never counts. When the
/// attempt is voided or deleted, or its score changes so that it is no longer a pass, the credentials it awarded are revoked.
/// </summary>
public sealed class CredentialSurveyObserver : ISurveyResponseObserver
{
    private readonly CredentialRuleTrigger _trigger;
    private readonly IEnrollmentStore _enrollments;
    private readonly CredentialSourceRevoker _revoker;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialSurveyObserver"/> class.
    /// </summary>
    /// <param name="trigger">The rule trigger.</param>
    /// <param name="enrollments">The enrollments.</param>
    /// <param name="revoker">Revokes the awards that rest on an attempt that is no longer a pass.</param>
    /// <param name="stringLocalizer">The localizer of the revocation reason.</param>
    public CredentialSurveyObserver(
        CredentialRuleTrigger trigger,
        IEnrollmentStore enrollments,
        CredentialSourceRevoker revoker,
        IStringLocalizer<CredentialSurveyObserver> stringLocalizer)
    {
        _trigger = trigger;
        _enrollments = enrollments;
        _revoker = revoker;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public Task CompletedAsync(SurveyResponseEventContext context)
        => PassedAsync(context);

    /// <inheritdoc />
    public Task GradedAsync(SurveyResponseEventContext context)
        => PassedAsync(context);

    /// <inheritdoc />
    public Task ScoreChangedAsync(SurveyResponseEventContext context)
        => ChangedAsync(context);

    /// <inheritdoc />
    public Task VoidedAsync(SurveyResponseEventContext context)
        => ChangedAsync(context);

    /// <inheritdoc />
    public Task DeletedAsync(SurveyResponseEventContext context)
    {
        var response = context?.Response;

        // A deleted attempt is gone, so nothing may rest on it any longer: revoke as for a voided one.
        return response is null || string.IsNullOrEmpty(response.ItemId)
            ? Task.CompletedTask
            : _revoker.RevokeAsync(CredentialsConstants.RuleKinds.QuizPassed, response.ItemId, context.ActorId, S["The quiz attempt that this credential rests on was deleted."]);
    }

    private Task ChangedAsync(SurveyResponseEventContext context)
    {
        var response = context?.Response;

        if (response is null || string.IsNullOrEmpty(response.ItemId))
        {
            return Task.CompletedTask;
        }

        if (response.Status == SurveyResponseStatus.Voided || response.Passed != true)
        {
            var reason = response.Status == SurveyResponseStatus.Voided
                ? S["The quiz attempt that this credential rests on was voided."]
                : S["The quiz attempt that this credential rests on is no longer a pass."];

            return _revoker.RevokeAsync(CredentialsConstants.RuleKinds.QuizPassed, response.ItemId, context.ActorId, reason);
        }

        return PassedAsync(context);
    }

    private async Task PassedAsync(SurveyResponseEventContext context)
    {
        var response = context?.Response;

        if (response is not { Passed: true, Status: SurveyResponseStatus.Completed, IsTest: false } || !LearningSurveyContexts.IsContentOwner(context.Survey?.Owner))
        {
            return;
        }

        var enrollmentId = LearningSurveyContexts.EnrollmentIdOf(response);

        if (string.IsNullOrEmpty(enrollmentId) || !CompletionEvidence.CountsAsEvidence(await _enrollments.FindByIdAsync(enrollmentId)))
        {
            return;
        }

        await _trigger.TriggerAsync(CredentialsConstants.RuleKinds.QuizPassed, context.Survey.Owner.Value.Id, response.UserId, response.ItemId);
    }
}
