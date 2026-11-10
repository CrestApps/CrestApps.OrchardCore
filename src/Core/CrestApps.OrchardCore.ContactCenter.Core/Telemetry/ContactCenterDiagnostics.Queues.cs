using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// The queue funnel: work entering a queue, how long it waited and how it left, admission when a queue is full, the
/// maximum-wait action and overflow.
/// </summary>
public static partial class ContactCenterDiagnostics
{
    /// <summary>
    /// An agent took the work.
    /// </summary>
    internal const string QueueExitAssigned = "assigned";

    /// <summary>
    /// The caller hung up while waiting.
    /// </summary>
    internal const string QueueExitAbandoned = "abandoned";

    /// <summary>
    /// The caller was sent to voicemail.
    /// </summary>
    internal const string QueueExitVoicemail = "voicemail";

    /// <summary>
    /// The work moved on to an overflow queue.
    /// </summary>
    internal const string QueueExitOverflowed = "overflowed";

    /// <summary>
    /// The work waited past the queue's maximum and its maximum-wait action took it out.
    /// </summary>
    internal const string QueueExitTimeout = "timeout";

    /// <summary>
    /// The work was transferred or completed while still queued.
    /// </summary>
    internal const string QueueExitCompleted = "completed";

    /// <summary>
    /// The work was taken out of the queue for another reason: withdrawn, recovered, or finished by the dialer.
    /// </summary>
    internal const string QueueExitRemoved = "removed";

    private static readonly AsyncLocal<string> _queueExitOutcome = new();

    private static readonly Counter<long> _queueItemsEnqueued = Meter.CreateCounter<long>(
        "crestapps.contactcenter.queue.item",
        "{item}",
        "The number of interactions that entered a queue, by channel and routing kind.");

    private static readonly Histogram<double> _queueWaitDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.queue.wait.duration",
        "The time an interaction waited in a queue, by how it left: assigned, abandoned, voicemail, overflowed, timeout, completed or removed.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    private static readonly Counter<long> _queueOverflows = Meter.CreateCounter<long>(
        "crestapps.contactcenter.queue.overflow",
        "{item}",
        "The number of queued interactions moved on to an overflow queue.");

    private static readonly Counter<long> _queueMaxWaitActions = Meter.CreateCounter<long>(
        "crestapps.contactcenter.queue.max_wait_action",
        "{item}",
        "The number of times a queue's maximum-wait action was applied to a caller who waited too long, by action and result.");

    private static readonly Counter<long> _queueAdmissions = Meter.CreateCounter<long>(
        "crestapps.contactcenter.queue.admission",
        "{call}",
        "The number of admission decisions made for a queue with a size limit, by result.");

    /// <summary>
    /// Names how the queued work that leaves its queue within the returned scope leaves it, for a caller that knows
    /// better than the queue does, such as sending a waiting caller to voicemail. An outer scope wins over an inner
    /// one: the maximum-wait action that sends a caller to voicemail knows they timed out.
    /// </summary>
    /// <param name="outcome">One of the <c>QueueExit*</c> values.</param>
    /// <returns>A scope that restores the previous value when disposed.</returns>
    internal static IDisposable BeginQueueExit(string outcome)
    {
        var previous = _queueExitOutcome.Value;
        _queueExitOutcome.Value = previous ?? outcome;

        return new QueueExitScope(previous);
    }

    /// <summary>
    /// Records that an interaction entered a queue.
    /// </summary>
    /// <param name="queueItem">The queue item.</param>
    /// <param name="channel">The channel of the interaction, when known.</param>
    internal static void RecordQueueEntered(QueueItem queueItem, InteractionChannel? channel)
    {
        ArgumentNullException.ThrowIfNull(queueItem);

        if (!_queueItemsEnqueued.Enabled)
        {
            return;
        }

        Add(_queueItemsEnqueued, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannel(channel) },
            { KindTag, GetRoutingKind(queueItem.QueueId) },
        });
    }

    /// <summary>
    /// Records how long an interaction waited in its queue, and how it left. Work that leaves within a
    /// <see cref="BeginQueueExit(string)"/> scope leaves the way the scope names.
    /// </summary>
    /// <param name="queueItem">The queue item, with its entry time.</param>
    /// <param name="outcome">How it left: one of the <c>QueueExit*</c> values.</param>
    /// <param name="channel">The channel of the interaction, when known.</param>
    /// <param name="leftUtc">When it left.</param>
    internal static void RecordQueueExit(QueueItem queueItem, string outcome, InteractionChannel? channel, DateTime leftUtc)
    {
        ArgumentNullException.ThrowIfNull(queueItem);

        outcome = _queueExitOutcome.Value ?? outcome;

        Activity.Current?.AddEvent(new ActivityEvent("crestapps.contactcenter.queue.exit", tags: new ActivityTagsCollection
        {
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
        }));

        if (!_queueWaitDuration.Enabled)
        {
            return;
        }

        var enteredUtc = queueItem.QueueEnteredUtc == default ? queueItem.EnqueuedUtc : queueItem.QueueEnteredUtc;

        if (enteredUtc == default)
        {
            return;
        }

        Record(_queueWaitDuration, Math.Max(0, (leftUtc - enteredUtc).TotalSeconds), new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannel(channel) },
            { KindTag, GetRoutingKind(queueItem.QueueId) },
        });
    }

    /// <summary>
    /// Records that queued work moved on to an overflow queue.
    /// </summary>
    /// <param name="channel">The channel of the interaction, when known.</param>
    internal static void RecordQueueOverflow(InteractionChannel? channel)
    {
        if (!_queueOverflows.Enabled)
        {
            return;
        }

        Add(_queueOverflows, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannel(channel) },
        });
    }

    /// <summary>
    /// Records that a queue's maximum-wait action was applied.
    /// </summary>
    /// <param name="action">The action: <c>overflow</c> or <c>voicemail</c>.</param>
    /// <param name="result"><c>applied</c>, or <c>no_target</c> when there was nowhere to send the caller.</param>
    internal static void RecordQueueMaxWaitAction(string action, string result)
    {
        if (!_queueMaxWaitActions.Enabled)
        {
            return;
        }

        Add(_queueMaxWaitActions, new TagList
        {
            { ActionTag, action },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        });
    }

    /// <summary>
    /// Records an admission decision for a queue with a size limit.
    /// </summary>
    /// <param name="result"><c>admitted</c>, <c>voicemail</c>, <c>overflowed</c> or <c>admitted_over_limit</c>.</param>
    internal static void RecordQueueAdmission(string result)
    {
        if (!_queueAdmissions.Enabled)
        {
            return;
        }

        Add(_queueAdmissions, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        });
    }

    private sealed class QueueExitScope : IDisposable
    {
        private readonly string _previous;

        public QueueExitScope(string previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            _queueExitOutcome.Value = _previous;
        }
    }
}
