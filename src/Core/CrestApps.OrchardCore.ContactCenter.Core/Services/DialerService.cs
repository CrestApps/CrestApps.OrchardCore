using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDialerService"/>. The Contact Center owns the
/// agent reservation, attempt limits, and compliance decisions; voice calling is routed through the
/// Voice Contact Center Call Router.
/// </summary>
public sealed class DialerService : IDialerService
{
    private readonly IActivityAssignmentService _assignmentService;
    private readonly IActivityReservationService _reservationService;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IVoiceContactCenterCallRouter _voiceCallRouter;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerService"/> class.
    /// </summary>
    /// <param name="assignmentService">The assignment service used to reserve agents and activities.</param>
    /// <param name="reservationService">The reservation service used to release failed attempts.</param>
    /// <param name="interactionManager">The interaction manager used to record attempts.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="voiceCallRouter">The voice call router.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp attempts.</param>
    /// <param name="logger">The logger instance.</param>
    public DialerService(
        IActivityAssignmentService assignmentService,
        IActivityReservationService reservationService,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IVoiceContactCenterCallRouter voiceCallRouter,
        IContactCenterEventPublisher publisher,
        IClock clock,
        ILogger<DialerService> logger)
    {
        _assignmentService = assignmentService;
        _reservationService = reservationService;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _voiceCallRouter = voiceCallRouter;
        _publisher = publisher;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> RunCycleAsync(DialerProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.Enabled || string.IsNullOrEmpty(profile.QueueId))
        {
            return 0;
        }

        if (profile.Mode is DialerMode.Manual or DialerMode.Preview)
        {
            return 0;
        }

        if (!_voiceCallRouter.CanRouteOutbound(profile.ProviderName))
        {
            _logger.LogWarning("No Contact Center voice provider can route outbound calls for dialer profile '{Profile}'.", profile.Name);

            return 0;
        }

        var attempted = 0;
        var maxAttemptsThisCycle = profile.Mode == DialerMode.Power
            ? Math.Max(profile.CallsPerAgent, 1)
            : 1;
        var started = 0;

        var reservation = await _assignmentService.AssignNextAsync(profile.QueueId, cancellationToken);

        while (reservation is not null && attempted < maxAttemptsThisCycle)
        {
            attempted++;

            if (await TryDialAsync(profile, reservation, cancellationToken))
            {
                started++;
            }

            if (attempted < maxAttemptsThisCycle)
            {
                reservation = await _assignmentService.AssignNextAsync(profile.QueueId, cancellationToken);
            }
        }

        return started;
    }

    private async Task<bool> TryDialAsync(DialerProfile profile, ActivityReservation reservation, CancellationToken cancellationToken)
    {
        var activity = await _activityManager.FindByIdAsync(reservation.ActivityItemId, cancellationToken);

        if (activity is null)
        {
            await _reservationService.CancelAsync(reservation.ItemId, cancellationToken);

            return false;
        }

        if (activity.Attempts >= profile.MaxAttempts || string.IsNullOrEmpty(activity.PreferredDestination))
        {
            activity.Status = ActivityStatus.Failed;
            await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
            await _reservationService.CancelAsync(reservation.ItemId, cancellationToken);

            return false;
        }

        var interaction = await _interactionManager.NewAsync(cancellationToken: cancellationToken);
        interaction.Channel = InteractionChannel.Voice;
        interaction.Direction = InteractionDirection.Outbound;
        interaction.Status = InteractionStatus.Created;
        interaction.ActivityItemId = activity.ItemId;
        interaction.QueueId = profile.QueueId;
        interaction.AgentId = reservation.AgentId;
        interaction.ProviderName = _voiceCallRouter.GetOutboundProviderName(profile.ProviderName);
        interaction.CustomerAddress = activity.PreferredDestination;
        await _interactionManager.CreateAsync(interaction, cancellationToken: cancellationToken);

        ContactCenterVoiceProviderResult result;

        try
        {
            result = await _voiceCallRouter.RouteOutboundAsync(new ContactCenterDialRequest
            {
                ActivityId = activity.ItemId,
                InteractionId = interaction.ItemId,
                AgentId = reservation.AgentId,
                QueueId = profile.QueueId,
                CampaignId = profile.CampaignId,
                Destination = activity.PreferredDestination,
                CallerId = profile.CallerId,
            }, profile.ProviderName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "The Voice Contact Center Call Router failed while dialing activity '{ActivityItemId}' for profile '{Profile}'.",
                activity.ItemId,
                profile.Name);

            result = new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                ErrorCode = "provider_exception",
                ErrorMessage = ex.Message,
            };
        }

        if (result.Succeeded && string.IsNullOrEmpty(result.ProviderCallId))
        {
            result = new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                ErrorCode = "missing_provider_call_id",
                ErrorMessage = "The Contact Center voice provider did not return a call identifier.",
            };
        }

        activity.Attempts++;
        activity.Status = result.Succeeded ? ActivityStatus.Dialing : ActivityStatus.Failed;
        await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);

        if (result.Succeeded)
        {
            interaction.Status = InteractionStatus.Ringing;
            interaction.ProviderInteractionId = result.ProviderCallId;
            interaction.StartedUtc = _clock.UtcNow;
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }
        else
        {
            interaction.Status = InteractionStatus.Failed;
            interaction.EndedUtc = _clock.UtcNow;
            interaction.TechnicalMetadata["providerErrorCode"] = result.ErrorCode;
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

            await _reservationService.CancelAsync(reservation.ItemId, cancellationToken);
        }

        await _publisher.PublishAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.DialerAttemptStarted,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(DialerProfile),
            AggregateId = profile.ItemId,
            SourceComponent = ContactCenterConstants.Components.Dialer,
        }, cancellationToken);

        return result.Succeeded;
    }
}
