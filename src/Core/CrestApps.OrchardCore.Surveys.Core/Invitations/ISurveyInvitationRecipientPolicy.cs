using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// Decides whether an invitation may be sent to its e-mail address, just before it is sent: opt-outs, unsubscribes,
/// consent and do-not-contact lists. Every registered policy is asked; the first that refuses skips the invitation and
/// marks it <see cref="SurveyInvitationStatus.OptedOut"/>.
/// </summary>
public interface ISurveyInvitationRecipientPolicy
{
    /// <summary>
    /// Gets whether the invitation's address must not be contacted.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="invitation">The invitation, with the e-mail address it goes to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the address opted out and the invitation must not be sent.</returns>
    Task<bool> HasOptedOutAsync(Survey survey, SurveyInvitation invitation, CancellationToken cancellationToken = default);
}
