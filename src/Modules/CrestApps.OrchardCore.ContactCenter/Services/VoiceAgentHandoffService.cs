using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The phone implementation of <see cref="IOmnichannelHandoffService"/>. When an automated voice conversation
/// escalates, it seats the still-connected caller into the target Contact Center queue and offers the call to
/// the next available agent — reusing the same enqueue-and-offer pipeline inbound calls use, so presence,
/// reservation, and voicemail handling all apply. The live call is represented by a Contact Center interaction
/// carrying the provider call identifier so the offer/connect pipeline can bridge an agent onto it.
/// </summary>
public sealed class VoiceAgentHandoffService : IOmnichannelHandoffService
{
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IActivityQueueService _queueService;
    private readonly IVoiceQueueOfferService _offerService;
    private readonly IActivityQueueManager _queueManager;
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceAgentHandoffService"/> class.
    /// </summary>
    public VoiceAgentHandoffService(
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IContactCenterWorkStateService workStateService,
        IActivityQueueService queueService,
        IVoiceQueueOfferService offerService,
        IActivityQueueManager queueManager,
        IClock clock,
        IServiceProvider serviceProvider,
        ILogger<VoiceAgentHandoffService> logger)
    {
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _workStateService = workStateService;
        _queueService = queueService;
        _offerService = offerService;
        _queueManager = queueManager;
        _clock = clock;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool CanHandle(string channel)
        => string.Equals(channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<OmnichannelHandoffResult> RequestHandoffAsync(OmnichannelHandoffRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Activity is null)
        {
            return OmnichannelHandoffResult.Failure("A handoff requires an activity.");
        }

        var queueId = string.IsNullOrWhiteSpace(request.TargetQueueId) ? null : request.TargetQueueId.Trim();

        if (string.IsNullOrEmpty(queueId))
        {
            return OmnichannelHandoffResult.Failure("A handoff requires a target queue.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            return OmnichannelHandoffResult.Failure("A phone handoff requires the live provider call identifier.");
        }

        // Re-load through the activity manager so we mutate and persist the same instance the routing pipeline
        // reads, rather than the instance the provider handler loaded through its own store.
        var activity = await _activityManager.FindByIdAsync(request.Activity.ItemId, cancellationToken);

        if (activity is null)
        {
            return OmnichannelHandoffResult.Failure("The activity could not be found.");
        }

        // Idempotency: a redelivered provider event must not act twice. A routed handoff moves the activity into
        // the manual (agent) lane; an after-hours handoff concludes it. Either way, once it is no longer a live
        // automated call it has already been handled — this guard is what stops a redelivered speak.ended from
        // enqueuing the call a second time or scheduling a duplicate callback.
        if (activity.InteractionType == ActivityInteractionType.Manual ||
            activity.Status is ActivityStatus.Completed
                or ActivityStatus.Cancelled
                or ActivityStatus.Failed
                or ActivityStatus.Purged)
        {
            return OmnichannelHandoffResult.Success("The call was already handed off.");
        }

        // After-hours gate: if the destination queue is closed right now, do not seat the caller in a queue nobody
        // is staffing. Schedule a callback for when it re-opens and tell the caller, rather than leaving them
        // waiting or hanging up bluntly.
        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);
        var businessHoursGate = _serviceProvider.GetService<IBusinessHoursGate>();

        if (businessHoursGate is not null && queue is not null && !string.IsNullOrWhiteSpace(queue.BusinessHoursCalendarId))
        {
            var open = await businessHoursGate.IsOpenAsync(queue.BusinessHoursCalendarId, _clock.UtcNow, timeZoneId: null, cancellationToken);

            if (!open)
            {
                await ScheduleAfterHoursCallbackAsync(activity, queueId, cancellationToken);

                return OmnichannelHandoffResult.CallbackScheduled("The destination queue is closed; a callback was scheduled.");
            }
        }

        // The live call needs a Contact Center interaction so the offer/connect pipeline can bridge an agent onto
        // it. An automated outbound call has none, so create one carrying the provider call identifier.
        var interaction = await _interactionManager.FindByActivityIdAsync(activity.ItemId, cancellationToken);

        if (interaction is null)
        {
            interaction = await _interactionManager.NewAsync(cancellationToken: cancellationToken);
            interaction.Channel = InteractionChannel.Voice;
            interaction.Direction = InteractionDirection.Inbound;
            interaction.ActivityItemId = activity.ItemId;
            interaction.ProviderName = request.ProviderName;
            interaction.ProviderInteractionId = request.ProviderCallId;
            interaction.CustomerAddress = activity.PreferredDestination;
            interaction.QueueId = queueId;

            await _interactionManager.CreateAsync(interaction, cancellationToken: cancellationToken);
        }
        else
        {
            interaction.QueueId = queueId;

            if (string.IsNullOrWhiteSpace(interaction.ProviderInteractionId))
            {
                interaction.ProviderName = request.ProviderName;
                interaction.ProviderInteractionId = request.ProviderCallId;
            }

            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        // Move the activity from the automated lane into the manual/queued lane so it routes like an inbound call.
        // Mark it escalated (durable) so containment reporting counts it even though it leaves the automated lane.
        activity.Kind = ActivityKind.Call;
        activity.Source = ActivitySources.Inbound;
        activity.InteractionType = ActivityInteractionType.Manual;
        activity.Status = ActivityStatus.AwaitingAgentResponse;
        activity.AiEscalated = true;

        var workState = await _workStateService.MutateAsync(
            activity.ItemId,
            state => state.TransitionTo(ActivityAssignmentStatus.Available),
            cancellationToken);

        if (workState is not null)
        {
            ContactCenterWorkStateProjector.Apply(activity, workState);
        }

        await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);

        await _queueService.EnqueueAsync(activity.ItemId, queueId, priority: null, cancellationToken);

        var offeredUserId = await _offerService.OfferNextAsync(queueId, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Handed off automated voice Activity {ActivityId} to queue {QueueId}; {OfferState}.",
                activity.ItemId.SanitizeLogValue(),
                queueId.SanitizeLogValue(),
                string.IsNullOrEmpty(offeredUserId) ? "waiting for the next available agent" : "offered to an available agent");
        }

        return OmnichannelHandoffResult.Success(
            string.IsNullOrEmpty(offeredUserId)
                ? "The caller is waiting in the queue for the next agent."
                : "The caller was offered to an available agent.",
            offeredToUserId: offeredUserId);
    }

