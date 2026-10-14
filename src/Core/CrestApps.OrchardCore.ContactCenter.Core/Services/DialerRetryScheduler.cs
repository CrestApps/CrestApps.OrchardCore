using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IDialerRetryScheduler"/>. It creates the next attempt the same way a subject's "Try again"
/// action does, and the same <see cref="IFollowUpActivityHandler"/>s queue it for the dialer or refuse it past the
/// attempt limit.
/// </summary>
public sealed class DialerRetryScheduler : IDialerRetryScheduler
{
    /// <summary>
    /// What a follow-up created by this scheduler for a workflow reports as its creator.
    /// </summary>
    public const string CreatedBy = "Workflow";

    /// <summary>
    /// What a follow-up the dialer created itself, for an outcome the subject's flow has no rule for, reports as its
    /// creator.
    /// </summary>
    public const string CreatedByDialer = "Dialer";

    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IEnumerable<IFollowUpActivityHandler> _followUpHandlers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerRetryScheduler"/> class.
    /// </summary>
    /// <param name="activityManager">The activity manager.</param>
    /// <param name="followUpHandlers">The handlers that queue the next attempt or refuse it.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DialerRetryScheduler(
        IOmnichannelActivityManager activityManager,
        IEnumerable<IFollowUpActivityHandler> followUpHandlers,
        IClock clock,
        ILogger<DialerRetryScheduler> logger)
    {
        _activityManager = activityManager;
        _followUpHandlers = followUpHandlers;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<DialerRetryScheduleResult> ScheduleRetryAsync(string activityItemId, int? delayMinutes, CancellationToken cancellationToken = default)
        => ScheduleAsync(activityItemId, delayMinutes, CreatedBy, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// No delay is asked for: the dialer profile's retry delay, which the follow-up handler applies, is the wait. The
    /// handler also refuses the attempt past the profile's Max attempts, since no maximum of the subject's own applies.
    /// </remarks>
    public Task<DialerRetryScheduleResult> ScheduleUnhandledOutcomeRetryAsync(string activityItemId, CancellationToken cancellationToken = default)
        => ScheduleAsync(activityItemId, delayMinutes: null, CreatedByDialer, cancellationToken);

    private async Task<DialerRetryScheduleResult> ScheduleAsync(string activityItemId, int? delayMinutes, string createdBy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activityItemId))
        {
            return NotRetryable("No activity was named.");
        }

        var activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);

        if (activity is null)
        {
            return NotRetryable("The activity does not exist.");
        }

        if (!DialerActivitySourceHelper.IsDialerSource(activity.Source))
        {
            return NotRetryable("The activity is not dialer campaign work.");
        }

        if (!activity.Status.IsTerminal())
        {
            return NotRetryable("The activity has not finished.");
        }

        // The dialer retries a record its flow has no rule for, and a workflow may ask too when the same call ends: the
        // first request schedules the next attempt, and a later one finds it rather than dialing the contact twice.
        if (activity.TryGet<DialerRetryRecord>(out var scheduled) && !string.IsNullOrEmpty(scheduled.NextActivityItemId))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Attempt {AttemptNumber} ('{NextActivityId}') of dialer activity '{ActivityId}' was already scheduled by {ScheduledBy}; the request from {CreatedBy} did not schedule another.",
                    scheduled.AttemptNumber,
                    scheduled.NextActivityItemId.SanitizeLogValue(),
                    activity.ItemId.SanitizeLogValue(),
                    scheduled.CreatedBy.SanitizeLogValue(),
                    createdBy);
            }

            return new DialerRetryScheduleResult
            {
                Status = DialerRetryScheduleStatus.Scheduled,
                NextActivityItemId = scheduled.NextActivityItemId,
                AttemptNumber = scheduled.AttemptNumber,
                DueUtc = scheduled.DueUtc,
            };
        }

        var now = _clock.UtcNow;
        var nextAttempt = OmnichannelActivityFollowUps.CreateNextAttempt(activity, now);
        nextAttempt.ScheduledUtc = now.AddMinutes(Math.Max(0, delayMinutes ?? 0));

        var context = new FollowUpActivityContext
        {
            PreviousActivity = activity,
            FollowUpActivity = nextAttempt,
            CreatedBy = createdBy,
            HasChosenSchedule = delayMinutes.HasValue,
        };

        foreach (var handler in _followUpHandlers)
        {
            await handler.CreatingAsync(context, cancellationToken);
        }

        if (context.Cancel)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "{CreatedBy} asked to try dialer activity '{ActivityId}' again, but no attempt was created: {Reason}",
                    createdBy,
                    activity.ItemId.SanitizeLogValue(),
                    context.CancelReason.SanitizeLogValue());
            }

            return new DialerRetryScheduleResult
            {
                Status = DialerRetryScheduleStatus.AttemptsExhausted,
                Reason = context.CancelReason,
            };
        }

        await _activityManager.CreateAsync(nextAttempt, cancellationToken: cancellationToken);

        activity.Put(new DialerRetryRecord
        {
            NextActivityItemId = nextAttempt.ItemId,
            AttemptNumber = nextAttempt.Attempts,
            DueUtc = nextAttempt.ScheduledUtc,
            CreatedBy = createdBy,
        });

        await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "{CreatedBy} scheduled attempt {AttemptNumber} ('{NextActivityId}') of dialer activity '{ActivityId}', due {DueUtc:O}.",
                createdBy,
                nextAttempt.Attempts,
                nextAttempt.ItemId.SanitizeLogValue(),
                activity.ItemId.SanitizeLogValue(),
                nextAttempt.ScheduledUtc);
        }

        return new DialerRetryScheduleResult
        {
            Status = DialerRetryScheduleStatus.Scheduled,
            NextActivityItemId = nextAttempt.ItemId,
            AttemptNumber = nextAttempt.Attempts,
            DueUtc = nextAttempt.ScheduledUtc,
        };
    }

    private static DialerRetryScheduleResult NotRetryable(string reason)
        => new()
        {
            Status = DialerRetryScheduleStatus.NotRetryable,
            Reason = reason,
        };
}
