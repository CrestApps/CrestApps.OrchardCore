using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDialerService"/>. The Contact Center owns the
/// agent reservation, attempt limits, and compliance decisions; calling is delegated to a provider.
/// </summary>
public sealed class DialerService : IDialerService
{
    private readonly IActivityAssignmentService _assignmentService;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IDialerProviderResolver _providerResolver;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerService"/> class.
    /// </summary>
    /// <param name="assignmentService">The assignment service used to reserve agents and activities.</param>
    /// <param name="interactionManager">The interaction manager used to record attempts.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="providerResolver">The dialer provider resolver.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp attempts.</param>
    /// <param name="logger">The logger instance.</param>
    public DialerService(
        IActivityAssignmentService assignmentService,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IDialerProviderResolver providerResolver,
        IContactCenterEventPublisher publisher,
        IClock clock,
        ILogger<DialerService> logger)
    {
        _assignmentService = assignmentService;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _providerResolver = providerResolver;
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

        var provider = _providerResolver.Get(profile.ProviderName);

        if (provider is null)
        {
            _logger.LogWarning("No dialer provider is registered for dialer profile '{Profile}'.", profile.Name);

            return 0;
        }

        var started = 0;

        var reservation = await _assignmentService.AssignNextAsync(profile.QueueId, cancellationToken);

        while (reservation is not null)
        {
            if (await TryDialAsync(profile, reservation, provider, cancellationToken))
            {
                started++;
            }

            reservation = await _assignmentService.AssignNextAsync(profile.QueueId, cancellationToken);
        }

        return started;
    }

    private async Task<bool> TryDialAsync(DialerProfile profile, ActivityReservation reservation, IDialerProvider provider, CancellationToken cancellationToken)
    {
        var activity = await _activityManager.FindByIdAsync(reservation.ActivityItemId, cancellationToken);

        if (activity is null || activity.Attempts > profile.MaxAttempts)
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
        interaction.ProviderName = provider.TechnicalName;
        interaction.CustomerAddress = activity.PreferredDestination;
        await _interactionManager.CreateAsync(interaction, cancellationToken: cancellationToken);

        var result = await provider.PlaceCallAsync(new DialerDialRequest
        {
            ActivityId = activity.ItemId,
            InteractionId = interaction.ItemId,
            AgentId = reservation.AgentId,
            QueueId = profile.QueueId,
            CampaignId = profile.CampaignId,
            Destination = activity.PreferredDestination,
            CallerId = profile.CallerId,
        }, cancellationToken);

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
