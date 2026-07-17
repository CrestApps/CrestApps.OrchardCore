using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Handles durable provider commands that send a ringing call to voicemail.
/// </summary>
public sealed class SendToVoicemailProviderCommandTypeExecutor : ProviderCallActionCommandTypeExecutor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SendToVoicemailProviderCommandTypeExecutor"/> class.
    /// </summary>
    /// <param name="telephonyServices">The optional telephony services used to execute the provider action.</param>
    /// <param name="interactionManager">The interaction manager used to validate and project linked interactions.</param>
    /// <param name="queueService">The queue service used to restore live work after a definitive action failure.</param>
    /// <param name="activityManager">The CRM activity manager used to restore live work after a definitive action failure.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp projections.</param>
    /// <param name="callControlAuthorizationService">The shared call-control authorization boundary.</param>
    public SendToVoicemailProviderCommandTypeExecutor(
        IEnumerable<ITelephonyService> telephonyServices,
        IInteractionManager interactionManager,
        IActivityQueueService queueService,
        IOmnichannelActivityManager activityManager,
        IContactCenterEventPublisher publisher,
        IClock clock,
        ICallControlAuthorizationService callControlAuthorizationService = null)
        : base(telephonyServices, interactionManager, queueService, activityManager, publisher, clock, callControlAuthorizationService)
    {
    }

    /// <inheritdoc/>
    public override ProviderCommandType CommandType => ProviderCommandType.SendToVoicemail;

    /// <inheritdoc/>
    protected override string ActionName => "SendToVoicemail";

    /// <inheritdoc/>
    protected override string ErrorCodePrefix => "voicemail";

    /// <inheritdoc/>
    protected override Task<TelephonyResult> ExecuteTelephonyAsync(
        ITelephonyService telephonyService,
        CallReference call,
        CancellationToken cancellationToken)
    {
        return telephonyService.SendToVoicemailAsync(call, cancellationToken);
    }
}
