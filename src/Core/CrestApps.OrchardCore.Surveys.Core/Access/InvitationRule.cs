using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// An invitation gives one response: an expired, opted-out or used invitation is refused, and an invitation collector
/// starts nothing without an invitation.
/// </summary>
public sealed class InvitationRule : ISurveyAccessRule
{
    /// <inheritdoc />
    public string Name => "Invitation";

    /// <inheritdoc />
    public int Order => SurveyAccessRuleOrder.Invitation;

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
    {
        var invitation = context.Invitation;

        if (context.IsTest)
        {
            return Task.FromResult(SurveyAccessDecision.Allow);
        }

        if (invitation is null)
        {
            // An invitation collector's link is a start through an invitation only: its token on its own is not a public
            // link, or an invitee could start more responses than the one an invitation gives.
            return Task.FromResult(context.Collector?.Kind == SurveyCollectorKinds.Invitation
                ? SurveyAccessDecision.Deny(SurveyErrorCode.InvitationInvalid)
                : SurveyAccessDecision.Allow);
        }

        if (!string.Equals(invitation.SurveyId, context.Survey.ItemId, StringComparison.Ordinal)
            || invitation.Status is SurveyInvitationStatus.Expired or SurveyInvitationStatus.OptedOut or SurveyInvitationStatus.Bounced
            || (invitation.ExpiresUtc is { } expires && context.UtcNow >= expires))
        {
            return Task.FromResult(SurveyAccessDecision.Deny(SurveyErrorCode.InvitationInvalid));
        }

        return Task.FromResult(invitation.Status == SurveyInvitationStatus.Completed
            ? SurveyAccessDecision.Deny(SurveyErrorCode.InvitationUsed)
            : SurveyAccessDecision.Allow);
    }

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckCompleteAsync(SurveyAccessContext context)
        => Task.FromResult(context.Invitation is { Status: SurveyInvitationStatus.Completed } && !context.IsTest
            ? SurveyAccessDecision.Deny(SurveyErrorCode.InvitationUsed)
            : SurveyAccessDecision.Allow);
}
