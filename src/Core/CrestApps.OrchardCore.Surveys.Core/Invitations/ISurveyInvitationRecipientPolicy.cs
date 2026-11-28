using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// Decides whether an invitation may be sent to its e-mail address, just before it is sent: opt-outs, unsubscribes,
/// consent and do-not-contact lists. Every registered policy is asked; the first that refuses skips the invitation and
/// marks it <see cref="SurveyInvitationStatus.OptedOut"/>. A policy that can't tell (its list can't be read) holds the
/// invitation back without changing it, so it goes out once the list can be read again.
/// </summary>
public interface ISurveyInvitationRecipientPolicy
{
    /// <summary>
    /// Gets whether the invitation's address must not be contacted.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="invitation">The invitation, with the e-mail address it goes to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the address opted out and the invitation must not be sent,
    /// <see langword="false"/> when it may be sent, or <see langword="null"/> when the policy can't tell right now.</returns>
    Task<bool?> HasOptedOutAsync(Survey survey, SurveyInvitation invitation, CancellationToken cancellationToken = default);
}
