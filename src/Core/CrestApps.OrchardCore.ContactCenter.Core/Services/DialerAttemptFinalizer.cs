using System.Text;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IDialerAttemptFinalizer"/>.
/// </summary>
/// <remarks>
/// Routing is let go of before the activity is completed. Completing assigned work is what returns its agent from wrap-up
/// to ready, and by the time an unanswered attempt is reported the agent may already be on the next call: releasing the
/// work first is what keeps the completion from touching an agent who never had the call.
/// </remarks>
public sealed class DialerAttemptFinalizer : IDialerAttemptFinalizer
{
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerAttemptFinalizer"/> class.
    /// </summary>
    /// <param name="activityManager">The activity manager used to load the dialed activity.</param>
    /// <param name="interactionManager">The interaction manager used to read the attempt's call.</param>
    /// <param name="workStateService">The routing-owned work state, released before the activity is completed.</param>
    /// <param name="serviceProvider">
    /// The scope's services. Releasing routing and completing the activity reach the presence, queue and disposition
    /// services, which publish through the event publisher; they are resolved when needed so this service can be used
    /// from an event handler without closing a dependency cycle.
    /// </param>
    /// <param name="logger">The logger.</param>
    public DialerAttemptFinalizer(
        IOmnichannelActivityManager activityManager,
        IInteractionManager interactionManager,
        IContactCenterWorkStateService workStateService,
        IServiceProvider serviceProvider,
        ILogger<DialerAttemptFinalizer> logger)
    {
        _activityManager = activityManager;
        _interactionManager = interactionManager;
        _workStateService = workStateService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<DialerAttemptFinalizationResult> FinalizeAsync(DialerAttemptFinalizationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.ActivityItemId) || !DialerAttemptOutcomes.IsPreConnect(request.Outcome))
        {
            return DialerAttemptFinalizationResult.NotApplicable;
        }

        var activity = await _activityManager.FindByIdAsync(request.ActivityItemId, cancellationToken);

        // Only campaign work is the dialer's to finish. A queued callback keeps its own retry, and work of any other
        // kind was never the dialer's.
        if (activity is null || !DialerActivitySourceHelper.IsDialerSource(activity.Source))
        {
            return DialerAttemptFinalizationResult.NotApplicable;
        }

