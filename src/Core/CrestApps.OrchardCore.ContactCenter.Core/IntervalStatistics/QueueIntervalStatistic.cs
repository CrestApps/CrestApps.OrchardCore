using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// One queue's work on one channel over one interval: what was offered, answered and abandoned, how long callers
/// waited, and how long the work took. Work belongs to the interval it was offered in, the way the Contact Center
/// reports count it, so the intervals of a day add up to the day's report.
/// </summary>
public sealed class QueueIntervalStatistic
{
    /// <summary>
    /// Gets or sets the queue.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public InteractionChannel Channel { get; set; }

    /// <summary>
    /// Gets or sets the UTC start of the interval.
    /// </summary>
    public DateTime IntervalStartUtc { get; set; }

    /// <summary>
    /// Gets or sets the interval's length in minutes.
    /// </summary>
    public int IntervalMinutes { get; set; } = IntervalStatisticsConstants.IntervalMinutes;

    /// <summary>
    /// Gets or sets the inbound work offered.
    /// </summary>
    public long Offered { get; set; }

    /// <summary>
    /// Gets or sets the outbound work started from the queue.
    /// </summary>
    public long Outbound { get; set; }

    /// <summary>
    /// Gets or sets the inbound work an agent answered.
    /// </summary>
    public long Answered { get; set; }

    /// <summary>
    /// Gets or sets the answered work, inbound or outbound, that has ended.
    /// </summary>
    public long Handled { get; set; }

    /// <summary>
    /// Gets or sets the inbound work the customer abandoned.
    /// </summary>
    public long Abandoned { get; set; }

    /// <summary>
    /// Gets or sets the abandoned work whose customer waited less than the short-abandon threshold.
    /// </summary>
    public long ShortAbandoned { get; set; }

    /// <summary>
    /// Gets or sets the work sent to voicemail.
    /// </summary>
    public long Voicemail { get; set; }

    /// <summary>
    /// Gets or sets the work whose customer took a callback.
    /// </summary>
    public long CallbackRequested { get; set; }

    /// <summary>
    /// Gets or sets the answered work that was transferred.
    /// </summary>
    public long Transferred { get; set; }

    /// <summary>
    /// Gets or sets the inbound work the service level is measured over: answered or abandoned.
    /// </summary>
    public long ServiceLevelOffered { get; set; }

    /// <summary>
    /// Gets or sets the inbound work answered within the queue's threshold.
    /// </summary>
    public long AnsweredWithinServiceLevel { get; set; }

    /// <summary>
    /// Gets or sets the queue's service level threshold in seconds when the interval was computed.
    /// </summary>
    public int ServiceLevelThresholdSeconds { get; set; }

    /// <summary>
    /// Gets or sets the total seconds answered work waited.
    /// </summary>
    public double AnswerWaitSeconds { get; set; }

    /// <summary>
    /// Gets or sets the longest wait of answered work, in seconds.
    /// </summary>
    public double MaxAnswerWaitSeconds { get; set; }

    /// <summary>
    /// Gets or sets the talk seconds of handled work.
    /// </summary>
    public double TalkSeconds { get; set; }

    /// <summary>
    /// Gets or sets the wrap-up seconds of handled work.
    /// </summary>
    public double WrapUpSeconds { get; set; }

    /// <summary>
    /// Gets the handle seconds: talk and wrap-up.
    /// </summary>
    public double HandleSeconds => TalkSeconds + WrapUpSeconds;

    /// <summary>
    /// Gets the average handle time in seconds, or zero when nothing was handled.
    /// </summary>
    public double AverageHandleSeconds => Handled > 0 ? HandleSeconds / Handled : 0d;

    /// <summary>
    /// Gets the average speed of answer in seconds, or zero when nothing was answered.
    /// </summary>
    public double AverageSpeedOfAnswerSeconds => Answered > 0 ? AnswerWaitSeconds / Answered : 0d;

    /// <summary>
    /// Gets the service level as a fraction, or <see langword="null"/> when nothing was answered or abandoned.
    /// </summary>
    public double? ServiceLevel => ServiceLevelOffered > 0 ? (double)AnsweredWithinServiceLevel / ServiceLevelOffered : null;

    /// <summary>
    /// Adds another interval's counts to this one, for a longer interval or several queues.
    /// </summary>
    /// <param name="other">The interval to add.</param>
    public void Add(QueueIntervalStatistic other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Offered += other.Offered;
        Outbound += other.Outbound;
        Answered += other.Answered;
        Handled += other.Handled;
        Abandoned += other.Abandoned;
        ShortAbandoned += other.ShortAbandoned;
        Voicemail += other.Voicemail;
        CallbackRequested += other.CallbackRequested;
        Transferred += other.Transferred;
        ServiceLevelOffered += other.ServiceLevelOffered;
        AnsweredWithinServiceLevel += other.AnsweredWithinServiceLevel;
        AnswerWaitSeconds += other.AnswerWaitSeconds;
        MaxAnswerWaitSeconds = Math.Max(MaxAnswerWaitSeconds, other.MaxAnswerWaitSeconds);
        TalkSeconds += other.TalkSeconds;
        WrapUpSeconds += other.WrapUpSeconds;
        ServiceLevelThresholdSeconds = Math.Max(ServiceLevelThresholdSeconds, other.ServiceLevelThresholdSeconds);
    }
}
