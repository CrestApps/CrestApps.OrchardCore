using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDialerAttemptService"/>. Every attempt runs the
/// outbound compliance gate first; eligible attempts are routed through the Voice Contact Center Call
/// Router, while suppressed attempts release the reservation and record an auditable suppression event.
/// </summary>
public sealed class DialerAttemptService : IDialerAttemptService
{
    private readonly IDialerEligibilityService _eligibilityService;
    private readonly IActivityReservationService _reservationService;
    private readonly IDialerAttemptCompensationService _compensationService;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IVoiceContactCenterCallRouter _voiceCallRouter;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IProviderCommandStateService _providerCommandStateService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerAttemptService"/> class.
    /// </summary>
    /// <param name="eligibilityService">The compliance gate evaluated before every attempt.</param>
    /// <param name="reservationService">The reservation service used to release failed or suppressed attempts.</param>
    /// <param name="compensationService">The service used to release failed or suppressed attempts.</param>
    /// <param name="interactionManager">The interaction manager used to record attempts.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="voiceCallRouter">The voice call router.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="scopeExecutor">The executor used for compensation and post-commit command wake-up.</param>
    /// <param name="providerCommandStateService">The service used to persist provider command intent.</param>
    /// <param name="logger">The logger instance.</param>
    public DialerAttemptService(
        IDialerEligibilityService eligibilityService,
        IActivityReservationService reservationService,
        IDialerAttemptCompensationService compensationService,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IVoiceContactCenterCallRouter voiceCallRouter,
        IContactCenterEventPublisher publisher,
        IContactCenterScopeExecutor scopeExecutor,
        IProviderCommandStateService providerCommandStateService,
        ILogger<DialerAttemptService> logger)
    {
        _eligibilityService = eligibilityService;
        _reservationService = reservationService;
        _compensationService = compensationService;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _voiceCallRouter = voiceCallRouter;
        _publisher = publisher;
        _scopeExecutor = scopeExecutor;
        _providerCommandStateService = providerCommandStateService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> TryDialAsync(DialerProfile profile, ActivityReservation reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(reservation);

        var activity = await _activityManager.FindByIdAsync(reservation.ActivityItemId, cancellationToken);

        if (activity is null)
        {
            await _compensationService.CompensateAsync(reservation, removeFromQueue: true, cancellationToken);

            return false;
        }

        var eligibility = await _eligibilityService.EvaluateAsync(new DialerEligibilityContext
        {
            Profile = profile,
            Activity = activity,
        }, cancellationToken);

        if (!eligibility.IsEligible)
        {
            await SuppressAsync(profile, reservation, activity, eligibility, cancellationToken);

            return false;
        }

        var acceptedReservation = await _reservationService.AcceptAsync(reservation.ItemId, cancellationToken);

        if (acceptedReservation is null)
        {
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
        interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId] = interaction.ItemId;
        var request = new ContactCenterDialRequest
        {
            ActivityId = activity.ItemId,
            InteractionId = interaction.ItemId,
            CommandId = interaction.ItemId,
            AgentId = reservation.AgentId,
            QueueId = profile.QueueId,
            CampaignId = profile.CampaignId,
            Destination = activity.PreferredDestination,
            CallerId = profile.CallerId,
            Metadata = new Dictionary<string, string>
            {
                [ContactCenterConstants.CommandMetadata.CommandId] = interaction.ItemId,
                [TelephonyConstants.RequestMetadata.IdempotencyKey] = interaction.ItemId,
            },
        };

        try
        {
            activity.Attempts++;
            await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
            await _interactionManager.CreateAsync(interaction, cancellationToken: cancellationToken);
            await _publisher.PublishAsync(new InteractionEvent
            {
                EventType = ContactCenterConstants.Events.DialerAttemptStarted,
                InteractionId = interaction.ItemId,
                AggregateType = nameof(DialerProfile),
                AggregateId = profile.ItemId,
                SourceComponent = ContactCenterConstants.Components.Dialer,
                IdempotencyKey = $"dialer-attempt:{interaction.ItemId}",
            }, cancellationToken);
            await _providerCommandStateService.RegisterAsync(new ProviderCommandRegistration
            {
                CommandId = interaction.ItemId,
                ProviderName = interaction.ProviderName,
                CommandType = ProviderCommandType.Dial,
                ActivityItemId = activity.ItemId,
                InteractionId = interaction.ItemId,
                ReservationId = acceptedReservation.ItemId,
                DialerProfileId = profile.ItemId,
                RequestPayload = JsonSerializer.Serialize(request),
            }, cancellationToken);
        }
        catch
        {
            await _scopeExecutor.ExecuteAsync<IDialerAttemptCompensationService>(service =>
                service.CompensateAsync(acceptedReservation, removeFromQueue: true, CancellationToken.None));

            throw;
        }

        _scopeExecutor.ScheduleAfterCommit<IProviderCommandProcessor>(processor =>
            processor.DispatchAsync(interaction.ItemId, CancellationToken.None));

        return true;
    }

    private async Task SuppressAsync(
        DialerProfile profile,
        ActivityReservation reservation,
        OmnichannelActivity activity,
        DialerEligibilityResult eligibility,
        CancellationToken cancellationToken)
    {
        var status = ResolveSuppressedStatus(eligibility.Reason);

        if (status.HasValue)
        {
            activity.Status = status.Value;
            await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
        }

        await _compensationService.CompensateAsync(reservation, removeFromQueue: status.HasValue, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Suppressed outbound attempt for activity '{ActivityItemId}' on profile '{Profile}': {Reason}.",
                OperationalLogRedactor.Pseudonymize(activity.ItemId, OperationalLogIdentifierCategory.Activity),
                profile.Name,
                eligibility.Reason);
        }

        var data = new DialerSuppressionEventData
        {
            ProfileItemId = profile.ItemId,
            ActivityItemId = activity.ItemId,
            Reason = eligibility.Reason,
            Description = eligibility.Description,
            Destination = activity.PreferredDestination,
        };

        var suppressionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.DialSuppressed,
            AggregateType = nameof(OmnichannelActivity),
            AggregateId = activity.ItemId,
            SourceComponent = ContactCenterConstants.Components.Dialer,
        };

        suppressionEvent.SetData(data);

        await _publisher.PublishAsync(suppressionEvent, cancellationToken);
    }

    private static ActivityStatus? ResolveSuppressedStatus(DialerSuppressionReason reason)
    {
        return reason switch
        {
            DialerSuppressionReason.NoDestination => ActivityStatus.Failed,
            DialerSuppressionReason.MaxAttemptsReached => ActivityStatus.Failed,
            DialerSuppressionReason.DoNotCall => ActivityStatus.Cancelled,
            DialerSuppressionReason.NationalDoNotCallRegistry => ActivityStatus.Cancelled,
            _ => null,
        };
    }
}
