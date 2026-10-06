using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Puts the next attempt of a dialer campaign record back in its campaign's queue, with the dialer profile that dialed
/// the first, so the dialer calls it again.
/// </summary>
/// <remarks>
/// A disposition's "Try again" action creates the next attempt as a new activity. Nothing queued it, so the dialer never
/// saw it and the contact was never called again. It now goes into the same campaign queue the first attempt was dialed
/// from, carrying the attempt number on, due no sooner than the profile's retry delay after the attempt it follows. One
/// past the profile's attempt limit is not created at all, and one the action gave to a named person stays that person's
/// own work. The dialer picks the agent; a follow-up of an attempt an agent dispositioned prefers that agent, as a
/// sticky preference only. A follow-up of an attempt whose call was abandoned -- a person answered and no agent was
/// there -- is marked so an over-dialing campaign retries it only with an agent reserved for it.
/// </remarks>
public sealed class DialerFollowUpActivityHandler : IFollowUpActivityHandler
{
    private readonly IQueueItemManager _queueItemManager;
    private readonly IDialerProfileReader _profileReader;
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerFollowUpActivityHandler"/> class.
    /// </summary>
    /// <param name="queueItemManager">The queue items, read for the queue and profile the first attempt was dialed with.</param>
    /// <param name="profileReader">The dialer profiles, read for the attempt limit and the retry delay.</param>
    /// <param name="interactionManager">The interactions, read for whether the first attempt's call was abandoned.</param>
    /// <param name="scopeExecutor">The executor that queues the follow-up once it has been saved.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DialerFollowUpActivityHandler(
        IQueueItemManager queueItemManager,
        IDialerProfileReader profileReader,
        IInteractionManager interactionManager,
        IContactCenterScopeExecutor scopeExecutor,
        IClock clock,
        ILogger<DialerFollowUpActivityHandler> logger)
    {
        _queueItemManager = queueItemManager;
        _profileReader = profileReader;
        _interactionManager = interactionManager;
        _scopeExecutor = scopeExecutor;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CreatingAsync(FollowUpActivityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var previous = context.PreviousActivity;
        var followUp = context.FollowUpActivity;

        if (previous is null ||
            followUp is null ||
            context.Cancel ||
            context.HasNamedOwner ||
            !DialerActivitySourceHelper.IsDialerSource(previous.Source))
        {
            return;
        }

        var previousItem = await _queueItemManager.FindByActivityIdAsync(previous.ItemId, cancellationToken);

        if (previousItem is null ||
            string.IsNullOrEmpty(previousItem.QueueId) ||
            string.IsNullOrEmpty(previousItem.DialerProfileId) ||
            QueueCallbackDialerProfile.IsCallbackProfile(previousItem.DialerProfileId))
        {
            _logger.LogWarning(
                "The next attempt of dialer activity '{ActivityId}' was created, but the queue and dialer profile the first attempt was dialed with could not be found, so it was not queued for the dialer.",
                previous.ItemId.SanitizeLogValue());

            return;
        }

        var profile = await _profileReader.FindByIdAsync(previousItem.DialerProfileId, cancellationToken);

        if (profile is null)
        {
            _logger.LogWarning(
                "The next attempt of dialer activity '{ActivityId}' was created, but dialer profile '{DialerProfileId}' no longer exists, so it was not queued for the dialer.",
                previous.ItemId.SanitizeLogValue(),
                previousItem.DialerProfileId.SanitizeLogValue());

            return;
        }

        if (followUp.Attempts > profile.MaxAttempts)
        {
            context.Cancel = true;
            context.CancelReason = $"attempt {followUp.Attempts} would exceed the {profile.MaxAttempts} attempts dialer profile '{profile.Name}' allows.";

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Did not create attempt {AttemptNumber} of dialer activity '{ActivityId}': dialer profile '{Profile}' allows {MaxAttempts} attempts.",
                    followUp.Attempts,
                    previous.ItemId.SanitizeLogValue(),
                    profile.Name,
                    profile.MaxAttempts);
            }

            return;
        }

        var earliestUtc = (previous.CompletedUtc ?? _clock.UtcNow).AddMinutes(Math.Max(0, profile.RetryDelayMinutes));

        if (followUp.ScheduledUtc < earliestUtc)
        {
            followUp.ScheduledUtc = earliestUtc;
        }

        var activityItemId = followUp.ItemId;
        var queueId = previousItem.QueueId;
        var dialerProfileId = previousItem.DialerProfileId;

        // A person who answered the last call and found nobody there is not called again without an agent waiting for
        // them: the follow-up is a new activity, so the mark travels with its queue item. Once set it stays on every later
        // attempt of the contact, the safe reading of the rules on repeat calls after an abandoned one.
        var requiresReservedAgent = previousItem.RequiresReservedAgent || await WasAbandonedAsync(previous.ItemId, cancellationToken);

        // Queueing commits, and the follow-up is only saved after this returns, so it is queued once it is.
        if (!_scopeExecutor.ScheduleAfterCommit<IActivityQueueService>(queueService =>
            queueService.EnqueueAsync(activityItemId, queueId, priority: null, dialerProfileId, requiresReservedAgent, CancellationToken.None)))
        {
            _logger.LogWarning(
                "The next attempt '{FollowUpId}' of dialer activity '{ActivityId}' could not be queued for the dialer because there was no scope to queue it from after it was saved.",
                activityItemId.SanitizeLogValue(),
                previous.ItemId.SanitizeLogValue());

            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Queued attempt {AttemptNumber} of {MaxAttempts} ('{FollowUpId}') of dialer activity '{ActivityId}' in queue '{QueueId}' with dialer profile '{DialerProfileId}', due {DueUtc:O}; created by {CreatedBy}; an agent reserved for it: {RequiresReservedAgent}.",
                followUp.Attempts,
                profile.MaxAttempts,
                activityItemId.SanitizeLogValue(),
                previous.ItemId.SanitizeLogValue(),
                queueId.SanitizeLogValue(),
                dialerProfileId.SanitizeLogValue(),
                followUp.ScheduledUtc,
                context.CreatedBy.SanitizeLogValue(),
                requiresReservedAgent);
        }
    }

    private async Task<bool> WasAbandonedAsync(string activityItemId, CancellationToken cancellationToken)
    {
        var interaction = await _interactionManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        return interaction is not null && DialerCallMetadata.IsAbandoned(interaction);
    }
}
