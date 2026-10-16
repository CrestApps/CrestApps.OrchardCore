using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// Cancels an automated email activity whose address is on the suppression list before anything is composed or sent:
/// it bounced, kept bouncing, or its owner reported the business's mail as spam.
/// </summary>
public sealed class EmailSuppressionScreener : IAutomatedActivityScreener
{
    private readonly IEmailSuppressionList _suppressions;

    public EmailSuppressionScreener(IEmailSuppressionList suppressions)
    {
        _suppressions = suppressions;
    }

    public string Channel => EmailChannelConstants.ChannelName;

    public async Task<AutomatedActivityScreeningResult> ScreenAsync(OmnichannelActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var suppression = await _suppressions.FindAsync(activity.PreferredDestination, cancellationToken);

        return suppression is null
            ? AutomatedActivityScreeningResult.Allow()
            : AutomatedActivityScreeningResult.Deny(
                OmnichannelConstants.TerminalReasons.AddressUndeliverable,
                $"The email address {activity.PreferredDestination} is on the suppression list ({suppression.Reason}), so no email was sent.");
    }
}
