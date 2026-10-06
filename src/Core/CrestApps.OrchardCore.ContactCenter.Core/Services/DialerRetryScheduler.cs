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
    /// What a follow-up created by this scheduler reports as its creator.
    /// </summary>
    public const string CreatedBy = "Workflow";

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
    public async Task<DialerRetryScheduleResult> ScheduleRetryAsync(string activityItemId, int? delayMinutes, CancellationToken cancellationToken = default)
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

        var now = _clock.UtcNow;
        var nextAttempt = OmnichannelActivityFollowUps.CreateNextAttempt(activity, now);
        nextAttempt.ScheduledUtc = now.AddMinutes(Math.Max(0, delayMinutes ?? 0));

        var context = new FollowUpActivityContext
        {
            PreviousActivity = activity,
            FollowUpActivity = nextAttempt,
            CreatedBy = CreatedBy,
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
                    "A workflow asked to try dialer activity '{ActivityId}' again, but no attempt was created: {Reason}",
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

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "A workflow scheduled attempt {AttemptNumber} ('{NextActivityId}') of dialer activity '{ActivityId}', due {DueUtc:O}.",
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