        if (activity.Status.IsTerminal())
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Dialer attempt for activity '{ActivityId}' ended as {Outcome}, but the activity had already finished as {Status}; left as it was.",
                    activity.ItemId.SanitizeLogValue(),
                    request.Outcome,
                    activity.Status);
            }

            return DialerAttemptFinalizationResult.AlreadyFinished;
        }

        var interaction = string.IsNullOrEmpty(request.InteractionId)
            ? null
            : await _interactionManager.FindByIdAsync(request.InteractionId, cancellationToken);

        // A call still live is not over, and a call an agent was connected to is the agent's to disposition, whatever
        // its outcome says.
        if (interaction is not null &&
            (!interaction.IsSettled ||
                (DialerCallMetadata.HasAgentJoined(interaction) &&
                    !string.Equals(request.Outcome, DialerAttemptOutcomes.AnsweringMachine, StringComparison.Ordinal))))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Left dialer activity '{ActivityId}' for the agent: its call '{InteractionId}' is {Status} and an agent {Joined} connected to it.",
                    activity.ItemId.SanitizeLogValue(),
                    interaction.ItemId.SanitizeLogValue(),
                    interaction.Status,
                    DialerCallMetadata.HasAgentJoined(interaction) ? "was" : "was not");
            }

            return DialerAttemptFinalizationResult.NotApplicable;
        }

        await ReleaseRoutingAsync(request, activity, cancellationToken);

        var phoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? interaction?.CustomerAddress ?? activity.PreferredDestination
            : request.PhoneNumber;
        var attemptNumber = interaction is null ? null : DialerCallMetadata.GetAttemptNumber(interaction);
        var maxAttempts = interaction is null ? null : DialerCallMetadata.GetMaxAttempts(interaction);
        attemptNumber ??= Math.Max(1, activity.Attempts);

        ActivityDispositionResult result;

        if (string.Equals(request.Outcome, DialerAttemptOutcomes.NotInService, StringComparison.Ordinal))
        {
            var notInService = _serviceProvider.GetService<INotInServiceActivityCompleter>();

            if (notInService is null)
            {
                LogNoCompleter(activity, request);

                return DialerAttemptFinalizationResult.NotDispositioned;
            }

            result = await notInService.CompleteAsync(new NotInServiceCompletionRequest
            {
                Activity = activity,
                PhoneNumber = phoneNumber,
                Source = OmnichannelConstants.NotInServiceSources.Dialer,
                Reason = request.Detail,
            }, cancellationToken);
        }
        else
        {
            var completer = _serviceProvider.GetService<IActivityOutcomeCompleter>();

            if (completer is null)
            {
                LogNoCompleter(activity, request);

                return DialerAttemptFinalizationResult.NotDispositioned;
            }

            result = await completer.CompleteAsync(new ActivityOutcomeCompletionRequest
            {
                Activity = activity,
                Outcome = DialerTerminalReasons.ToDispositionOutcome(request.Outcome),
                FallbackOutcome = DispositionOutcome.NoAnswer,
                TerminalReasonCode = string.IsNullOrEmpty(request.TerminalReasonCode)
                    ? DialerTerminalReasons.ForOutcome(request.Outcome)
                    : request.TerminalReasonCode,
                Notes = BuildNotes(request, phoneNumber, attemptNumber.Value, maxAttempts),
                Source = ActivityDispositionSource.System,

                // The source stays System for reporting, but the person reading the activity is told it was the dialer.
                DispositionedBy = ActivityDispositionActor.Dialer,
            }, cancellationToken);
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Could not disposition dialer activity '{ActivityId}' after its attempt ended as {Outcome}: {Error}. Attempt {AttemptNumber} of {MaxAttempts}.",
                activity.ItemId.SanitizeLogValue(),
                request.Outcome,
                result.ErrorMessage.SanitizeLogValue(),
                attemptNumber,
                maxAttempts);

            return DialerAttemptFinalizationResult.NotDispositioned;
        }

        await RemoveWaitingQueueItemAsync(activity.ItemId, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Dialer dispositioned activity '{ActivityId}' automatically: outcome {Outcome}, disposition '{DispositionId}', terminal reason '{TerminalReason}', attempt {AttemptNumber} of {MaxAttempts}, interaction '{InteractionId}', reported by {Trigger}. No agent was assigned the attempt.",
                activity.ItemId.SanitizeLogValue(),
                request.Outcome,
                activity.DispositionId.SanitizeLogValue(),
                activity.TerminalReasonCode.SanitizeLogValue(),
                attemptNumber,
                maxAttempts,
                request.InteractionId.SanitizeLogValue(),
                request.Trigger.SanitizeLogValue());
        }

        return DialerAttemptFinalizationResult.Dispositioned;
    }

    /// <summary>
    /// Completes an activity that has used every attempt its dialer profile allows, with the disposition for how its
    /// last attempt ended; with nothing to disposition it, the activity ends failed, as it did before.
    /// </summary>
    internal static async Task FinalizeExhaustedAsync(IServiceProvider services, string activityItemId)
    {
        var interactionManager = services.GetRequiredService<IInteractionManager>();
        var lastInteraction = await interactionManager.FindByActivityIdAsync(activityItemId, CancellationToken.None);
        var lastOutcome = lastInteraction is null ? null : DialerCallMetadata.GetOutcome(lastInteraction);
        var finalizer = services.GetService<IDialerAttemptFinalizer>();
        var result = finalizer is null
            ? DialerAttemptFinalizationResult.NotDispositioned
            : await finalizer.FinalizeAsync(new DialerAttemptFinalizationRequest
            {
                ActivityItemId = activityItemId,
                InteractionId = lastInteraction?.ItemId,
                Outcome = string.IsNullOrEmpty(lastOutcome) || !DialerAttemptOutcomes.IsPreConnect(lastOutcome)
                    ? DialerAttemptOutcomes.NoAnswer
                    : lastOutcome,
                TerminalReasonCode = DialerTerminalReasons.MaxAttemptsReached,
                Trigger = "AttemptLimit",
            }, CancellationToken.None);

        if (result is DialerAttemptFinalizationResult.NotDispositioned or DialerAttemptFinalizationResult.NotApplicable)
        {
            await services.GetRequiredService<IContactCenterActivityWriter>().UpdateAsync(
                activityItemId,
                activity =>
                {
                    if (!activity.Status.IsTerminal())
                    {
                        activity.Status = ActivityStatus.Failed;
                        activity.TerminalReasonCode = DialerTerminalReasons.MaxAttemptsReached;
                    }
                },
                CancellationToken.None);
        }
    }

    private async Task ReleaseRoutingAsync(DialerAttemptFinalizationRequest request, OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        // The same release the call ending already ran; running it again is a no-op when it did, and when it has not
        // run yet (or was lost) it is what returns the agent to ready and lets go of the queue item.
        if (!string.IsNullOrEmpty(request.InteractionId))
        {
            var offerSynchronization = _serviceProvider.GetService<IProviderVoiceOfferSynchronizationService>();

            if (offerSynchronization is not null)
            {
                await offerSynchronization.ReconcileEndedOfferAsync(request.InteractionId, cancellationToken);
            }
        }

        var workState = await _workStateService.GetAsync(activity.ItemId, cancellationToken);

        if (workState is null ||
            (string.IsNullOrEmpty(workState.AssignedToId) && string.IsNullOrEmpty(workState.ReservationId)))
        {
            return;
        }

        var released = await _workStateService.MutateAsync(activity.ItemId, state =>
        {
            if (state.AssignmentStatus != ActivityAssignmentStatus.Released && state.CanTransitionTo(ActivityAssignmentStatus.Released))
            {
                state.TransitionTo(ActivityAssignmentStatus.Released);
            }

            state.AssignedToId = null;
            state.AssignedToUsername = null;
            state.AssignedToUtc = null;
            state.ReservationId = null;
            state.ReservedById = null;
            state.ReservedByUsername = null;
            state.ReservedUtc = null;
            state.ReservationExpiresUtc = null;
        }, cancellationToken);

        // The completion below saves the activity as loaded here; its copy of the assignment no longer names the agent
        // either.
        if (released is not null)
        {
            ContactCenterWorkStateProjector.Apply(activity, released);
        }
    }

    // The recovery sweep may have put the attempt back in the queue before this ran. A completed activity has nothing
    // left to dial, so the waiting item goes too, or it would be offered again. So does the item of a call an
    // over-dialing campaign placed without an agent: it stays Assigned with nobody on it, and left there it would count
    // as a call in flight for good.
    private async Task RemoveWaitingQueueItemAsync(string activityItemId, CancellationToken cancellationToken)
    {
        var queueItemManager = _serviceProvider.GetService<IQueueItemManager>();
        var queueService = _serviceProvider.GetService<IActivityQueueService>();

        if (queueItemManager is null || queueService is null)
        {
            return;
        }

        var queueItem = await queueItemManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (queueItem is not null &&
            (queueItem.Status == QueueItemStatus.Waiting ||
                (queueItem.Status == QueueItemStatus.Assigned && string.IsNullOrEmpty(queueItem.AgentId))))
        {
            await queueService.DequeueAsync(queueItem, QueueItemStatus.Removed, cancellationToken);
        }
    }

    private void LogNoCompleter(OmnichannelActivity activity, DialerAttemptFinalizationRequest request)
        => _logger.LogWarning(
            "Dialer attempt for activity '{ActivityId}' ended as {Outcome} before an agent was connected, but no disposition could be applied because activity management is not enabled. The agent was released; the activity was left for the recovery sweep.",
            activity.ItemId.SanitizeLogValue(),
            request.Outcome);

    private static string BuildNotes(DialerAttemptFinalizationRequest request, string phoneNumber, int attemptNumber, int? maxAttempts)
    {
        var notes = new StringBuilder("The dialer's call");

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            notes.Append(" to ").Append(phoneNumber);
        }

        notes.Append(" (attempt ").Append(attemptNumber);

        if (maxAttempts.HasValue)
        {
            notes.Append(" of ").Append(maxAttempts.Value);
        }

        notes.Append(") ").Append(DescribeOutcome(request.Outcome));

        if (!string.IsNullOrWhiteSpace(request.Detail))
        {
            notes.Append(" (").Append(request.Detail.Trim()).Append(')');
        }

        notes.Append(". No agent was connected, so the dialer completed the activity automatically.");

        if (string.Equals(request.TerminalReasonCode, DialerTerminalReasons.MaxAttemptsReached, StringComparison.Ordinal))
        {
            notes.Append(" Every attempt the dialer profile allows has been used.");
        }

        return notes.ToString();
    }

    private static string DescribeOutcome(string outcome)
        => outcome switch
        {
            DialerAttemptOutcomes.Busy => "found the line busy",
            DialerAttemptOutcomes.AnsweringMachine => "was answered by a machine and screened out",
            DialerAttemptOutcomes.Rejected => "was rejected",
            DialerAttemptOutcomes.Failed => "could not be completed",
            DialerAttemptOutcomes.Disconnected => "was answered, but the customer hung up before an agent was connected",
            DialerAttemptOutcomes.NotInService => "found the number not in service",
            _ => "was not answered",
        };
}
