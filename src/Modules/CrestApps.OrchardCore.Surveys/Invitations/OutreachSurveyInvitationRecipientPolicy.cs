using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Invitations;

/// <summary>
/// Asks the consent service and the Outreach recipient policies, the opt-out, consent and do-not-contact checks every
/// bulk send honors, whether an e-mail invitation may be sent. The consent service covers contact and customer
/// preferences, address opt-outs and the unsubscribes of survey invitations. When a consent source can't tell (the
/// unsubscribe list's key can't be read, for one), the invitation waits, unchanged: an invitation is never sent on an
/// unknown answer.
/// </summary>
internal sealed class OutreachSurveyInvitationRecipientPolicy : ISurveyInvitationRecipientPolicy
{
    /// <summary>
    /// The owner type of the run the policies are asked about.
    /// </summary>
    public const string OwnerType = "SurveyInvitation";

    private readonly IConsentService _consentService;
    private readonly IEnumerable<IOutreachRecipientPolicy> _policies;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutreachSurveyInvitationRecipientPolicy"/> class.
    /// </summary>
    /// <param name="consentService">The consent service.</param>
    /// <param name="policies">The Outreach recipient policies.</param>
    public OutreachSurveyInvitationRecipientPolicy(IConsentService consentService, IEnumerable<IOutreachRecipientPolicy> policies)
    {
        _consentService = consentService;
        _policies = policies;
    }

    /// <inheritdoc />
    public async Task<bool?> HasOptedOutAsync(Survey survey, SurveyInvitation invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(invitation);

        if (string.IsNullOrWhiteSpace(invitation.Email))
        {
            return false;
        }

        var address = invitation.Email.Trim();

        // The consent service is asked here rather than through Outreach's consent policy, which lets the other
        // sources decide when one can't tell; an invitation waits instead.
        var consent = await _consentService.GetAsync(new ConsentQuery
        {
            Channel = ConsentChannels.Email,
            Address = address,
        }, cancellationToken);

        if (consent.HasOptedOut)
        {
            return true;
        }

        if (consent.IsUnknown)
        {
            return null;
        }

        // The invitations of a survey are sent like one outreach run on the e-mail channel, so the other policies decide
        // them exactly as they decide a campaign. The run names no consent channel: consent was asked above.
        var run = new OutreachRun
        {
            ItemId = survey.ItemId,
            Name = string.IsNullOrWhiteSpace(survey.Title) ? survey.Name : survey.Title,
            OwnerType = OwnerType,
            OwnerId = survey.ItemId,
            Channel = OutreachConstants.EmailSender,
            Sender = OutreachConstants.EmailSender,
        };

        var recipient = new OutreachRecipient
        {
            Key = invitation.ItemId,
            Address = address,
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
