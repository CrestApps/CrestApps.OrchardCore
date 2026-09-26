using CrestApps.OrchardCore.ContactCenter.Core.Models;
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
    /// <param name="workStateService">The routing-owned work state service.</param>
    /// <param name="activityWriter">The writer used to apply CRM activity changes outside the routing transaction.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp projections.</param>
    /// <param name="callControlAuthorizationService">The shared call-control authorization boundary.</param>
    public SendToVoicemailProviderCommandTypeExecutor(
        IEnumerable<ITelephonyService> telephonyServices,
        IInteractionManager interactionManager,
        IAgentProfileManager agentProfileManager,
        IActivityQueueService queueService,
        IContactCenterWorkStateService workStateService,
        IContactCenterActivityWriter activityWriter,
        IContactCenterEventPublisher publisher,
        IClock clock,
        ICallControlAuthorizationService callControlAuthorizationService)
        : base(
            telephonyServices,
            interactionManager,
            agentProfileManager,
            queueService,
            workStateService,
            activityWriter,
            publisher,
            clock,
            callControlAuthorizationService)
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
