using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Invitations;

/// <summary>
/// Asks the Outreach recipient policies, the opt-out, consent and do-not-contact checks every bulk send honors, whether
/// an e-mail invitation may be sent. Through Outreach's consent policy this covers contact and customer preferences,
/// address opt-outs and the unsubscribes of survey invitations.
/// </summary>
internal sealed class OutreachSurveyInvitationRecipientPolicy : ISurveyInvitationRecipientPolicy
{
    /// <summary>
    /// The owner type of the run the policies are asked about.
    /// </summary>
    public const string OwnerType = "SurveyInvitation";

    private readonly IEnumerable<IOutreachRecipientPolicy> _policies;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutreachSurveyInvitationRecipientPolicy"/> class.
    /// </summary>
    /// <param name="policies">The Outreach recipient policies.</param>
    public OutreachSurveyInvitationRecipientPolicy(IEnumerable<IOutreachRecipientPolicy> policies)
    {
        _policies = policies;
    }

    /// <inheritdoc />
    public async Task<bool> HasOptedOutAsync(Survey survey, SurveyInvitation invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(invitation);

        if (string.IsNullOrWhiteSpace(invitation.Email))
        {
            return false;
        }

        // The invitations of a survey are sent like one outreach run on the e-mail channel, so the policies decide
        // them exactly as they decide a campaign.
        var run = new OutreachRun
        {
            ItemId = survey.ItemId,
            Name = string.IsNullOrWhiteSpace(survey.Title) ? survey.Name : survey.Title,
            OwnerType = OwnerType,
            OwnerId = survey.ItemId,
            Channel = OutreachConstants.EmailSender,
            Sender = OutreachConstants.EmailSender,
            ConsentChannel = ConsentChannels.Email,
        };

        var recipient = new OutreachRecipient
        {
            Key = invitation.ItemId,
            Address = invitation.Email.Trim(),
            DisplayName = invitation.DisplayName,
        };

        foreach (var policy in _policies)
        {
            if (!string.IsNullOrEmpty(await policy.GetSkipReasonAsync(run, recipient, cancellationToken)))
            {
                return true;
            }
        }

        return false;
    }
}
