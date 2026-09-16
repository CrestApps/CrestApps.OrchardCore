namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The answer to "may this caller wait here?": where they wait, or what happens to them instead.
/// </summary>
public sealed class QueueAdmissionDecision
{
    private QueueAdmissionDecision(QueueAdmissionOutcome outcome, string queueId)
    {
        Outcome = outcome;
        QueueId = queueId;
    }

    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public QueueAdmissionOutcome Outcome { get; }

    /// <summary>
    /// Gets the queue the caller waits in: the queue that was asked when admitted, the overflow queue when
    /// overflowed, or <see langword="null"/> when the caller does not wait at all.
    /// </summary>
    public string QueueId { get; }

    /// <summary>
    /// Gets a value indicating whether the caller waits somewhere.
    /// </summary>
    public bool IsQueued => Outcome is QueueAdmissionOutcome.Admitted or QueueAdmissionOutcome.Overflowed;

    /// <summary>
    /// The caller waits in the queue that was asked.
    /// </summary>
    /// <param name="queueId">The queue.</param>
    public static QueueAdmissionDecision Admit(string queueId)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        return new QueueAdmissionDecision(QueueAdmissionOutcome.Admitted, queueId);
    }

    /// <summary>
    /// The caller waits in an overflow queue because the one asked is full.
    /// </summary>
    /// <param name="overflowQueueId">The overflow queue.</param>
    public static QueueAdmissionDecision Overflow(string overflowQueueId)
    {
        ArgumentException.ThrowIfNullOrEmpty(overflowQueueId);

        return new QueueAdmissionDecision(QueueAdmissionOutcome.Overflowed, overflowQueueId);
    }

    /// <summary>
    /// The caller is sent to voicemail because the queue is full.
    /// </summary>
    public static QueueAdmissionDecision SendToVoicemail()
        => new(QueueAdmissionOutcome.Voicemail, queueId: null);
}
