namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Creates and sends personal invitations. The Survey Invitations feature implements it.
/// </summary>
public interface ISurveyInvitationService
{
    /// <summary>
    /// Creates an invitation for one recipient and sends it.
    /// </summary>
    /// <param name="request">Who to invite to which survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyInvitationResult> InviteAsync(SurveyInvitationRequest request, CancellationToken cancellationToken = default);
}