    private async Task ScheduleAfterHoursCallbackAsync(OmnichannelActivity activity, string queueId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var callbackService = _serviceProvider.GetService<ICallbackService>();

        if (callbackService is not null && !string.IsNullOrWhiteSpace(activity.PreferredDestination))
        {
            await callbackService.ScheduleAsync(new CallbackRequest
            {
                ItemId = IdGenerator.GenerateId(),
                Destination = activity.PreferredDestination,
                QueueId = queueId,
                ContactContentItemId = activity.ContactContentItemId,
                ContactContentType = activity.ContactContentType,
                CampaignId = activity.CampaignId,
                RequestedUtc = now,
                ScheduledUtc = now,
                Notes = "Callback requested because the AI escalated to a live agent while the destination queue was closed.",
            }, cancellationToken);
        }

        // Conclude the automated call: it did not route live, but the callback carries the work forward.
        activity.Status = ActivityStatus.Completed;
        activity.CompletedUtc = now;
        activity.TerminalReasonCode = OmnichannelConstants.TerminalReasons.HandedOffAfterHoursCallback;
        activity.AiEscalated = true;

        await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Scheduled an after-hours callback for voice Activity {ActivityId} to queue {QueueId}.", activity.ItemId.SanitizeLogValue(), queueId.SanitizeLogValue());
        }
    }
}
