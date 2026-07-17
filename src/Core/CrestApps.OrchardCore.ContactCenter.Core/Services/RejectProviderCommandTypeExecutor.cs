using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Handles durable provider commands that reject a ringing call.
/// </summary>
public sealed class RejectProviderCommandTypeExecutor : ProviderCallActionCommandTypeExecutor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RejectProviderCommandTypeExecutor"/> class.
    /// </summary>
    /// <param name="telephonyServices">The optional telephony services used to execute the provider action.</param>
    /// <param name="interactionManager">The interaction manager used to validate and project linked interactions.</param>
    /// <param name="queueService">The queue service used to restore live work after a definitive action failure.</param>
    /// <param name="activityManager">The CRM activity manager used to restore live work after a definitive action failure.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp projections.</param>
    /// <param name="callControlAuthorizationService">The shared call-control authorization boundary.</param>
    public RejectProviderCommandTypeExecutor(
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
    public override ProviderCommandType CommandType => ProviderCommandType.Reject;

    /// <inheritdoc/>
    protected override string ActionName => "Reject";

    /// <inheritdoc/>
    protected override string ErrorCodePrefix => "reject";

    /// <inheritdoc/>
    protected override Task<TelephonyResult> ExecuteTelephonyAsync(
        ITelephonyService telephonyService,
        CallReference call,
        CancellationToken cancellationToken)
    {
        return telephonyService.RejectAsync(call, cancellationToken);
    }
}
