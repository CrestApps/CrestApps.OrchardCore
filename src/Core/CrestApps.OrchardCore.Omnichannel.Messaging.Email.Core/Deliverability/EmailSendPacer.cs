using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// Paces automated email outreach (campaign opening emails and their follow-ups) by each address's sending limits and
/// pauses, so a large campaign drains at the address's pace instead of failing or bursting.
/// </summary>
public sealed class EmailSendPacer : IOmnichannelSendPacer
{
    private readonly IEmailSendingGovernor _governor;

    public EmailSendPacer(IEmailSendingGovernor governor)
    {
        _governor = governor;
    }

    public string Channel => EmailChannelConstants.ChannelName;

    public async Task<DateTime?> GetBulkSendTimeAsync(OmnichannelChannelEndpoint address, bool reserveTurn, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        var settings = address.TryGet<EmailAddressSettings>(out var stored) ? stored : new EmailAddressSettings();
        var decision = await _governor.EvaluateAsync(address, settings, recipient: null, isBulk: true, reserveTurn, cancellationToken);

        return decision.IsAllowed ? null : decision.RetryAfterUtc;
    }
}
