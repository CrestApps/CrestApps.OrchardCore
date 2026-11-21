using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// Makes personal links as invitations of the survey's personal links collector, without sending them.
/// </summary>
public sealed class SurveyInvitationPersonalLinkFactory : ISurveyPersonalLinkFactory
{
    private readonly SurveyInvitationService _invitations;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationPersonalLinkFactory"/> class.
    /// </summary>
    /// <param name="invitations">The invitation service.</param>
    public SurveyInvitationPersonalLinkFactory(SurveyInvitationService invitations)
    {
        _invitations = invitations;
    }

    /// <inheritdoc />
    public async Task<string> CreateAsync(string surveyId, RecordRef? context, PartyRef? respondent, string email, DateTime? expiresUtc, CancellationToken cancellationToken = default)
    {
        var userId = respondent is { } party && party.Kind == PartyKinds.User ? party.Id : null;
        var result = await _invitations.CreateAsync(new SurveyInvitationRequest
        {
            SurveyId = surveyId,
            UserId = userId,
            Recipient = userId is null ? respondent : null,
            Email = email,
            Context = context,
            ExpiresUtc = expiresUtc,
        }, requireRecipient: false, cancellationToken);

        return result.Succeeded ? result.Link : null;
    }
}
