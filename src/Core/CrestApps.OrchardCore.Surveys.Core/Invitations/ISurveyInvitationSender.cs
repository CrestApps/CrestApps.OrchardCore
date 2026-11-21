using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// Delivers an invitation or a reminder: a notification to a user, an e-mail to anyone else. The module provides it.
/// </summary>
public interface ISurveyInvitationSender
{
    /// <summary>
    /// Delivers an invitation or a reminder.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="collector">The invitation collector.</param>
    /// <param name="invitation">The invitation.</param>
    /// <param name="link">The personal link.</param>
    /// <param name="isReminder">Whether this is a reminder.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when it was delivered.</returns>
    Task<bool> SendAsync(Survey survey, SurveyCollector collector, SurveyInvitation invitation, string link, bool isReminder, CancellationToken cancellationToken = default);
}
